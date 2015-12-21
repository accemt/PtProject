using PtProject.Classifier;
using PtProject.Domain.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Calibrator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length<3 || args.Length>6)
            {
                Logger.Log("usage: program.exe <train.csv> <test.csv> <target_name> [ids=, [dep_stat.csv=null [measure_field=Chi2Coeff]]]");
                return;
            }

            string trainPath = args[0];
            string testPath = args[1];
            string target = args[2];
            string ids = args.Length >= 4 ? args[3] : ",";
            string depstatPath = args.Length >= 5 ? args[4] : null;
            string measureField = args.Length >= 6 ? args[5] : "Chi2Coeff";

            Logger.Log("train = " + trainPath);
            Logger.Log("test = " + testPath);
            Logger.Log("target = " + target);
            Logger.Log("ids = " + ids);

            if (depstatPath == null) // тогда подбираем параметры d и ntrees
            {
                Logger.Log("ntrees-d mode");
                CreateRFStat(trainPath, testPath, ids, target);
            }
            else // подбираем параметры зависимости с целевой и попарной мерой
            {
                Logger.Log("td-fd mode");
                Logger.Log("depstat = " + depstatPath);
                Logger.Log("measure = " + measureField);
                CreateDepStat(trainPath, testPath, depstatPath, ids, target, measureField);
            }
        }

        private static void CreateDepStat(string trainPath, string testPath, string depstatPath, string ids, string target, string measureField)
        {
            var fmngr = new FactorManager();
            fmngr.Load(depstatPath, target, measureField);

            var fdList = new List<double>();
            fdList.Add(0); 
            fdList = fdList.OrderByDescending(c => c).ToList();

            var tdList = new List<double>(fmngr.GetTargetValues());
            //tdList = tdList.OrderBy(c => c).ToList();
            tdList = tdList.OrderByDescending(c => c).ToList();

            using (var sw = new StreamWriter(new FileStream("depstat.csv", FileMode.Create, FileAccess.Write)))
            {
                sw.WriteLine("td;fd;cnt;last_auc;best_auc;vars;measure");
                var countedDict = new Dictionary<string, ClassifierResult>();

                foreach (double td in tdList)
                {
                    foreach (double fd in fdList)
                    {
                        try
                        {
                            fmngr.TargDep = td;
                            fmngr.FactorDep = fd;
                            fmngr.SelectFactors();
                            var factors = fmngr.VisibleFactors;
                            Array.Sort(factors);
                            string vstr = string.Join("@", factors);

                            if (!countedDict.ContainsKey(vstr))
                            {
                                var cls = new RFClassifier();
                                var fdict = factors.ToDictionary(c => c);

                                foreach (string variable in fmngr.FactorDict.Keys)
                                {
                                    if (!fdict.ContainsKey(variable))
                                        cls.AddDropColumns(new string[] { variable });
                                }

                                cls.LoadData(trainPath, testPath, ids, target);
                                var result = cls.Build();
                                countedDict.Add(vstr, result);
                            }
                            else
                            {
                                Logger.Log("skipping...");
                            }

                            sw.WriteLine(fmngr.TargDep.ToString("F06") + ";" + fmngr.FactorDep.ToString("F06") + ";" + factors.Length + ";" + countedDict[vstr].LastResult.AUC + ";" + countedDict[vstr].BestResult.AUC + ";" + vstr + ";" + measureField);
                            sw.Flush();
                            Logger.Log("td=" + td.ToString("F06") + "; fd=" + fd.ToString("F06") + "; cnt=" + factors.Length + ";" + countedDict[vstr].LastResult.AUC);
                        }
                        catch (Exception e)
                        {
                            Logger.Log(e);
                        }
                    }
                }
            }
        }

        private static void CreateRFStat(string trainPath, string testPath, string ids, string target)
        {

            using (var sw = new StreamWriter(new FileStream("rfstat.csv", FileMode.Create, FileAccess.Write)))
            {
                sw.WriteLine("n;d;auc");
                for (double d = 0.01; d <= 1; d += 0.05)
                {
                    var cls = new RFClassifier();
                    cls.RFCoeff = d;
                    cls.PrintParams();
                    cls.LoadData(trainPath, testPath, ids, target);
                    var result = cls.Build();
                    foreach (int n in result.ResDict.Keys)
                    {
                        sw.WriteLine(n + ";" + d.ToString("F06") + ";" + result.ResDict[n].AUC.ToString("F03"));
                        sw.Flush();
                    }
                }
            }
        }
    }
}

