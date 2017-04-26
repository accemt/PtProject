using PtProject.Domain.Util;
using System.Collections.Generic;

namespace PtProject.Classifier
{
    public abstract class AbstractClassifier
    {
        protected readonly SortedDictionary<string, object> Prms;
        protected readonly string TrainPath;
        protected readonly string TestPath;
        protected readonly string TargetName;
        protected readonly string IdName;

        protected readonly bool IsParallel;

        public void PrintParams()
        {
            foreach (var p in Prms.Keys)
                Logger.Log(p + ": " + Prms[p]);
        }

        protected AbstractClassifier()
        {
            Prms = new SortedDictionary<string, object>();

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

        public abstract void LoadData();

        public abstract ClassifierResult Build();

        public abstract ObjectClassificationResult PredictProba(double[] sarr);

        public abstract int LoadClassifier();
    }
}
