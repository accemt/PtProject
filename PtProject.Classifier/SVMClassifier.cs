using PtProject.Domain;
using PtProject.Domain.Util;
using PtProject.Loader;
using SVM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FType = System.Double;

namespace PtProject.Classifier
{
    public class SVMClassifier : AbstractClassifier
    {
        private DataLoader<FType> _trainLoader;
        private DataLoader<FType> _testLoader;
        private Dictionary<string, List<double[]>> _testDataDict;
        private Dictionary<string, int> _resultDict;
        private ProblemCreator _problemCreator = new ProblemCreator();
        private Problem _trainProblem;
        private Problem _testProblem;
        private Model _model;


        private string _trainPath;
        private string _testPath;
        private string _target;
        private object _obj = new object();


        public override ClassifierResult Build()
        {
            //For this example (and indeed, many scenarios), the default
            //parameters will suffice.
            Parameter parameters = new Parameter();
            double C;
            double Gamma;


            //This will do a grid optimization to find the best parameters
            //and store them in C and Gamma, outputting the entire
            //search to params.txt.

            ParameterSelection.Grid(_trainProblem, parameters, "params.txt", out C, out Gamma);
            parameters.C = C;
            parameters.Gamma = Gamma;


            //Train the model using the optimal parameters.

            _model = Training.Train(_trainProblem, parameters);


            // пробегаем по всем клиентски данным и сохраняем результат
            var probDictList = new Dictionary<string, Dictionary<int, double>>();
            foreach (string id in _testDataDict.Keys)
            {
                if (!probDictList.ContainsKey(id))
                    probDictList.Add(id, new Dictionary<int, double>());

                foreach (var sarr in _testDataDict[id])
                {
                    var y = PredictProba(sarr);

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

            // находим статистики классификации
            var rlist = new RocItem[_resultDict.Count]; // массив для оценки результата
            int idx = 0;
            foreach (string id in probDict.Keys)
            {
                if (rlist[idx] == null) rlist[idx] = new RocItem();

                rlist[idx].Prob = probDict[id]; // среднее по наблюдениям
                rlist[idx].Target = _resultDict[id];
                rlist[idx].Predicted = 0;

                idx++;
            }
            Array.Sort(rlist, (o1, o2) => (1 - o1.Prob).CompareTo(1 - o2.Prob));
            var cres = ResultCalc.GetResult(rlist, 0.05);

            var clsRes = new ClassifierResult();
            clsRes.BestResult = cres;
            clsRes.LastResult = cres;
            clsRes.ResDict.Add(0, cres);

            return clsRes;
        }

        public override double[] PredictProba(double[] sarr)
        {
            Node[] x = new Node[sarr.Length];
            for (int j = 0; j < sarr.Length; j++)
            {
                x[j] = new Node();
                x[j].Index = j + 1;
                x[j].Value = sarr[j];
            }
            var result = Prediction.PredictProbability(_model, x);
            return result;
        }

        public override void LoadData()
        {
            string trainPath="";
            string testPath="";
            string ids="";
            string target="";

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
            _trainLoader.AddIdsString(ids);
            _trainLoader.ProceedRowFunc = ProceedRow;
            _trainLoader.Load(_trainPath);

            _trainProblem = _problemCreator.CreateProblem();

            // loading test file
            foreach (var id in _trainLoader.Ids.Keys) // the same id's
                _testLoader.AddIdColumn(id);

            foreach (var col in _trainLoader.SkippedColumns.Keys) // the same columns
                _testLoader.AddSkipColumn(col);

            // loading
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

        private object ProceedRow(DataRow<FType> row)
        {
            _problemCreator.ReadRow(row.Coeffs, row.Target);
            return _obj;
        }

        public override int LoadClassifier()
        {
            throw new NotImplementedException();
        }
    }
}
