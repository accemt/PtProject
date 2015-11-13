﻿using PtProject.DataLoader;
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

namespace PtProject.Classifier
{
    public class RFClassifier
    {
        private DataLoader<double> _trainLoader;
        private DataLoader<double> _testLoader;
        private Dictionary<string, List<double[]>> _testDataDict;
        private Dictionary<string, int> _resultDict;

        private string _trainPath;
        private string _testPath;
        private string _target;
        private double _rfcoeff = 0.01;
        private int _ntrees = 1;
        private int _nclasses = 2;

        public Dictionary<int, string> TrainColumns
        {
            get { return _trainLoader.ColumnByIdx; }
        }

        public Dictionary<int, string> TestColumns
        {
            get { return _testLoader.ColumnByIdx; }
        }

        /// <summary>
        /// Creates random forest classifier
        /// </summary>
        /// <param name="trainPath"></param>
        /// <param name="testPath"></param>
        /// <param name="target"></param>
        public RFClassifier(string trainPath, string testPath, string target)
        {
            _trainPath = trainPath;
            _testPath = testPath;
            _target = target;

            _trainLoader = new DataLoader<double>(_target);
            _testLoader = new DataLoader<double>(_target);
        }

        /// <summary>
        /// Drops columns from learning set
        /// </summary>
        /// <param name="cols">set of columns</param>
        public void AddDropColumns(IEnumerable<string> cols)
        {
            foreach (var c in cols)
            {
                _trainLoader.DropIds.Add(c, c);
            }
        }

        /// <summary>
        /// Id column. If empty then row_number will be used.
        /// </summary>
        /// <param name="id"></param>
        public void AddIdColumn(string id)
        {
            _trainLoader.AddIdColumn(id);
        }

        public void SetRFParams(int ntrees, double r, int nclasses)
        {
            _ntrees = ntrees;
            _rfcoeff = r;
            _nclasses = nclasses;
        }

        /// <summary>
        /// Reads data from learning files and build classfier
        /// </summary>
        public void LoadData()
        {
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
            _trainLoader.Load(_trainPath);

            // loading test file
            foreach (var id in _testLoader.IdName.Keys) // the same id's
                _testLoader.AddIdColumn(id);
            _testLoader.Load(_testPath);

            _testDataDict = new Dictionary<string, List<double[]>>(); // тестовые данные: id -> список строк на данный id
            _resultDict = new Dictionary<string, int>(); // результат тестовых данных: id -> target

            // модифицируем тестовые данные
            foreach (var row in _testLoader.Rows)
            {
                // сохраняем результат
                if (!_resultDict.ContainsKey(row.Id))
                    _resultDict.Add(row.Id, Convert.ToInt32(row.Target));

                // сохраняем ответ из бюро
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
        /// Reads data from learning files and build classfier
        /// </summary>
        public ClassifierResult Build(bool savetree=false)
        {
            var probSum = new Dictionary<string, double>(); // сюда сохраняем сумму
            var probAvg = new Dictionary<string, double>(); // сюда сохраняем среднее
            var rlist = new RocItem[_resultDict.Count]; // массив для оценки результата

            var ret = new ClassifierResult();

            for (int i = 0; i < _ntrees; i++)
            {
                // создаем дерево
                var tree = CreateTree(_trainLoader, 1);
                if (savetree)
                {
                    SerializeTree(tree, i); // сохраняем
                }

                Logger.Log("d="+_rfcoeff+"; tree="+(i+1));

                // получаем результат по одному дереву
                var result = GetClassificationResult(tree);

                // сохраняем общую сумму вероятностей по идентификатору
                foreach (string id in result.Keys)
                {
                    if (!probSum.ContainsKey(id))
                        probSum.Add(id, 0);

                    probSum[id] += result[id];
                }

                // сохраняем cреднее вероятностей по идентификатору
                foreach (string id in result.Keys)
                {
                    if (!probAvg.ContainsKey(id))
                        probAvg.Add(id, 0);

                    probAvg[id] = probSum[id] / (i + 1);
                }

                // находим статистики классификации
                int idx = 0;
                foreach (string id in result.Keys)
                {
                    if (rlist[idx] == null) rlist[idx] = new RocItem();

                    rlist[idx].Prob = probAvg[id]; // среднее по наблюдениям
                    rlist[idx].Target = _resultDict[id];
                    rlist[idx].Predicted = 0;

                    idx++;
                }
                Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
                var clsRes = ResultCalc.GetResult(rlist, 0.05);

                ret.AddStepResult(clsRes,i);
            }

            return ret;
        }

        private Dictionary<string, double> GetClassificationResult(alglib.decisionforest tree)
        {
            // пробегаем по всем клиентски данным и сохраняем результат
            var probDictList = new Dictionary<string, Dictionary<int, double>>();
            foreach (string id in _testDataDict.Keys)
            {
                if (!probDictList.ContainsKey(id))
                    probDictList.Add(id, new Dictionary<int, double>());

                foreach (var sarr in _testDataDict[id])
                {
                    var y = new double[_nclasses];
                    alglib.dfprocess(tree, sarr, ref y);

                    double prob = y[1];
                    int kmax = probDictList[id].Keys.Count == 0 ? 0 : probDictList[id].Keys.Max() + 1;
                    probDictList[id].Add(kmax, prob);
                }
            }

            // вероятность дефолта определяется как среднее по записям для клиента
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

        private alglib.decisionforest CreateTree(DataLoader<double> loader, int cnt)
        {
            alglib.decisionforest df;

            int npoints = loader.TotalDataLines;
            int info;
            double[,] xy = loader.LearnRows;

            alglib.dfreport rep;
            int nvars = loader.NVars;

            alglib.dfbuildrandomdecisionforest(xy, npoints, nvars, _nclasses, cnt, _rfcoeff, out info, out df, out rep);

            return df;
        }

        private static void SerializeTree(alglib.decisionforest tree, int i)
        {
            var fs = new FileStream("tree_" + i + ".tree", FileMode.Create);
            string serstr;
            alglib.dfserialize(tree, out serstr);

            // Construct a BinaryFormatter and use it to serialize the data to the stream.
            BinaryFormatter formatter = new BinaryFormatter();
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
    }
}
