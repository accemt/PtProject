using PtProject.Loader;
using PtProject.Domain.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

using FType = System.Double;
using PtProject.Domain;

namespace PtProject.Classifier
{
    public class RFClassifier : IClassifier
    {
        private DataLoader<FType> _trainLoader;
        private DataLoader<FType> _testLoader;

        /// <summary>
        /// Тестовые данные (на один идентификатор несколько массивов)
        /// </summary>
        private Dictionary<string, List<double[]>> _testDataDict;

        /// <summary>
        /// результат тестовых данных: id -> target
        /// </summary>
        private Dictionary<string, int> _resultDict;

        /// <summary>
        /// сюда сохраняем сумму глосований на тестовом множестве
        /// </summary>
        private Dictionary<string, double> _testProbSum = new Dictionary<string, double>(); 

        /// <summary>
        /// сюда сохраняем среднее глосований на тестовом множестве
        /// </summary>
        private Dictionary<string, double> _testProbAvg = new Dictionary<string, double>();

        private string _trainPath;
        private string _testPath;
        private string _target;
        private double _rfcoeff = 0.05;
        private int _nbatches = 100;
        private int _treesbatch = 1;
        private int _nclasses = 2;

        private Dictionary<int, DecisionForest> _treesDict = new Dictionary<int, DecisionForest>();

        
        /// <summary>
        /// Drops columns from learning set
        /// </summary>
        /// <param name="cols">set of columns</param>
        public void AddDropColumns(IEnumerable<string> cols)
        {
            foreach (var c in cols)
            {
                _trainLoader.AddSkipColumn(c);
            }
        }

        /// <summary>
        /// Задание параметров random forest
        /// </summary>
        /// <param name="nbatches">общее количество классификаторов</param>
        /// <param name="r">доля множества для посторения дерева</param>
        /// <param name="nclasses">количентсво классов</param>
        /// <param name="treesbatch">количентсво деревьев на один классификатор</param>
        public void SetRFParams(int nbatches, double r, int nclasses, int treesbatch)
        {
            _nbatches = nbatches;
            _rfcoeff = r;
            _nclasses = nclasses;
            _treesbatch = treesbatch;
        }

        /// <summary>
        /// Reads data from train and test files
        /// <param name="trainPath">train file path</param>
        /// <param name="testPath">test file path</param>
        /// <param name="target">target variable name</param>
        /// </summary>
        public void LoadData(string trainPath, string testPath, string ids, string target)
        {
            _trainPath = trainPath;
            _testPath = testPath;
            _target = target;

            _trainLoader = _target != null ? new DataLoader<FType>(_target) : new DataLoader<FType>();
            _testLoader = _target != null ? new DataLoader<FType>(_target) : new DataLoader<FType>();

            if (!File.Exists(_trainPath))
            {
                Logger.Log("train file " + _trainPath + " not found");
                throw new FileNotFoundException("", _trainPath);
            }

            if (!File.Exists(_trainPath))
            {
                Logger.Log("test file " + _testPath + " not found");
                throw new FileNotFoundException("", _testPath);
            }

            // loading train file
            _trainLoader.IsLoadForLearning = true;
            _trainLoader.AddIdsString(ids);
            _trainLoader.Load(_trainPath);

            foreach (var key in _trainLoader.TargetProb.Keys)
                Logger.Log("prob[" + key.ToString("F0") + "] = " + _trainLoader.TargetProb[key].ToString("F06"));

            // loading test file
            foreach (var id in _trainLoader.Ids.Keys) // the same id's
                _testLoader.AddIdColumn(id);

            foreach (var col in _trainLoader.SkippedColumns.Keys) // the same columns
                _testLoader.AddSkipColumn(col);

            // loading test file
            _testLoader.Load(_testPath);

            ModifyTestData();
        }


        /// <summary>
        /// build and test classifier
        /// </summary>
        public ClassifierResult Build(bool savetrees=false, bool boost=false)
        {
            Clear();
            var ret = new ClassifierResult();
            
            int rowsCnt = _trainLoader.TotalDataLines;
            int varsCnt = _trainLoader.NVars;

            for (int i = 0; i < _nbatches; i++)
            {
                // создаем классификатор
                var cls = CreateBatch();
                if (savetrees)
                {
                    // сохраняем
                    SerializeTree(cls, i);
                    _treesDict.Add(i, cls);
                }

                var trainDiffs = new Dictionary<int, double>();
                double sumErr = 0;
                for (int k = 0; k < rowsCnt; k++)
                {
                    var crow = new double[varsCnt];
                    for (int l = 0; l < varsCnt; l++)
                    {
                        crow[l] = _trainLoader.LearnRows[k, l];
                    }
                    var cres = PredictProba(crow);
                    double targ = _trainLoader.LearnRows[k, varsCnt];
                    double diff = Math.Abs(cres[1] - targ);
                    sumErr += diff;
                    trainDiffs.Add(k, diff);
                }

                Logger.Log("summary error = " + sumErr);

                // расчитываем метрики для тестового множества
                var clsRes = GetTestSetMetrics(cls, i + 1);
                ret.AddStepResult(clsRes, i);
                Logger.Log("n=" + varsCnt + " d=" + _rfcoeff + " batch=" + (i + 1) + " ok; AUC=" + clsRes.AUC.ToString("F04"));
            }

            return ret;
        }

        /// <summary>
        /// Predict probability for one instance
        /// </summary>
        /// <param name="sarr">array of double params</param>
        /// <returns></returns>
        public double[] PredictProba(double[] sarr, bool devide=true)
        {
            var y = new double[_nclasses];
            int cnt = _treesDict.Keys.Count();

            foreach (var id in _treesDict.Keys)
            {
                var tree = _treesDict[id];
                var sy = new double[_nclasses];
                alglib.dfprocess(tree.AlglibForest, sarr, ref sy);
                for (int i = 0; i < sy.Length; i++)
                    y[i] += sy[i];
            }

            if (devide)
            {
                for (int i = 0; i < y.Length; i++)
                    y[i] /= cnt;
            }

            return y;
        }

        private void ModifyTestData()
        {
            _testDataDict = new Dictionary<string, List<double[]>>(); // тестовые данные: id -> список строк на данный id
            _resultDict = new Dictionary<string, int>(); // результат тестовых данных: id -> target

            // модифицируем тестовые данные
            foreach (var row in _testLoader.Rows)
            {
                // сохраняем результат
                if (!_resultDict.ContainsKey(row.Id))
                    _resultDict.Add(row.Id, Convert.ToInt32(row.Target));

                // сохраняем даные расчета
                var txy = new double[_testLoader.NVars];
                for (int k = 0; k < _testLoader.NVars; k++)
                {
                    txy[k] = row.Coeffs[k];
                }
                if (!_testDataDict.ContainsKey(row.Id))
                    _testDataDict.Add(row.Id, new List<double[]>());
                _testDataDict[row.Id].Add(txy);
            }
        }

        /// <summary>
        /// Расчет метрик качества классификации на тестовом множестве для очередного классификатора
        /// </summary>
        /// <param name="cnt"></param>
        /// <param name="cls"></param>
        /// <returns></returns>
        private FinalFuncResult GetTestSetMetrics(DecisionForest cls, double cnt)
        {
            var rlist = new RocItem[_resultDict.Count]; // массив для оценки результата

            // получаем результат по одному классификатору
            var result = GetTestClassificationResult(cls);

            // сохраняем общую сумму вероятностей по идентификатору
            // т.е. добавляем результат от очередного классификатора
            foreach (string id in result.Keys)
            {
                if (!_testProbSum.ContainsKey(id))
                    _testProbSum.Add(id, 0);

                _testProbSum[id] += result[id];
            }

            // находим cреднее вероятностей по идентификатору
            foreach (string id in result.Keys)
            {
                if (!_testProbAvg.ContainsKey(id))
                    _testProbAvg.Add(id, 0);

                _testProbAvg[id] = _testProbSum[id] / cnt;
            }

            // находим статистики классификации
            int idx = 0;
            foreach (string id in result.Keys)
            {
                if (rlist[idx] == null) rlist[idx] = new RocItem();

                rlist[idx].Prob = _testProbAvg[id]; // среднее по наблюдениям
                rlist[idx].Target = _resultDict[id];
                //rlist[idx].Predicted = _testProbAvg[id] > _trainLoader.TargetProb[1] ? 1 : 0;
                rlist[idx].Predicted = _testProbAvg[id] > 0.5 ? 1 : 0;

                idx++;
            }
            Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
            var clsRes = ResultCalc.GetResult(rlist, 0.05);
            return clsRes;
        }

        /// <summary>
        /// Расчет классификации по тестовому множеству на одном классификаторе
        /// ипользуется в GetTestSetMetrics
        /// </summary>
        /// <param name="cls">current classifier</param>
        /// <returns></returns>
        private Dictionary<string, double> GetTestClassificationResult(DecisionForest cls)
        {
            var probDictList = new Dictionary<string, Dictionary<int, double>>();

            // пробегаем по всем клиентски данным и сохраняем результат
            foreach (string id in _testDataDict.Keys)
            {
                if (!probDictList.ContainsKey(id))
                    probDictList.Add(id, new Dictionary<int, double>());

                foreach (var sarr in _testDataDict[id])
                {
                    var y = new double[_nclasses];
                    alglib.dfprocess(cls.AlglibForest, sarr, ref y);

                    double prob = y[1];
                    int kmax = probDictList[id].Keys.Count == 0 ? 0 : probDictList[id].Keys.Max() + 1;
                    probDictList[id].Add(kmax, prob);
                }
            }

            // вероятность определяется как среднее по записям для клиента
            var probDict = new Dictionary<string, double>();
            foreach (var id in probDictList.Keys)
            {
                int cnt = probDictList[id].Keys.Count();
                double prob = 0;
                foreach (var d in probDictList[id].Keys)
                {
                    prob += probDictList[id][d];
                }

                if (!probDict.ContainsKey(id))
                    probDict.Add(id, prob / cnt);
            }

            return probDict;
        }

        /// <summary>
        /// Creates one classifier (batch of trees)
        /// </summary>
        /// <returns></returns>
        private DecisionForest CreateBatch()
        {
            var tree = new DecisionForest();
            alglib.decisionforest df;

            int npoints = _trainLoader.TotalDataLines;
            int nvars = _trainLoader.NVars;
            int info;
            double[,] xy = _trainLoader.LearnRows;

            alglib.dfreport rep;
            alglib.dfbuildrandomdecisionforest(xy, npoints, nvars, _nclasses, _treesbatch, _rfcoeff, out info, out df, out rep);

            tree.AlglibForest = df;
            xy = null;

            return tree;
        }

        private void SerializeTree(DecisionForest tree, int i)
        {
            string treesDir = Environment.CurrentDirectory + "\\trees";
            if (!Directory.Exists(treesDir))
                Directory.CreateDirectory(treesDir);
            var dinfo = new DirectoryInfo(treesDir);

            var fs = new FileStream(dinfo.FullName + "\\" + "tree_" + string.Format("{0:0000.#}", i) + ".dmp", FileMode.Create, FileAccess.Write);

            var formatter = new BinaryFormatter();
            try
            {
                formatter.Serialize(fs, tree);
            }
            catch (SerializationException e)
            {
                Logger.Log(e);
            }
            finally
            {
                fs.Close();
            }
        }

        private DecisionForest DeserializeTree(string path)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var formatter = new BinaryFormatter();
            DecisionForest tree = null;

            try
            {
                tree = (DecisionForest)formatter.Deserialize(fs);
            }
            catch (SerializationException e)
            {
                Logger.Log(e);
            }
            finally
            {
                fs.Close();
            }

            return tree;
        }

        /// <summary>
        /// Load trees from dump files
        /// </summary>
        /// <param name="root">diretory with trees</param>
        /// <param name="cnt">trees count in bucket</param>
        /// <param name="bucket">number of bucket</param>
        /// <returns>loaded trees count</returns>
        public int LoadTrees(string root, int cnt=0, int bucket=0)
        {
            string treesDir = root == null ? (Environment.CurrentDirectory + "\\trees") : root;
            if (!Directory.Exists(treesDir))
            {
                Logger.Log("directory " + root + " doesn't exists");
                return 0;
            }
            var dinfo = new DirectoryInfo(treesDir);
            _treesDict.Clear();

            int idx = 0;
            var files = dinfo.GetFiles().OrderBy(f => f.Name).ToArray();
            if (cnt > 0)
            {
                files = files.Skip(cnt * bucket).Take(cnt).ToArray();
            }
            foreach (var finfo in files)
            {
                var tree = DeserializeTree(finfo.FullName);
                _treesDict.Add(idx++, tree);
                Logger.Log(finfo.Name + " loaded;");
            }
            Logger.Log("all trees loaded;");
            return idx;
        }

        public void Clear()
        {
            _treesDict.Clear();
        }

        public ClassifierResult Build()
        {
            return Build(false, false);
        }

        public FType[] PredictProba(FType[] sarr)
        {
            return PredictProba(sarr, true);
        }
    }
}
