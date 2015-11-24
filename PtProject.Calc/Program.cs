using PtProject.Classifier;
using PtProject.Domain.Util;
using PtProject.Eval;
using PtProject.Loader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Calc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 5)
            {
                Logger.Log("usage: program.exe <data.csv> <conf.csv> [savestat=false [id1,id2=, [target]]]");
                return;
            }

            string dataPath = args[0];
            string confPath = args[1];
            string save = args.Length >= 3 ? args[2] : "false";
            string ids = args.Length >= 4 ? args[3] : ",";
            string target = args.Length >= 5 ? args[4] : null;

            Logger.Log("data = " + dataPath);
            Logger.Log("conf = " + confPath);
            Logger.Log("savestat = " + save);

            try
            {
                // loading modifier
                var modifier = new DataModifier(File.ReadAllLines(confPath));

                // loading classifier
                var cls = new RFClassifier(null, null, target);
                cls.AddIdsString(ids);
                cls.LoadTrees("trees");

                // loading data
                var loader = target == null ? new DataLoader() : new DataLoader(target);
                foreach (string key in cls.IdsDict.Keys)
                    loader.AddIdColumn(key);
                loader.Load(dataPath);

                using (var sw = new StreamWriter(new FileStream(dataPath+"_calc.csv", FileMode.Create, FileAccess.Write)))
                {
                    sw.WriteLine("id;prob;target");

                    int idx = 0;
                    // calculating prob for each row
                    foreach (var row in loader.Rows)
                    {
                        idx++;

                        var vals = new Dictionary<string, double>();
                        for (int i = 0; i < row.Coeffs.Length; i++)
                        {
                            string colname = loader.ColumnByIdxRow[i];
                            vals.Add(colname, row.Coeffs[i]);
                        }
                        var mvals = modifier.GetModifiedDataVector(vals);
                        var prob = cls.PredictProba(mvals);

                        sw.WriteLine(row.Id + ";" + prob[1] + ";" + row.Target);

                        if (idx%12345==0)
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
    }
}
