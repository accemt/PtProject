using PtProject.Classifier;
using PtProject.Domain.Util;
using PtProject.Eval;
using PtProject.Loader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Calc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string DataPath = ConfigReader.Read("DataPath");
            string ConfPath = ConfigReader.Read("ConfPath");
            int BucketSize = int.Parse(ConfigReader.Read("BucketSize"));
            string ClassifierType = ConfigReader.Read("ClassifierType");
            string IdName = ConfigReader.Read("IdName");
            string TargetName = ConfigReader.Read("TargetName");

            Logger.Log("DataPath = " + DataPath);
            Logger.Log("ConfPath = " + ConfPath);
            Logger.Log("ClassifierType = " + ClassifierType);
            Logger.Log("IdName = " + IdName);
            Logger.Log("TargetName = " + TargetName);

            try
            {
                // loading modifier
                DataModifier modifier = null;
                if (ConfPath != null)
                    modifier = new DataModifier(File.ReadAllLines(ConfPath));

                // loading classifier
                AbstractClassifier cls = LoadClassifier(ClassifierType);

                //if (BucketSize > 0)
                //{
                //    // by tree bucket mode
                //    Logger.Log("by tree bucket mode, BucketSize = " + BucketSize);
                //    ByBucketMode(DataPath, IdName, TargetName, BucketSize, modifier);
                //}
                //else
                //{
                // by client mode
                Logger.Log("by client mode");
                ByClientMode(DataPath, IdName, TargetName, modifier, cls);
                //}
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private static AbstractClassifier LoadClassifier(string ClassifierType)
        {
            AbstractClassifier cls = null;
            var assm = Assembly.LoadFrom("PtProject.Classifier.dll");
            Type clsType = assm.GetType(ClassifierType);
            cls = (AbstractClassifier)Activator.CreateInstance(clsType);
            return cls;
        }

        private static void ByClientMode(string dataPath, string ids, string target, DataModifier modifier, AbstractClassifier cls)
        {
            try
            {
                cls.LoadClassifier();

                // loading data
                var loader = target == null ? new DataLoader() : new DataLoader(target);
                loader.AddIdsString(ids);
                loader.Load(dataPath);

                using (var sw = new StreamWriter(new FileStream(dataPath + "_calc.csv", FileMode.Create, FileAccess.Write)))
                {
                    if (target!=null)
                        sw.WriteLine(loader.IdName + ";prob;target");
                    else
                        sw.WriteLine(loader.IdName + ";prob");

                    int idx = 0;
                    // calculating prob for each row
                    foreach (var row in loader.Rows)
                    {
                        idx++;
                        double[] mvals = GetRowValues(modifier, loader, row);
                        var prob = cls.PredictProba(mvals);

                        string targStr = target != null ? (";" + row.Target) : null;
                        string oinfo = prob.ObjectInfo != null ? (";" + prob.ObjectInfo) : null;
                        sw.WriteLine(row.Id + ";" + prob.Probs[1] + targStr + oinfo);

                        if (idx % 123 == 0)
                        {
                            Logger.Log(idx + " lines writed;");
                            sw.Flush();
                        }
                    }

                    Logger.Log(idx + " lines writed; done;");

                    sw.Close();
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private static double[] GetRowValues(DataModifier modifier, DataLoader loader, Domain.DataRow<float> row)
        {
            if (modifier == null)
                return Array.ConvertAll(row.Coeffs, x => (double)x);

            var vals = new Dictionary<string, double>();
            for (int i = 0; i < row.Coeffs.Length; i++)
            {
                string colname = loader.RowColumnByIdx[i];
                vals.Add(colname, row.Coeffs[i]);
            }
            var mvals = modifier.GetModifiedDataVector(vals);
            return mvals;
        }

        /*
        private static void ByBucketMode(string dataPath, string ids, string target, int bucketsize, DataModifier modifier)
        {
            try
            {
                // classifier
                var cls = new RFClassifier();

                // loading data
                var loader = target == null ? new DataLoader() : new DataLoader(target);
                loader.AddIdsString(ids);
                loader.Load(dataPath);

                int cnt=0;
                int idx = 0;
                int totaltrees = 0;
                var probDict = new Dictionary<string, double>();
                do
                {
                    Logger.Log("Processing bucket #" + idx);

                    cls.Clear();
                    cnt = cls.LoadTrees(null, bucketsize, idx);
                    if (cnt > 0)
                    {
                        totaltrees += cls.CountAllTrees;

                        int nc = 0;
                        // calculating prob for each row
                        foreach (var row in loader.Rows)
                        {
                            nc++;

                            var vals = new Dictionary<string, double>();
                            for (int i = 0; i < row.Coeffs.Length; i++)
                            {
                                string colname = loader.RowColumnByIdx[i];
                                vals.Add(colname, row.Coeffs[i]);
                            }
                            var mvals = modifier.GetModifiedDataVector(vals);
                            var prob = cls.PredictCounts(mvals);

                            if (!probDict.ContainsKey(row.Id))
                                probDict.Add(row.Id, 0);
                            probDict[row.Id] += prob[1];
                        }

                        idx++;
                    }
                }
                while (cnt >= bucketsize);

                using (var sw = new StreamWriter(new FileStream(dataPath + "_calc.csv", FileMode.Create, FileAccess.Write)))
                {
                    if (target != null)
                        sw.WriteLine(loader.IdName + ";prob;target");
                    else
                        sw.WriteLine(loader.IdName + ";prob");

                    foreach (var row in loader.Rows)
                    {
                        double prob = probDict[row.Id] / totaltrees;
                        string targStr = target != null ? (";" + row.Target) : null;
                        sw.WriteLine(row.Id + ";" + prob + targStr);
                    }

                    sw.Close();
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }
        */
    }
}
