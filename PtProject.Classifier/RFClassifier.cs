using PtProject.Loader;
using PtProject.Domain.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PtProject.Domain;
using System.Globalization;

using FType = System.Double;

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
        private int[] _indexes = null;
        private Dictionary<string,double> _errors = new Dictionary<string, FType>();

        public double RFCoeff = 0.05;
        public int TotalBatches = 100;
        public int TreesInBatch = 1;
        public int BatchesInBruteForce = 1;
        public int BatchesInFirstStep = 1;
        public bool LoadFirstStepBatches = false;
        public double OutliersPrct = 0;
        public string BruteMeasure = "train";
        public string SkipColumns = "";
        public string IndexSortOrder = "none";

        private SortedDictionary<int, DecisionBatch> _classifiers = new SortedDictionary<int, DecisionBatch>();

        
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

        public RFClassifier(int nbatches, double r, int nclasses)
        {
            LoadDefaultParams();

            TotalBatches = nbatches;
            RFCoeff = r;
            _nclasses = nclasses;
        }

        public RFClassifier()
        {
            LoadDefaultParams();
        }

        /// <summary>
        /// Задание параметров random forest
        /// </summary>
        /// <param name="nbatches">общее количество классификаторов</param>
        /// <param name="r">доля множества для посторения дерева</param>
        /// <param name="nclasses">количентсво классов (пока реализовано для 2)</param>
        public void LoadDefaultParams()
        {
            string so = ConfigReader.Read("IndexSort");
            if (so != null) IndexSortOrder = ConfigReader.Read("IndexSort");

            string tbf = ConfigReader.Read("BatchesInBruteForce");
            if (tbf != null) BatchesInBruteForce = int.Parse(tbf);

            string tib = ConfigReader.Read("TreesInBatch");
            if (tib != null) TreesInBatch = int.Parse(tib);

            string tifs = ConfigReader.Read("BatchesInFirstStep");
            if (tifs != null) BatchesInFirstStep = int.Parse(tifs);

            string lfsb = ConfigReader.Read("LoadFirstStepBatches");
            if (lfsb != null) LoadFirstStepBatches = bool.Parse(lfsb);

            string op = ConfigReader.Read("OutliersPrct");
            if (op != null) OutliersPrct = double.Parse(op.Replace(',', '.'), CultureInfo.InvariantCulture);

            string bm = ConfigReader.Read("BruteMeasure");
            if (bm != null) BruteMeasure = bm;

            string sc = ConfigReader.Read("SkipColumns");
            if (sc != null) SkipColumns = sc;

            string tb = ConfigReader.Read("TotalBatches");
            if (tb != null) TotalBatches = int.Parse(tb);

            string rfc = ConfigReader.Read("RFCoeff");
            if (rfc != null) RFCoeff = double.Parse(rfc.Replace(',', '.'), CultureInfo.InvariantCulture);
        }

        public void PrintParams()
        {
            Logger.Log("RFCoeff: " + RFCoeff);
            Logger.Log("TotalBatches: " + TotalBatches);
            Logger.Log("BatchesInFirstStep: " + BatchesInFirstStep);
            Logger.Log("BatchesInBruteForce: " + BatchesInBruteForce);
            Logger.Log("TreesInBatch: " + TreesInBatch);
            Logger.Log("LoadFirstStepBatches: " + LoadFirstStepBatches);
            Logger.Log("indexes sort order: " + IndexSortOrder);
            Logger.Log("OutliersPrct: " + OutliersPrct);
            Logger.Log("BruteMeasure: " + BruteMeasure);
            Logger.Log("SkipColumns: " + SkipColumns);
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
            _trainLoader.AddSkipColumns(SkipColumns);
            _trainLoader.Load(_trainPath);

            foreach (var key in _trainLoader.TargetProb.Keys)
                Logger.Log("prob[" + key.ToString("F0") + "] = " + _trainLoader.TargetProb[key].ToString("F06"));

            Logger.Log("Outliers to drop: " + (int)(_trainLoader.TotalDataLines * OutliersPrct));

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
            if (_trainLoader == null || _trainLoader.LearnRows == null)
                throw new InvalidOperationException("train set is empty");

            Clear();
            var ret = new ClassifierResult();

            using (var sw = new StreamWriter(new FileStream("auchist.csv", FileMode.Create, FileAccess.Write)))
            {
                sw.WriteLine("time;n;train auc;test auc;stype");

                // создаем первые классификаторы
                for (int i = 0; i < BatchesInFirstStep; i++)
                {
                    DecisionBatch cls = null;
                    if (LoadFirstStepBatches)
                    {
                        cls = DecisionBatch.Load(Environment.CurrentDirectory + "\\batches\\" + "batch_" + string.Format("{0:0000.#}", i) + ".dmp");
                        if (cls == null)
                        {
                            cls = CreateClassifier(useidx: false, parallel: true);
                            if (savetrees) cls.Save();
                        }
                    }
                    else
                    {
                        cls = CreateClassifier(useidx: false, parallel: true);
                        if (savetrees) cls.Save();
                    }

                    // расчитываем метрики для тестового и обучающего множества (накопленные)
                    var testRes = GetTestMetricsAccumulated(cls);
                    var trainRes = GetTrainMetricsAccumulated(cls);

                    Logger.Log("batch=" + i + " ok; test AUC=" + testRes.AUC.ToString("F10") + "; train AUC=" + trainRes.AUC.ToString("F10"));
                    sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ";" + i + ";" + trainRes.AUC + ";" + testRes.AUC + ";none");
                    sw.Flush();

                    ret.AddStepResult(testRes, i);
                }

                // далее создаем классификаторы с учетом ошибки предыдущих
                for (int i = BatchesInFirstStep; i < TotalBatches; i++)
                {
                    DecisionBatch bestForest = null;

                    if (boost)
                    {
                        // перестраиваем индексы плохо классифицированных объектов (плохие сначала)
                        RefreshIndexes();

                        double bestMetric = 1000000;
                        int bestk = 0;

                        // строим классификаторы и выбираем лучший
                        for (int k = 0; k < BatchesInBruteForce; k++)
                        {
                            var scls = CreateClassifier(useidx: true, parallel: true);

                            // расчитываем метрики для тестового множества
                            var trainCntRes = GetTrainClassificationCounts(scls);
                            var testCntRes = GetTestClassificationCounts(scls);
                            int cnt = scls.CountTreesInBatch;

                            var rlist = new RocItem[_trainResult.Count]; // массив для оценки результата
                                                                         // находим статистики классификации
                            int idx = 0;
                            double epsilon = 0.0;
                            foreach (string id in _trainResult.Keys)
                            {
                                if (rlist[idx] == null) rlist[idx] = new RocItem();

                                rlist[idx].Prob = (BruteMeasure == "train" ? trainCntRes[id] : testCntRes[id]) / cnt; // среднее по наблюдениям
                                rlist[idx].Target = _trainResult[id];
                                rlist[idx].Predicted = rlist[idx].Prob > 0.5 ? 1 : 0;

                                epsilon += Math.Abs(rlist[idx].Prob - rlist[idx].Target) * _errors[id];

                                idx++;
                            }

                            Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
                            var clsRes = ResultCalc.GetResult(rlist, 0.05);

                            epsilon *= (1 - clsRes.AUC);

                            Logger.Log("sub cls #" + k + " auc=" + clsRes.AUC.ToString("F10") + " eps=" + epsilon + (epsilon < bestMetric ? " [best]" : ""));

                            if (epsilon < bestMetric)
                            {
                                bestMetric = epsilon;
                                bestForest = scls;
                                bestk = k;
                            }
                        }
                    }
                    else
                    {
                        bestForest = CreateClassifier(useidx: false, parallel: true);
                    }


                    var testRes = GetTestMetricsAccumulated(bestForest);
                    var trainRes = GetTrainMetricsAccumulated(bestForest);
                    if (savetrees) bestForest.Save();

                    ret.AddStepResult(testRes, i);
                    Logger.Log("batch=" + i + " ok; test AUC=" + testRes.AUC.ToString("F10") + "; train AUC=" + trainRes.AUC.ToString("F10"));
                    sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ";" + i + ";" + trainRes.AUC + ";" + testRes.AUC + ";" + IndexSortOrder);
                    sw.Flush();
                }

                sw.Close();
            }
            
            return ret;
        }


        /// <summary>
        /// Полный расчет метрик качества классификации на тестовом множестве
        /// c учетом очередного классификатора
        /// </summary>
        /// <returns>Результат классификации</returns>
        private FinalFuncResult GetTestMetricsAccumulated(DecisionBatch cls)
        {
            _nbTest += cls.CountTreesInBatch; // обновляем общее кол-во деревьев
            return GetMetricsAccumulated(cls, _testProbSum, _testProbAvg, _testResult, _nbTest, GetTestClassificationCounts);
        }

        /// <summary>
        /// Полный расчет метрик качества классификации на тестовом множестве
        /// c учетом очередного классификатора
        /// </summary>
        /// <returns>Результат классификации</returns>
        private FinalFuncResult GetTrainMetricsAccumulated(DecisionBatch cls)
        {
            _nbTrain += cls.CountTreesInBatch; // обновляем общее кол-во деревьев
            return GetMetricsAccumulated(cls, _trainProbSum, _trainProbAvg, _trainResult, _nbTrain, GetTrainClassificationCounts);
        }

        private FinalFuncResult GetMetricsAccumulated(DecisionBatch cls,
                Dictionary<string, double> probSum,
                Dictionary<string, double> probAvg,
                Dictionary<string, int> resultDict,
                int nbcount,
                Func<DecisionBatch, Dictionary<string, double>> GetResult
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
        private Dictionary<string, double> GetTestClassificationCounts(DecisionBatch cls)
        {
            var probDict = new Dictionary<string, double>();

            // пробегаем по всем клиентски данным и сохраняем результат
            foreach (string id in _testDataDict.Keys)
            {
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
        private Dictionary<string, double> GetTrainClassificationCounts(DecisionBatch cls)
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
                cnt += _classifiers[id].CountTreesInBatch;

            for (int i = 0; i < y.Length; i++)
                y[i] /= cnt;
            
            return y;
        }

        public double[] PredictProba(double[] sarr, double[] coeffs)
        {
            var y = new double[_nclasses];
            int cnt = _classifiers.Keys.Count();

            int cnum = 0;
            foreach (var id in _classifiers.Keys)
            {
                var cls = _classifiers[id];
                var sy = cls.PredictProba(sarr);

                for (int i = 0; i < sy.Length; i++)
                    y[i] += sy[i]*coeffs[cnum];

                cnum++;
            }

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
        private DecisionBatch CreateClassifier(bool useidx=false, bool parallel=false)
        {
            int npoints = _trainLoader.TotalDataLines;
            int nvars = _trainLoader.NVars;
            double coeff = RFCoeff;
            double[,] xy = _trainLoader.LearnRows;
            var classifier = new DecisionBatch();


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
            _errors = new Dictionary<string, double>();
            var rlist = new RocItem[rowsCnt]; // массив для оценки результата

            double sumdiff = 0;
            int i = 0;
            foreach (var k in _trainResult.Keys)
            {
                double tprob = _trainProbAvg[k];
                int targ = _trainResult[k];
                double diff = Math.Abs(tprob - targ);
                sumdiff += diff;

                _errors.Add(k, diff);

                rlist[i] = new RocItem();
                rlist[i].Prob = tprob;
                rlist[i].Target = targ;
                rlist[i].Predicted = tprob > 0.5 ? 1 : 0;

                i++;
            }

            Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
            var clres = ResultCalc.GetResult(rlist, 0.05);

            Logger.Log("cl auc=" + clres.AUC.ToString("F10") + "; loss=" + clres.LogLoss.ToString("F10") + "; sumdiff=" + sumdiff);

            // сортируем индексы
            KeyValuePair<string, double>[] sarr = null;
            if (IndexSortOrder==null || (IndexSortOrder.ToLower()!="desc" && IndexSortOrder.ToLower()!="asc"))
            {
                sarr = new KeyValuePair<string, double>[rowsCnt];
                for (int l=0;l<sarr.Length;l++)
                    sarr[l] = new KeyValuePair<string, double>(l.ToString(), l);
            }
            else
            {
                if (IndexSortOrder.ToLower() == "desc")
                {
                    sarr = _errors.OrderByDescending(t => t.Value).ToArray();

                    int outliersCnt = (int)(sarr.Length * OutliersPrct);
                    for (int s = 0; s < outliersCnt; s++)
                        sarr[s] = new KeyValuePair<string, FType>(sarr[s].Key, -1);

                    sarr = sarr.OrderByDescending(t => t.Value).ToArray();
                }
                if (IndexSortOrder.ToLower() == "asc")
                    sarr = _errors.OrderBy(t => t.Value).ToArray();
            }

            _indexes = new int[rowsCnt];
            i = 0;
            foreach (var kvp in sarr)
                _indexes[i++] = Convert.ToInt32(double.Parse(kvp.Key));
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
            string treesDir = root == null ? (Environment.CurrentDirectory + "\\batches") : root;
            if (!Directory.Exists(treesDir))
            {
                Logger.Log("directory " + root + " doesn't exists");
                return 0;
            }
            var dinfo = new DirectoryInfo(treesDir);
            _classifiers.Clear();

            int idx = 0;
            var files = dinfo.GetFiles("*.dmp").OrderBy(f => f.Name).ToArray();
            if (cnt > 0)
            {
                files = files.Skip(cnt * bucket).Take(cnt).ToArray();
            }
            foreach (var finfo in files)
            {
                var cls = DecisionBatch.Load(finfo.FullName);
                _classifiers.Add(idx++, cls);
                Logger.Log(finfo.Name + " loaded;");
            }
            Logger.Log("all trees loaded;");
            return idx;
        }

        /// <summary>
        /// Get all trees in all batches count
        /// </summary>
        public int CountAllTrees
        {
            get { return _classifiers.Sum(c => c.Value.CountTreesInBatch); }
        }

        /// <summary>
        /// Count all batches in classifier
        /// </summary>
        public int CountAllBatches
        {
            get { return _classifiers.Count; }
        }


        private void ModifyData()
        {
            _testDataDict = new Dictionary<string, FType[]>(); // тестовые данные: id -> список строк на данный id
            _testResult = new Dictionary<string, int>(); // результат тестовых данных: id -> target
            _trainResult = new Dictionary<string, int>(); // результат обучающих данных: row_number -> target

            if (_trainLoader.LearnRows == null) return;

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
