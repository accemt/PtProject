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
    public class DecisionForest : AbstractClassifier
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
        private readonly Dictionary<string, double> _testProbSum = new Dictionary<string, double>(); 

        /// <summary>
        /// сюда сохраняем среднее глосований на тестовом множестве
        /// </summary>
        private readonly Dictionary<string, double> _testProbAvg = new Dictionary<string, double>();

        /// <summary>
        /// сюда сохраняем сумму глосований на обучающем множестве
        /// </summary>
        private readonly Dictionary<string, double> _trainProbSum = new Dictionary<string, double>();

        /// <summary>
        /// сюда сохраняем среднее глосований на обучающем множестве
        /// </summary>
        private readonly Dictionary<string, double> _trainProbAvg = new Dictionary<string, double>();


        private int _nbTrain;
        private int _nbTest;
        private int _nclasses = 2;
        private int[] _indexes;
        private Dictionary<string,double> _errors = new Dictionary<string, FType>();

        public double RfCoeff = 0.05;
        public double VarsCoeff = 1;
        public int BatchesInFirstStep = 100;
        public int BatchesInSecondStep = 100;
        public int TreesInBatch = 1;
        public int BatchesInBruteForce = 1;
        public bool IsLoadFirstStepBatches;
        public double OutliersPrct;
        public string IndexSortOrder = "none";
        public bool IsSaveTrees;
        public bool UseBatchLogit = false;

        /// <summary>
        /// Dict for all batches id->batch
        /// </summary>
        private readonly SortedDictionary<int, DecisionBatch> _classifiers = new SortedDictionary<int, DecisionBatch>();


        /// <summary>
        /// Random Forest classifier
        /// </summary>
        // /// <param name="prms">parameters, described in app.config</param>
        public DecisionForest()
        {
            LoadDefaultParams();
        }

        /// <summary>
        /// Default parameters for random-forest algorithm
        /// </summary>
        private void LoadDefaultParams()
        {
            string rfc = ConfigReader.Read("RfCoeff");
            if (rfc != null) RfCoeff = double.Parse(rfc.Replace(',', '.'), CultureInfo.InvariantCulture);
            Prms.Add("RfCoeff", RfCoeff);

            string bifs = ConfigReader.Read("BatchesInFirstStep");
            if (bifs != null) BatchesInFirstStep = int.Parse(bifs);
            Prms.Add("BatchesInFirstStep", BatchesInFirstStep);

            string biss = ConfigReader.Read("BatchesInSecondStep");
            if (biss != null) BatchesInSecondStep = int.Parse(biss);
            Prms.Add("BatchesInSecondStep", BatchesInSecondStep);

            string tib = ConfigReader.Read("TreesInBatch");
            if (tib != null) TreesInBatch = int.Parse(tib);
            Prms.Add("TreesInBatch", TreesInBatch);

            string tbf = ConfigReader.Read("BatchesInBruteForce");
            if (tbf != null) BatchesInBruteForce = int.Parse(tbf);
            Prms.Add("BatchesInBruteForce", BatchesInBruteForce);

            string lfsb = ConfigReader.Read("IsLoadFirstStepBatches");
            if (lfsb != null) IsLoadFirstStepBatches = bool.Parse(lfsb);
            Prms.Add("IsLoadFirstStepBatches", IsLoadFirstStepBatches);

            string op = ConfigReader.Read("OutliersPrct");
            if (op != null) OutliersPrct = double.Parse(op.Replace(',', '.'), CultureInfo.InvariantCulture);
            Prms.Add("OutliersPrct", OutliersPrct);

            string so = ConfigReader.Read("IndexSortOrder");
            if (so != null) IndexSortOrder = so;
            Prms.Add("IndexSortOrder", IndexSortOrder);

            string st = ConfigReader.Read("IsSaveTrees");
            if (st != null) IsSaveTrees = bool.Parse(st);
            Prms.Add("IsSaveTrees", IsSaveTrees);

            string vcf = ConfigReader.Read("VarsCoeff");
            if (vcf != null) VarsCoeff = double.Parse(vcf.Replace(',', '.'), CultureInfo.InvariantCulture);
            Prms.Add("VarsCoeff", VarsCoeff);

            string ubt = ConfigReader.Read("UseBatchLogit");
            if (ubt != null) UseBatchLogit = bool.Parse(ubt);
            Prms.Add("UseBatchLogit", UseBatchLogit);
        }

        public void AddDropColumn(string col)
        {
            _trainLoader.AddSkipColumn(col);
        }

        /// <summary>
        /// Reads data from train and test files, pre-modification
        /// </summary>
        public override void LoadData()
        {
            _trainLoader = TargetName != null ? new DataLoader<FType>(TargetName) : new DataLoader<FType>();
            _testLoader = TargetName != null ? new DataLoader<FType>(TargetName) : new DataLoader<FType>();

            if (!File.Exists(TrainPath))
            {
                Logger.Log("train file " + TrainPath + " not found");
                throw new FileNotFoundException("", TrainPath);
            }

            if (!File.Exists(TestPath))
            {
                Logger.Log("test file " + TestPath + " not found");
                throw new FileNotFoundException("", TestPath);
            }

            // loading train file
            _trainLoader.IsLoadForLearning = true;
            if (IdName!=null)
                _trainLoader.AddIdsString(IdName);
            _trainLoader.Load(TrainPath);

            foreach (var key in _trainLoader.TargetProb.Keys)
                Logger.Log("prob[" + key.ToString("F0") + "] = " + _trainLoader.TargetProb[key].ToString("F06"));

            Logger.Log("Outliers to drop: " + (int)(_trainLoader.TotalDataLines * OutliersPrct));

            // loading test file
            foreach (var id in _trainLoader.Ids.Keys) // the same id's
                _testLoader.AddIdColumn(id);

            foreach (var col in _trainLoader.SkippedColumns.Keys) // the same columns
                _testLoader.AddSkipColumn(col);

            // loading test file
            _testLoader.Load(TestPath);

            ModifyData();
        }


        /// <summary>
        /// build and test classifier
        /// </summary>
        public override ClassifierResult Build()
        {
            if (_trainLoader?.LearnRows == null)
                throw new InvalidOperationException("train set is empty");

            Clear();
            var ret = new ClassifierResult();

            using (var sw = new StreamWriter(new FileStream("auchist.csv", FileMode.Create, FileAccess.Write)))
            {
                sw.WriteLine("time;n;train auc;test auc;stype");

                // создаем первые классификаторы (first step)
                for (int i = 0; i < BatchesInFirstStep; i++)
                {
                    DecisionBatch cls;
                    if (IsLoadFirstStepBatches)
                    {
                        cls = DecisionBatch.Load(Environment.CurrentDirectory + "\\batches\\" + "batch_" + $"{i:0000.#}" + ".dmp");
                        if (cls == null)
                        {
                            cls = DecisionBatch.CreateBatch(_trainLoader.LearnRows, TreesInBatch, _nclasses, RfCoeff, 
                                VarsCoeff,null, IsParallel, UseBatchLogit);
                            if (IsSaveTrees) cls.Save();
                        }
                    }
                    else
                    {
                        cls = DecisionBatch.CreateBatch(_trainLoader.LearnRows, TreesInBatch, _nclasses, RfCoeff,
                            VarsCoeff, null, IsParallel, UseBatchLogit);
                        if (IsSaveTrees) cls.Save();
                    }

                    // расчитываем метрики для тестового и обучающего множества (накопленные)
                    var testRes = GetTestMetricsAccumulated(cls);
                    var trainRes = GetTrainMetricsAccumulated(cls);

                    Logger.Log("batch=" + i + " ok; train AUC=" + trainRes.AUC.ToString("F10") + " test AUC = " + testRes.AUC.ToString("F10"));
                    sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ";" + i + ";" + trainRes.AUC + ";" + testRes.AUC + ";none");
                    sw.Flush();

                    ret.AddStepResult(testRes, i);
                }

                // далее создаем классификаторы с учетом ошибки предыдущих (second step)
                int totalBatches = BatchesInFirstStep + BatchesInSecondStep;
                for (int i = BatchesInFirstStep; i < totalBatches; i++)
                {
                    DecisionBatch bestBatch = null;

                    // перестраиваем индексы плохо классифицированных объектов (плохие сначала)
                    RefreshIndexes();

                    double bestMetric = double.MaxValue;

                    // строим классификаторы и выбираем лучший
                    for (int k = 0; k < BatchesInBruteForce; k++)
                    {
                        var scls = DecisionBatch.CreateBatch(_trainLoader.LearnRows, TreesInBatch, _nclasses, RfCoeff,
                            VarsCoeff, _indexes, IsParallel, UseBatchLogit);

                        // расчитываем метрики для тестового множества
                        var trainCntRes = GetTrainClassificationCounts(scls);
                        int cnt = UseBatchLogit ? 1 : scls.CountTreesInBatch;

                        var rlist = new RocItem[_trainResult.Count]; // массив для оценки результата
                                                                        // находим статистики классификации
                        int idx = 0;
                        double accerr = 0.0;
                        foreach (string id in _trainResult.Keys)
                        {
                            if (rlist[idx] == null) rlist[idx] = new RocItem();

                            rlist[idx].Prob = trainCntRes[id]/ cnt; // среднее по наблюдениям
                            rlist[idx].Target = _trainResult[id];
                            rlist[idx].Predicted = rlist[idx].Prob > 0.5 ? 1 : 0;

                            //accerr += Math.Pow(rlist[idx].Prob - rlist[idx].Target, 2);

                            idx++;
                        }

                        Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
                        var clsRes = ResultCalc.GetResult(rlist, 0.05);

                        accerr = clsRes.LogLoss;

                        Logger.Log("sub cls #" + k + " auc=" + clsRes.AUC.ToString("F10") + " eps=" + accerr + (accerr < bestMetric ? " [best]" : ""));

                        if (accerr < bestMetric)
                        {
                            bestMetric = accerr;
                            bestBatch = scls;
                        }
                    }


                    var testRes = GetTestMetricsAccumulated(bestBatch);
                    var trainRes = GetTrainMetricsAccumulated(bestBatch);
                    if (IsSaveTrees)
                    {
                        if (bestBatch != null)
                            bestBatch.Save();
                        else
                            Logger.Log("best Batch is null");
                    }

                    ret.AddStepResult(testRes, i);
                    Logger.Log("batch=" + i + " ok; train AUC=" + trainRes.AUC.ToString("F10") + "; test AUC = " + testRes.AUC.ToString("F10"));
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
            _nbTest += UseBatchLogit ? 1 : cls.CountTreesInBatch; // обновляем общее кол-во деревьев
            return GetMetricsAccumulated(cls, _testProbSum, _testProbAvg, _testResult, _nbTest, GetTestClassificationCounts);
        }

        /// <summary>
        /// Полный расчет метрик качества классификации на тестовом множестве
        /// c учетом очередного классификатора
        /// </summary>
        /// <returns>Результат классификации</returns>
        private FinalFuncResult GetTrainMetricsAccumulated(DecisionBatch cls)
        {
            _nbTrain += UseBatchLogit ? 1 : cls.CountTreesInBatch; // обновляем общее кол-во деревьев
            return GetMetricsAccumulated(cls, _trainProbSum, _trainProbAvg, _trainResult, _nbTrain, GetTrainClassificationCounts);
        }

        private FinalFuncResult GetMetricsAccumulated(DecisionBatch cls,
                Dictionary<string, double> probSum,
                Dictionary<string, double> probAvg,
                Dictionary<string, int> resultDict,
                int nbcount,
                Func<DecisionBatch, Dictionary<string, double>> getResult
            )
        {
            // получаем результат по одному классификатору
            var result = getResult(cls);

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
        /// <param name="batch"></param>
        /// <returns>Количество деревьев, проголосовавших за каждый класс</returns>
        private Dictionary<string, double> GetTestClassificationCounts(DecisionBatch batch)
        {
            var probDict = new Dictionary<string, double>();

            // пробегаем по всем клиентски данным и сохраняем результат
            foreach (string id in _testDataDict.Keys)
            {
                //var y = batch.PredictCounts(_testDataDict[id]);
                var y = batch.PredictProba(_testDataDict[id]);
                if (!probDict.ContainsKey(id))
                    probDict.Add(id, y[1]);
            }

            return probDict;
        }

        /// <summary>
        /// Расчет классификации по тестовому обучающему на одном классификаторе
        /// </summary>
        /// <param name="batch"></param>
        /// <returns>Количество деревьев, проголосовавших за каждый класс</returns>
        private Dictionary<string, double> GetTrainClassificationCounts(DecisionBatch batch)
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

                //var y = batch.PredictCounts(cdata);
                var y = batch.PredictProba(cdata);
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
        public override ObjectClassificationResult PredictProba(double[] sarr)
        {
            var result = new ObjectClassificationResult();

            var y = PredictCounts(sarr);
            int cnt = _classifiers.Keys.Sum(id => _classifiers[id].CountTreesInBatch);

            for (int i = 0; i < y.Length; i++)
                y[i] /= cnt;
            
            result.Probs = y;
            return result;
        }


        /// <summary>
        /// Get trees counts for each class
        /// </summary>
        /// <param name="sarr">array of double params</param>
        /// <returns></returns>
        public double[] PredictCounts(double[] sarr)
        {
            var y = new double[_nclasses];

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
        /// Перестройка индексов для определения объектов, которые плохо классифицтрованы
        /// </summary>
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
            if (sarr == null)
            {
                Logger.Log("sarr is null");
                return;
            }
            foreach (var kvp in sarr)
                _indexes[i++] = Convert.ToInt32(double.Parse(kvp.Key));
        }


        private int _bucketNum;
        /// <summary>
        /// Load trees from dump files
        /// </summary>
        public override int LoadClassifier()
        {
            string treesRoot = ConfigReader.Read("TreesRoot");
            int bucketSize = int.Parse(ConfigReader.Read("BucketSize"));

            string treesDir = treesRoot ?? (Environment.CurrentDirectory + "\\batches");
            if (!Directory.Exists(treesDir))
            {
                Logger.Log("directory " + treesDir + " doesn't exists");
                return 0;
            }
            var dinfo = new DirectoryInfo(treesDir);
            _classifiers.Clear();

            var files = dinfo.GetFiles("*.dmp").OrderBy(f => f.Name).ToArray();
            if (bucketSize > 0)
            {
                files = files.Skip(bucketSize * _bucketNum).Take(bucketSize).ToArray();
                _bucketNum++;
            }
            int clid = 0;
            foreach (var finfo in files)
            {
                var cls = DecisionBatch.Load(finfo.FullName);
                _classifiers.Add(clid++, cls);
                Logger.Log(finfo.Name + " loaded;");
            }
            Logger.Log("all trees loaded;");
            return clid;
        }

        /// <summary>
        /// Get all trees in all batches count
        /// </summary>
        public int CountAllTrees => _classifiers.Sum(c => c.Value.CountTreesInBatch);

        /// <summary>
        /// Count all batches in classifier
        /// </summary>
        public int CountAllBatches => _classifiers.Count;


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
                _testDataDict.Add(row.Id, row.Values);
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
    }
}
