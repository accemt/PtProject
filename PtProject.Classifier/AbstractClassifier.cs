using PtProject.Domain.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Classifier
{
    public abstract class AbstractClassifier
    {
        protected SortedDictionary<string, object> _prms;
        public string TrainPath;
        public string TestPath;
        public string TargetName;
        public string IdName;

        public bool IsParallel = false;

        public void PrintParams()
        {
            foreach (var p in _prms.Keys)
                Logger.Log(p + ": " + _prms[p]);
        }

        protected AbstractClassifier(/*IDictionary<string,object> prms=null*/)
        {
            _prms = new SortedDictionary<string, object>();
            //foreach (var p in prms.Keys)
            //    _prms.Add(p, prms[p]);

            string trp = ConfigReader.Read("TrainPath");
            if (trp != null) TrainPath = trp;
            _prms.Add("TrainPath", TrainPath);

            string tsp = ConfigReader.Read("TestPath");
            if (tsp != null) TestPath = tsp;
            _prms.Add("TestPath", TestPath);

            string tn = ConfigReader.Read("TargetName");
            if (tn != null) TargetName = tn;
            _prms.Add("TargetName", TargetName);

            string inm = ConfigReader.Read("IdName");
            if (inm != null) IdName = inm;
            _prms.Add("IdName", IdName);

            string isp = ConfigReader.Read("IsParallel");
            if (isp != null) IsParallel = bool.Parse(isp);
            _prms.Add("IsParallel", IsParallel);
        }

        abstract public void LoadData();

        abstract public ClassifierResult Build();

        abstract public ObjectClassificationResult PredictProba(double[] sarr);

        abstract public int LoadClassifier();
    }
}
