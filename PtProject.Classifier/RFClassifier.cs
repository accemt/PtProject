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
        /// Тестовые данные
        /// </summary>
        private Dictionary<string, FType[]> _testDataDict;

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
        private int _nb = 0;
        private int _treesbatch = 1;
        private int _nclasses = 2;
        private int[] _indexes = null;

        private SortedDictionary<int, DecisionForest> _forestDict = new SortedDictionary<int, DecisionForest>();

        
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

            // создаем первый классификатор
            _nb = 1;
            var cls = CreateForest();
            if (savetrees)
            {
                // сохраняем
                SerializeTree(cls, 0);
                _forestDict.Add(0, cls);
            }

            // расчитываем метрики для тестового множества
            var clsRes = GetTestSetMetrics(cls);
            ret.AddStepResult(clsRes, 0);
            Logger.Log("n=" + varsCnt + " d=" + _rfcoeff + " batch=" + (0 + 1) + " ok; AUC=" + clsRes.AUC.ToString("F04"));

            for (int i = 1; i < _nbatches; i++)
            {
                _nb = i + 1; // batch num

                // перестраиваем индексы плохо классифицированных объектов (плохие сначала)
                RefreshIndexes(rowsCnt, varsCnt);

                // строим классификаторы и выбираем лучший
                DecisionForest maxForest = null;
                double maxMetric = 0;
                for (int k = 0; k < 10; k++)
                {
                    var scls = CreateForest(true);
                    // расчитываем метрики для тестового множества
                    var sres = GetTestSetMetrics(scls, true);
                    Logger.Log("sub cls #" + k + " auc=" + sres.AUC.ToString("F04"));

                    if (sres.AUC > maxMetric)
                    {
                        maxMetric = sres.AUC;
                        maxForest = scls;
                    }
                }

                maxForest.Coeff = GetBestMetricsCoeff(maxForest);

                var nClsRes = GetTestSetMetrics(maxForest);
                if (savetrees)
                {
                    // сохраняем
                    SerializeTree(maxForest, i);
                    _forestDict.Add(i, maxForest);
                }
                ret.AddStepResult(nClsRes, i);
                Logger.Log("n=" + varsCnt + " d=" + _rfcoeff + " batch=" + (i + 1) + " ok; AUC=" + nClsRes.AUC.ToString("F04"));
            }

            return ret;
        }

        private void RefreshIndexes(int rowsCnt, int varsCnt)
        {
            // находим разницы между реальными значениями и прогнозными в train-set
            var trainDiffs = new Dictionary<int, double>();
            double sumErr = 0;
            var rlist = new RocItem[rowsCnt]; // массив для оценки результата

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

                rlist[k] = new RocItem();
                rlist[k].Prob = cres[1];
                rlist[k].Target = (int)targ;
                rlist[k].Predicted = cres[1] > 0.5 ? 1 : 0;
            }

            Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
            var clres = ResultCalc.GetResult(rlist, 0.05);

            Logger.Log("cl auc=" + clres.AUC.ToString("F06") + " loss=" + clres.LogLoss.ToString("F06"));

            // сосавляем массив индексов (сначала - плохо классифицированные)
            var sarr = trainDiffs.OrderByDescending(t => t.Value).ToArray();
            _indexes = new int[rowsCnt];
            for (int k = 0; k < rowsCnt; k++)
            {
                _indexes[k] = sarr[k].Key;
            }
        }

        private FType GetBestMetricsCoeff(DecisionForest maxForest)
        {
            double best = 0;
            double fmax = 0;
            var clsData = GetTestClassificationResult(maxForest);

            for (double d = 0.001; d < 1; d += 0.001)
            {
                var rlist = new RocItem[_resultDict.Count]; // массив для оценки результата

                int idx = 0;
                foreach (string id in clsData.Keys)
                {
                    if (rlist[idx] == null) rlist[idx] = new RocItem();

                    rlist[idx].Prob = _testProbAvg[id] * (1 - d) + clsData[id] * d;
                    rlist[idx].Target = _resultDict[id];
                    rlist[idx].Predicted = clsData[id] > 0.5 ? 1 : 0;

                    idx++;
                }
                Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
                var cres = ResultCalc.GetResult(rlist, 0.05);

                if (cres.AUC > fmax)
                {
                    fmax = cres.AUC;
                    best = d;
                }
            }

            Logger.Log("dc=" + best.ToString("F03") + " auc=" + fmax.ToString("F04"));

            return best;
        }

        /// <summary>
        /// Predict probability for one instance
        /// </summary>
        /// <param name="sarr">array of double params</param>
        /// <returns></returns>
        public double[] PredictProba(double[] sarr, bool devide=true)
        {
            var y = new double[_nclasses];
            int cnt = _forestDict.Keys.Count();
            bool isboost = false;

            foreach (var id in _forestDict.Keys)
            {
                var forest = _forestDict[id];
                var sy = PredictProba(forest, sarr);

                if (forest.Coeff != null)
                {
                    if (!isboost) isboost = true;
                    for (int i = 0; i < sy.Length; i++)
                        y[i] = y[i] * (1 - forest.Coeff.Value) + sy[i] * forest.Coeff.Value;
                }
                else
                {
                    for (int i = 0; i < sy.Length; i++)
                        y[i] += sy[i];
                }
            }

            if (devide && !isboost)
            {
                for (int i = 0; i < y.Length; i++)
                    y[i] /= cnt;
            }

            return y;
        }

        public double[] PredictProba(DecisionForest forest, double[] sarr)
        {
            var sy = new double[_nclasses];
            alglib.dfprocess(forest.Forest, sarr, ref sy);

            return sy;
        }

        private void ModifyTestData()
        {
            _testDataDict = new Dictionary<string, FType[]>(); // тестовые данные: id -> список строк на данный id
            _resultDict = new Dictionary<string, int>(); // результат тестовых данных: id -> target

            // модифицируем тестовые данные
            foreach (var row in _testLoader.Rows)
            {
                // сохраняем результат
                if (!_resultDict.ContainsKey(row.Id))
                    _resultDict.Add(row.Id, Convert.ToInt32(row.Target));

                // сохраняем даные для расчета
                _testDataDict.Add(row.Id, row.Coeffs);
            }
        }

        /// <summary>
        /// Полный расчет метрик качества классификации на тестовом множестве
        /// для очередного классификатора
        /// </summary>
        /// <param name="batchnum"></param>
        /// <param name="cls"></param>
        /// <param name="accumulate"></param>
        /// <returns></returns>
        private FinalFuncResult GetTestSetMetrics(DecisionForest cls, bool isolated=false)
        {
            var rlist = new RocItem[_resultDict.Count]; // массив для оценки результата

            Dictionary<string, double> testProbSum = _testProbSum;
            Dictionary<string, double> testProbAvg = _testProbAvg;

            if (isolated)
            {
                testProbSum = new Dictionary<string, FType>();
                testProbAvg = new Dictionary<string, FType>();
            }

            // получаем результат по одному классификатору
            var result = GetTestClassificationResult(cls);

            // сохраняем общую сумму вероятностей по идентификатору
            // т.е. добавляем результат от очередного классификатора
            foreach (string id in result.Keys)
            {
                if (!testProbSum.ContainsKey(id))
                    testProbSum.Add(id, 0);

                if (cls.Coeff == null)
                    testProbSum[id] += result[id];
                else
                    testProbSum[id] = testProbSum[id] * (1 - cls.Coeff.Value) + result[id] * cls.Coeff.Value;
            }


            if (cls.Coeff == null)
            {
                // не-boost метод, надо делить
                foreach (var id in _testProbSum.Keys)
                {
                    if (!testProbAvg.ContainsKey(id))
                        testProbAvg.Add(id, 0);
                    testProbAvg[id] = testProbSum[id] / (isolated ? 1 : _nb);
                }
            }
            else
            {
                // не делим, работает коэффициент
                foreach (var id in _testProbSum.Keys)
                {
                    if (!testProbAvg.ContainsKey(id))
                        testProbAvg.Add(id, 0);
                    testProbAvg[id] = testProbSum[id];
                }
            }

            // находим статистики классификации
            int idx = 0;
            foreach (string id in result.Keys)
            {
                if (rlist[idx] == null) rlist[idx] = new RocItem();

                rlist[idx].Prob = testProbAvg[id]; // среднее по наблюдениям
                rlist[idx].Target = _resultDict[id];
                //rlist[idx].Predicted = testProbAvg[id] > _trainLoader.TargetProb[1] ? 1 : 0;
                rlist[idx].Predicted = testProbAvg[id] > 0.5 ? 1 : 0;

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
        /// <param name="cls"></param>
        /// <returns>вероятности целевой</returns>
        private Dictionary<string, double> GetTestClassificationResult(DecisionForest cls)
        {
            var probDict = new Dictionary<string, double>();

            // пробегаем по всем клиентски данным и сохраняем результат
            foreach (string id in _testDataDict.Keys)
            {
                var y = PredictProba(cls, _testDataDict[id]);
                if (!probDict.ContainsKey(id))
                    probDict.Add(id, y[1]);
            }

            return probDict;
        }

        /// <summary>
        /// Creates one classifier (batch of trees)
        /// </summary>
        /// <returns></returns>
        private DecisionForest CreateForest(bool useidx=false)
        {
            var tree = new DecisionForest();
            alglib.decisionforest df;

            int npoints = _trainLoader.TotalDataLines;
            int nvars = _trainLoader.NVars;
            int info;
            double coeff = _rfcoeff;
            double[,] xy = _trainLoader.LearnRows;

            if (useidx)
            {
                coeff = 1;
                npoints = (int)(npoints * _rfcoeff);
                double[,] nxy = new double[npoints, nvars + 1];
                for (int i = 0; i < npoints; i++)
                {
                    int sn = (int)(Domain.Util.RandomGen.GetTrangle() * npoints);
                    int sidx = _indexes[sn];
                    for (int j = 0; j < nvars + 1; j++)
                    {
                        nxy[i, j] = xy[sidx, j];
                    }
                }
                xy = nxy;
            }

            alglib.dfreport rep;
            alglib.dfbuildrandomdecisionforest(xy, npoints, nvars, _nclasses, _treesbatch, coeff, out info, out df, out rep);

            tree.Forest = df;
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
            _forestDict.Clear();

            int idx = 0;
            var files = dinfo.GetFiles().OrderBy(f => f.Name).ToArray();
            if (cnt > 0)
            {
                files = files.Skip(cnt * bucket).Take(cnt).ToArray();
            }
            foreach (var finfo in files)
            {
                var tree = DeserializeTree(finfo.FullName);
                _forestDict.Add(idx++, tree);
                Logger.Log(finfo.Name + " loaded;");
            }
            Logger.Log("all trees loaded;");
            return idx;
        }

        public void Clear()
        {
            _forestDict.Clear();
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
