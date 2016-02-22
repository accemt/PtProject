using PtProject.Domain.Util;
using PtProject.Loader;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using FType = System.Double;

namespace PtProject.Classifier
{
    public class KMeansClassifier : AbstractClassifier
    {
        private DataLoader<FType> _trainLoader;

        public int KNeighbors = 2;
        public double Pow = 2;

        private int _nclasses = 2;
        private double[] _avgs;
        private double[] _vars;

        public KMeansClassifier(/*Dictionary<string,object> prms=null*/) : base(/*prms*/)
        {
            LoadDefaultParams();
        }

        /// <summary>
        /// Default parameters for random-forest algorithm
        /// </summary>
        public void LoadDefaultParams()
        {
            string kn = ConfigReader.Read("KNeighbors");
            if (kn != null) KNeighbors = int.Parse(kn);
            _prms.Add("KNeighbors", KNeighbors);

            string pw = ConfigReader.Read("Pow");
            if (pw != null) Pow = double.Parse(pw.Replace(',', '.'), CultureInfo.InvariantCulture);
            _prms.Add("Pow", Pow);
        }

        public override ClassifierResult Build()
        {
            _avgs = new double[_trainLoader.NVars];
            _vars = new double[_trainLoader.NVars];

            // sum per column
            foreach (var row in _trainLoader.Rows)
            {
                for (int i = 0; i < row.Coeffs.Length; i++)
                    _avgs[i] += row.Coeffs[i];
            }

            // average per column
            for (int i=0;i<_avgs.Length;i++)
            {
                _avgs[i] /= _trainLoader.TotalDataLines;
            }

            // variance per column
            foreach (var row in _trainLoader.Rows)
            {
                for (int i = 0; i < row.Coeffs.Length; i++)
                    _vars[i] += Math.Pow(row.Coeffs[i]-_avgs[i],2);
            }
            for (int i = 0; i < _avgs.Length; i++)
            {
                _vars[i] /= (_trainLoader.TotalDataLines-1);
            }

            // modifying data
            foreach (var row in _trainLoader.Rows)
            {
                for (int i = 0; i < row.Coeffs.Length; i++)
                {
                    row.Coeffs[i] -= _avgs[i];
                    row.Coeffs[i] /= _vars[i]>0?Math.Sqrt(_vars[i]):1;
                }
            }

            return null;
        }

        public override int LoadClassifier()
        {
            LoadData();
            Build();

            return 0;
        }

        public override void LoadData()
        {
            _trainLoader = TargetName != null ? new DataLoader<FType>(TargetName) : new DataLoader<FType>();

            if (!File.Exists(TrainPath))
            {
                Logger.Log("train file " + TrainPath + " not found");
                throw new FileNotFoundException("", TrainPath);
            }

            // loading train file
            _trainLoader.AddIdsString(IdName);
            _trainLoader.Load(TrainPath);
        }

        public override double[] PredictProba(double[] sarr)
        {
            // modifying data
            for (int i = 0; i < sarr.Length; i++)
            {
                sarr[i] -= _avgs[i];
                sarr[i] /= _vars[i] > 0 ? Math.Sqrt(_vars[i]) : 1;
            }

            var tlist = new List<Tuple<double, double>>();
            foreach (var row in _trainLoader.Rows)
            {
                double rsum = 0;
                for (int i=0;i< row.Coeffs.Length; i++)
                {
                    double diff = sarr[i] - row.Coeffs[i];
                    rsum += Math.Pow(diff, Pow);
                }
                double dist = Math.Pow(rsum, 1/Pow);
                var tp = new Tuple<double, double>(dist, row.Target);
                tlist.Add(tp);
            }

            var slist = tlist.OrderBy(t => t.Item1).ToArray();
            var pcounts = new SortedDictionary<double, double>();

            for (int i=0;i<KNeighbors;i++)
            {
                var targ = slist[i].Item2;
                if (!pcounts.ContainsKey(targ))
                    pcounts.Add(targ, 0);
                pcounts[targ]++;
            }

            var ret = new double[_nclasses];
            for (int i=0;i<_nclasses;i++)
            {
                ret[i] = pcounts.ContainsKey(i) ? pcounts[i] / KNeighbors : 0;
            }
            return ret;
        }
    }
}
