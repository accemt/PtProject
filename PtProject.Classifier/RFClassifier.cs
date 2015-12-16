using PtProject.Loader;
using PtProject.Domain.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        private string _ids;
        private string _target;
        private int _nbTrain = 0;
        private int _nbTest = 0;
        private int _nclasses = 2;

        private double RFCoeff = 0.05;
        private int Nbatches = 100;
        private int TreesInBatch = 1;
        private int TreesBruteForce = 12;
        private int TreesInFirstStep = 1;
        private int[] _indexes = null;

        private SortedDictionary<int, DecisionForestClassifier> _classifiers = new SortedDictionary<int, DecisionForestClassifier>();

        
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
            Nbatches = nbatches;
            RFCoeff = r;
            _nclasses = nclasses;
            TreesInBatch = treesbatch;
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
            _ids = ids;

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

            // создаем первые классификаторы
            for (int i = 0; i < TreesInFirstStep; i++)
            {
                var cls = CreateClassifier(useidx: false, parallel: true);
                if (savetrees) cls.Serialize();

                // расчитываем метрики для тестового и обучающего множества
                var testRes = GetTestMetricsAccumulated(cls);
                var trainRes = GetTrainMetricsAccumulated(cls);

                Logger.Log("batch=" + i + " ok; test AUC=" + testRes.AUC.ToString("F10") + "; train AUC=" + trainRes.AUC.ToString("F10"));

                ret.AddStepResult(testRes, i);
            }

            // далее создаем классификаторы с учетом ошибки предыдущих
            for (int i = TreesInFirstStep; i < Nbatches; i++)
            {
                DecisionForestClassifier maxForest = null;

                if (boost)
                {
                    // перестраиваем индексы плохо классифицированных объектов (плохие сначала)
                    RefreshIndexes();

                    double maxMetric = 0;
                    // строим классификаторы и выбираем лучший
                    for (int k=0;k< TreesBruteForce;k++)
                    {
                        var scls = CreateClassifier(useidx: true, parallel: true);

                        // расчитываем метрики для тестового множества
                        var sres = GetTrainClassificationCounts(scls);
                        int cnt = scls.CountTrees;

                        var rlist = new RocItem[_trainResult.Count]; // массив для оценки результата
                        // находим статистики классификации
                        int idx = 0;
                        foreach (string id in _trainResult.Keys)
                        {
                            if (rlist[idx] == null) rlist[idx] = new RocItem();

                            rlist[idx].Prob = sres[id] / cnt; // среднее по наблюдениям
                            rlist[idx].Target = _trainResult[id];
                            rlist[idx].Predicted = sres[id] > 0.5 ? 1 : 0;

                            idx++;
                        }
                        Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
                        var clsRes = ResultCalc.GetResult(rlist, 0.05);

                        Logger.Log("sub cls #" + k + " auc=" + clsRes.AUC.ToString("F10"));

                        if (clsRes.AUC > maxMetric)
                        {
                            maxMetric = clsRes.AUC;
                            maxForest = scls;
                        }
                    }
                }
                else
                {
                    maxForest = CreateClassifier(useidx: false, parallel: true);
                }


                var testRes = GetTestMetricsAccumulated(maxForest);
                var trainRes = GetTrainMetricsAccumulated(maxForest);
                if (savetrees) maxForest.Serialize();

                ret.AddStepResult(testRes, i);
                Logger.Log("batch=" + i + " ok; test AUC=" + testRes.AUC.ToString("F10") + "; train AUC=" + trainRes.AUC.ToString("F10"));
            }

            return ret;
        }


        /// <summary>
        /// Полный расчет метрик качества классификации на тестовом множестве
        /// c учетом очередного классификатора
        /// </summary>
        /// <returns>Результат классификации</returns>
        private FinalFuncResult GetTestMetricsAccumulated(DecisionForestClassifier cls)
        {
            _nbTest += cls.CountTrees; // обновляем общее кол-во деревьев
            return GetMetricsAccumulated(cls, _testProbSum, _testProbAvg, _testResult, _nbTest, GetTestClassificationCounts);
        }

        /// <summary>
        /// Полный расчет метрик качества классификации на тестовом множестве
        /// c учетом очередного классификатора
        /// </summary>
        /// <returns>Результат классификации</returns>
        private FinalFuncResult GetTrainMetricsAccumulated(DecisionForestClassifier cls)
        {
            _nbTrain += cls.CountTrees; // обновляем общее кол-во деревьев
            return GetMetricsAccumulated(cls, _trainProbSum, _trainProbAvg, _trainResult, _nbTrain, GetTrainClassificationCounts);
        }

        private FinalFuncResult GetMetricsAccumulated(DecisionForestClassifier cls,
                Dictionary<string, double> probSum,
                Dictionary<string, double> probAvg,
                Dictionary<string, int> resultDict,
                int nbcount,
                Func<DecisionForestClassifier, Dictionary<string, double>> GetResult
            )
        {
            // получаем результат по одному классификатору
            var result = GetResult(cls);

            // сохраняем общую сумму голостов по идентификатору
            // т.е. добавляем результат голосований от очередного классификатора cls
            foreach (string id in result.Keys)
            {
                if (!probSum.ContainsKey(id))
                    probSum.Add(id, 0);

                probSum[id] += result[id];
            }

            // находим вероятности, деля количество голосов на количеств деревьев во всех классификаторах
            foreach (var id in probSum.Keys)
            {
                if (!probAvg.ContainsKey(id))
                    probAvg.Add(id, 0);
                probAvg[id] = probSum[id] / nbcount;
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
        /// <returns>Количество деревьев, проголосовавших за каждый класс</returns>
        private Dictionary<string, double> GetTestClassificationCounts(DecisionForestClassifier cls)
        {
            var probDict = new Dictionary<string, double>();

            // пробегаем по всем клиентски данным и сохраняем результат
            foreach (string id in _testDataDict.Keys)
            {
                //var y = PredictProba(cls, _testDataDict[id]);
                var y = cls.PredictCounts(_testDataDict[id]);
                if (!probDict.ContainsKey(id))
                    probDict.Add(id, y[1]);
            }

            return probDict;
        }

        /// <summary>
        /// Расчет классификации по тестовому обучающему на одном классификаторе
        /// </summary>
        /// <param name="cls"></param>
        /// <returns>Количество деревьев, проголосовавших за каждый класс</returns>
        private Dictionary<string, double> GetTrainClassificationCounts(DecisionForestClassifier cls)
        {
            var probDict = new Dictionary<string, double>();

            int lines = _trainLoader.TotalDataLines;
            int vars = _trainLoader.NVars;

            // пробегаем по всем клиентски данным и сохраняем результат
            for (int i = 0; i < lines; i++)
            {
                var cdata = new double[vars];
                for (int j = 0; j < vars; j++)
                    cdata[j] = _trainLoader.LearnRows[i, j];

                var y = cls.PredictCounts(cdata);
                string id = i.ToString();
                if (!probDict.ContainsKey(id))
                    probDict.Add(id, y[1]);
            }

            return probDict;
        }

        /// <summary>
        /// Predict probability for object
        /// </summary>
        /// <param name="sarr">array of double params</param>
        /// <returns></returns>
        public double[] PredictProba(double[] sarr)
        {
            var y = PredictCounts(sarr);
            int cnt = 0;

            foreach (var id in _classifiers.Keys)
                cnt += _classifiers[id].CountTrees;

            for (int i = 0; i < y.Length; i++)
                y[i] /= cnt;
            
            return y;
        }


        /// <summary>
        /// Get trees counts for each class
        /// </summary>
        /// <param name="sarr">array of double params</param>
        /// <returns></returns>
        public double[] PredictCounts(double[] sarr)
        {
            var y = new double[_nclasses];
            int cnt = _classifiers.Keys.Count();

            foreach (var id in _classifiers.Keys)
            {
                var cls = _classifiers[id];
                var sy = cls.PredictCounts(sarr);

                for (int i = 0; i < sy.Length; i++)
                    y[i] += sy[i];
            }

            return y;
        }


        /// <summary>
        /// Creates one classifier (batch of trees)
        /// </summary>
        /// <returns></returns>
        private DecisionForestClassifier CreateClassifier(bool useidx=false, bool parallel=false)
        {
            int npoints = _trainLoader.TotalDataLines;
            int nvars = _trainLoader.NVars;
            double coeff = RFCoeff;
            double[,] xy = _trainLoader.LearnRows;
            var classifier = new DecisionForestClassifier();


            IEnumerable<int> source = Enumerable.Range(1, TreesInBatch);
            List<DecisionTree> treeList = null;

            if (parallel)
                treeList = (from n in source.AsParallel()
                            select DecisionTree.CreateTree(useidx ? _indexes : null, xy, npoints, nvars, _nclasses, RFCoeff)
                        ).ToList();
            else
                treeList = (from n in source
                            select DecisionTree.CreateTree(useidx ? _indexes : null, xy, npoints, nvars, _nclasses, RFCoeff)
                        ).ToList();

            treeList.ForEach(classifier.AddTree);


            return classifier;
        }

        /// <summary>
        /// Перестройка индексов для определения объектов, которые плохо классифицтрованы
        /// </summary>
        /// <param name="rowsCnt"></param>
        /// <param name="varsCnt"></param>
        private void RefreshIndexes()
        {
            int rowsCnt = _trainResult.Count;

            // находим разницы между реальными значениями и прогнозными в train-set
            var trainDiffs = new Dictionary<string, double>();
            var rlist = new RocItem[rowsCnt]; // массив для оценки результата

            double sumdiff = 0;
            int i = 0;
            foreach (var k in _trainResult.Keys)
            {
                double tprob = _trainProbAvg[k];
                int targ = _trainResult[k];
                double diff = Math.Abs(tprob - targ);
                sumdiff += diff;

                trainDiffs.Add(k, diff);

                rlist[i] = new RocItem();
                rlist[i].Prob = tprob;
                rlist[i].Target = targ;
                rlist[i].Predicted = tprob > 0.5 ? 1 : 0;

                i++;
            }

            Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
            var clres = ResultCalc.GetResult(rlist, 0.05);

            Logger.Log("cl auc=" + clres.AUC.ToString("F10") + "; loss=" + clres.LogLoss.ToString("F10") + "; sumdiff=" + sumdiff);

            // сосавляем массив индексов (сначала - плохо классифицированные)
            var sarr = trainDiffs.OrderByDescending(t => t.Value).ToArray();
            //var sarr = trainDiffs.OrderBy(t => t.Value).ToArray();
            _indexes = new int[rowsCnt];

            i = 0;
            foreach (var kvp in sarr)
            {
                _indexes[i] = int.Parse(kvp.Key);
                //_indexes[i] = i;
                i++;
            }
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
            _classifiers.Clear();

            int idx = 0;
            var files = dinfo.GetFiles().OrderBy(f => f.Name).ToArray();
            if (cnt > 0)
            {
                files = files.Skip(cnt * bucket).Take(cnt).ToArray();
            }
            foreach (var finfo in files)
            {
                var cls = DecisionForestClassifier.Deserialize(finfo.FullName);
                _classifiers.Add(idx++, cls);
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
            _classifiers.Clear();
        }

        public ClassifierResult Build()
        {
            return Build(false, false);
        }
    }
}
