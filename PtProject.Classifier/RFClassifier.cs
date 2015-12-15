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
        private Dictionary<string, int> _testResult;

        /// <summary>
        /// результат обучающих данных: id -> target
        /// </summary>
        private Dictionary<string, int> _trainResult;

        /// <summary>
        /// сюда сохраняем сумму глосований на тестовом множестве
        /// </summary>
        private Dictionary<string, double> _testProbSum = new Dictionary<string, double>(); 

        /// <summary>
        /// сюда сохраняем среднее глосований на тестовом множестве
        /// </summary>
        private Dictionary<string, double> _testProbAvg = new Dictionary<string, double>();

        /// <summary>
        /// сюда сохраняем сумму глосований на обучающем множестве
        /// </summary>
        private Dictionary<string, double> _trainProbSum = new Dictionary<string, double>();

        /// <summary>
        /// сюда сохраняем среднее глосований на обучающем множестве
        /// </summary>
        private Dictionary<string, double> _trainProbAvg = new Dictionary<string, double>();


        private string _trainPath;
        private string _testPath;
        private string _target;
        private double _rfcoeff = 0.05;
        private int _nbatches = 100;
        private int _nb = 0;
        private int _treesbatch = 1;
        private int _treesbrootforce = 10;
        private int _nclasses = 2;
        private int _nfirstcls = 1;
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

            ModifyData();
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

            // создаем первые классификаторы
            for (int i = 0; i < _nfirstcls; i++)
            {
                _nb = i + 1;

                var cls = CreateForest();
                if (savetrees) SerializeTree(cls, 0);

                // расчитываем метрики для тестового и обучающего множества
                var testRes = GetTestMetricsAccumulated(cls);
                var trainRes = GetTrainMetricsAccumulated(cls);

                Logger.Log("batch=" + _nb + " ok; test AUC=" + testRes.AUC.ToString("F08") + "; train AUC=" + trainRes.AUC.ToString("F08"));

                ret.AddStepResult(testRes, 0);
            }

            // далее создаем классификаторы с учетом ошибки предыдущих
            for (int i = _nfirstcls; i < _nbatches; i++)
            {
                _nb = i + 1; // batch num

                DecisionForest maxForest = null;

                if (boost)
                {
                    // перестраиваем индексы плохо классифицированных объектов (плохие сначала)
                    RefreshIndexes(rowsCnt, varsCnt);

                    // строим классификаторы и выбираем лучший
                    
                    double maxMetric = 0;
                    for (int k = 0; k < _treesbrootforce; k++)
                    {
                        var scls = CreateForest(true);
                        // расчитываем метрики для тестового множества
                        var sres = GetTrainClassificationResult(scls);


                        var rlist = new RocItem[_trainResult.Count]; // массив для оценки результата
                                                                   // находим статистики классификации
                        int idx = 0;
                        foreach (string id in _trainResult.Keys)
                        {
                            if (rlist[idx] == null) rlist[idx] = new RocItem();

                            rlist[idx].Prob = sres[id]; // среднее по наблюдениям
                            rlist[idx].Target = _trainResult[id];
                            rlist[idx].Predicted = sres[id] > 0.5 ? 1 : 0;

                            idx++;
                        }
                        Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
                        var clsRes = ResultCalc.GetResult(rlist, 0.05);


                        Logger.Log("sub cls #" + k + " auc=" + clsRes.AUC.ToString("F04"));

                        if (clsRes.AUC > maxMetric)
                        {
                            maxMetric = clsRes.AUC;
                            maxForest = scls;
                        }
                    }
                }
                else
                {
                    maxForest = CreateForest();
                }


                var testRes = GetTestMetricsAccumulated(maxForest);
                var trainRes = GetTrainMetricsAccumulated(maxForest);
                if (savetrees) SerializeTree(maxForest, i);

                ret.AddStepResult(testRes, i);
                Logger.Log("batch=" + _nb + " ok; test AUC=" + testRes.AUC.ToString("F08") + "; train AUC=" + trainRes.AUC.ToString("F08"));
            }

            return ret;
        }


        /// <summary>
        /// Полный расчет метрик качества классификации на тестовом множестве
        /// c учетом очередного классификатора
        /// </summary>
        /// <returns>Результат классификации</returns>
        private FinalFuncResult GetTestMetricsAccumulated(DecisionForest cls)
        {
            return GetMetricsAccumulated(cls, _testProbSum, _testProbAvg, _testResult, GetTestClassificationResult);
        }

        /// <summary>
        /// Полный расчет метрик качества классификации на тестовом множестве
        /// c учетом очередного классификатора
        /// </summary>
        /// <returns>Результат классификации</returns>
        private FinalFuncResult GetTrainMetricsAccumulated(DecisionForest cls)
        {
            return GetMetricsAccumulated(cls, _trainProbSum, _trainProbAvg, _trainResult, GetTrainClassificationResult);
        }

        private FinalFuncResult GetMetricsAccumulated(DecisionForest cls,
                Dictionary<string, double> probSum,
                Dictionary<string, double> probAvg,
                Dictionary<string, int> resultDict,
                Func<DecisionForest, Dictionary<string, double>> GetResult
            )
        {
            // получаем результат по одному классификатору
            var result = GetResult(cls);

            // сохраняем общую сумму вероятностей по идентификатору
            // т.е. добавляем результат от очередного классификатора
            foreach (string id in result.Keys)
            {
                if (!probSum.ContainsKey(id))
                    probSum.Add(id, 0);

                probSum[id] += result[id];
            }

            // не-boost метод, надо делить
            foreach (var id in probSum.Keys)
            {
                if (!probAvg.ContainsKey(id))
                    probAvg.Add(id, 0);
                probAvg[id] = probSum[id] / _nb;
            }

            var rlist = new RocItem[resultDict.Count]; // массив для оценки результата
            // находим статистики классификации
            int idx = 0;
            foreach (string id in result.Keys)
            {
                if (rlist[idx] == null) rlist[idx] = new RocItem();

                rlist[idx].Prob = probAvg[id]; // среднее по наблюдениям
                rlist[idx].Target = resultDict[id];
                rlist[idx].Predicted = probAvg[id] > 0.5 ? 1 : 0;

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
        /// Расчет классификации по тестовому обучающему на одном классификаторе
        /// </summary>
        /// <param name="cls"></param>
        /// <returns></returns>
        private Dictionary<string, double> GetTrainClassificationResult(DecisionForest cls)
        {
            var probDict = new Dictionary<string, double>();

            int lines = _trainLoader.TotalDataLines;
            int vars = _trainLoader.NVars;

            // пробегаем по всем клиентски данным и сохраняем результат
            for (int i = 0; i < lines; i++)
            {
                var cdata = new double[vars];
                for (int j = 0; j < vars; j++)
                {
                    cdata[j] = _trainLoader.LearnRows[i, j];
                }

                var y = PredictProba(cls, cdata);
                string id = i.ToString();
                if (!probDict.ContainsKey(id))
                    probDict.Add(id, y[1]);
            }

            return probDict;
        }

        /// <summary>
        /// Predict probability for one instance
        /// </summary>
        /// <param name="sarr">array of double params</param>
        /// <returns></returns>
        public double[] PredictProba(double[] sarr, bool devide = true)
        {
            var y = new double[_nclasses];
            int cnt = _forestDict.Keys.Count();
            bool isboost = false;

            foreach (var id in _forestDict.Keys)
            {
                var forest = _forestDict[id];
                var sy = PredictProba(forest, sarr);


                for (int i = 0; i < sy.Length; i++)
                    y[i] += sy[i];
            }

            if (devide && !isboost)
            {
                for (int i = 0; i < y.Length; i++)
                    y[i] /= cnt;
            }

            return y;
        }

        /// <summary>
        /// Predict probability by exact classifier
        /// </summary>
        /// <param name="forest">classifier</param>
        /// <param name="sarr">object to classify</param>
        /// <returns></returns>
        public double[] PredictProba(DecisionForest forest, double[] sarr)
        {
            var sy = new double[_nclasses];
            alglib.dfprocess(forest.Forest, sarr, ref sy);

            return sy;
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

        /// <summary>
        /// Перестройка индексов для определения объектов, которые плохо классифицтрованы
        /// </summary>
        /// <param name="rowsCnt"></param>
        /// <param name="varsCnt"></param>
        private void RefreshIndexes(int rowsCnt, int varsCnt)
        {
            // находим разницы между реальными значениями и прогнозными в train-set
            var trainDiffs = new Dictionary<int, double>();
            var rlist = new RocItem[rowsCnt]; // массив для оценки результата

            for (int k = 0; k < rowsCnt; k++)
            {
                string id = k.ToString();
                double tprob = _trainProbAvg[id];
                int targ = _trainResult[id];
                double diff = Math.Abs(tprob - targ);

                trainDiffs.Add(k, diff);

                rlist[k] = new RocItem();
                rlist[k].Prob = tprob;
                rlist[k].Target = targ;
                rlist[k].Predicted = tprob > 0.5 ? 1 : 0;
            }

            Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
            var clres = ResultCalc.GetResult(rlist, 0.05);

            Logger.Log("cl auc=" + clres.AUC.ToString("F08") + " loss=" + clres.LogLoss.ToString("F08"));

            // сосавляем массив индексов (сначала - плохо классифицированные)
            var sarr = trainDiffs.OrderByDescending(t => t.Value).ToArray();
            _indexes = new int[rowsCnt];
            for (int k = 0; k < rowsCnt; k++)
            {
                _indexes[k] = sarr[k].Key;
            }
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

        private void ModifyData()
        {
            _testDataDict = new Dictionary<string, FType[]>(); // тестовые данные: id -> список строк на данный id
            _testResult = new Dictionary<string, int>(); // результат тестовых данных: id -> target
            _trainResult = new Dictionary<string, int>(); // результат обучающих данных: row_number -> target

            // модифицируем тестовые данные
            foreach (var row in _testLoader.Rows)
            {
                // сохраняем результат
                if (!_testResult.ContainsKey(row.Id))
                    _testResult.Add(row.Id, Convert.ToInt32(row.Target));

                // сохраняем даные для расчета
                _testDataDict.Add(row.Id, row.Coeffs);
            }

            for (int i=0;i<_trainLoader.TotalDataLines;i++)
            {
                string id = i.ToString();
                // сохраняем результат
                if (!_trainResult.ContainsKey(id))
                    _trainResult.Add(id, Convert.ToInt32(_trainLoader.LearnRows[i, _trainLoader.NVars]));
            }
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
