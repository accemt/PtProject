using PtProject.Domain.Util;
using System.Collections.Generic;

namespace PtProject.Classifier
{
    public abstract class AbstractClassifier
    {
        protected SortedDictionary<string, object> Prms;
        public string TrainPath;
        public string TestPath;
        public string TargetName;
        public string IdName;

        public bool IsParallel;

        public void PrintParams()
        {
            foreach (var p in Prms.Keys)
                Logger.Log(p + ": " + Prms[p]);
        }

        protected AbstractClassifier(/*IDictionary<string,object> prms=null*/)
        {
            Prms = new SortedDictionary<string, object>();
            //foreach (var p in prms.Keys)
            //    Prms.Add(p, prms[p]);

            string trp = ConfigReader.Read("TrainPath");
            if (trp != null) TrainPath = trp;
            Prms.Add("TrainPath", TrainPath);

            string tsp = ConfigReader.Read("TestPath");
            if (tsp != null) TestPath = tsp;
            Prms.Add("TestPath", TestPath);

            string tn = ConfigReader.Read("TargetName");
            if (tn != null) TargetName = tn;
            Prms.Add("TargetName", TargetName);

            string inm = ConfigReader.Read("IdName");
            if (inm != null) IdName = inm;
            Prms.Add("IdName", IdName);

            string isp = ConfigReader.Read("IsParallel");
            if (isp != null) IsParallel = bool.Parse(isp);
            Prms.Add("IsParallel", IsParallel);
        }

        abstract public void LoadData();

        abstract public ClassifierResult Build();

        abstract public ObjectClassificationResult PredictProba(double[] sarr);

        abstract public int LoadClassifier();
    }
}
