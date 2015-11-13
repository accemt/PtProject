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
            if (args.Length<3 || args.Length>4)
            {
                Logger.Log("usage: program.exe train.csv test.csv target_name [dep_stat.csv]");
                return;
            }

            string trainPath = args[0];
            string testPath = args[1];
            string target = args[2];
            string depstatPath = args.Length >= 4 ? args[3] : null;

            string id = "id";

            if (depstatPath == null) // тогда подбираем параметры d и  ntrees
            {
                CreateRFStat(trainPath, testPath, id, target);
            }
            else // подбираем параметры зависимости с целевой и попарной зависимости
            {
                CreateDepStat(trainPath, testPath, depstatPath, id, target);
            }
        }

        private static void CreateDepStat(string trainPath, string testPath, string depstatPath, string id, string target)
        {
            FactorManager.Load(depstatPath, target);

            var fdList = new List<double>();
            fdList.Add(1000);
            fdList.Add(700);
            fdList.Add(300);
            fdList.Add(100);
            fdList.Add(50);
            fdList.Add(10);
            fdList.Add(5);
            fdList.Add(1);
            fdList.Add(0.5);
            fdList.Add(0);
            fdList = fdList.OrderByDescending(c => c).ToList();

            var tdList = new List<double>();
            tdList.Add(0);
            tdList.Add(0.5);
            tdList.Add(1);
            tdList.Add(1.5);
            tdList.Add(3);
            tdList.Add(7);
            tdList.Add(10);
            tdList.Add(15);
            tdList = tdList.OrderBy(c => c).ToList();

            using (var sw = new StreamWriter(new FileStream("depstat.csv", FileMode.Create, FileAccess.Write)))
            {
                sw.WriteLine("td;fd;cnt;last_auc;best_auc;vars");
                var countedDict = new Dictionary<string, ClassifierResult>();

                foreach (double td in tdList)
                {
                    foreach (double fd in fdList)
                    {
                        FactorManager.TargDep = td;
                        FactorManager.FactorDep = fd;
                        FactorManager.SelectFactors();
                        var factors = FactorManager.VisibleFactors;
                        Array.Sort(factors);
                        string vstr = string.Join("@", factors);

                        if (!countedDict.ContainsKey(vstr))
                        {
                            var cls = new RFClassifier(trainPath, testPath, target);
                            cls.AddIdColumn(id);
                            cls.SetRFParams(300, 0.3, 2);
                            var fdict = factors.ToDictionary(c => c);

                            foreach (string variable in FactorManager.FactorDict.Keys)
                            {
                                if (variable == target || variable == id) continue;
                                if (fdict.ContainsKey(variable))
                                    cls.AddDropColumns(new string[] { variable });
                            }

                            cls.LoadData();
                            var result = cls.Build();
                            countedDict.Add(vstr, result);
                        }
                        else
                        {
                            Logger.Log("skipping...");
                        }

                        sw.WriteLine(FactorManager.TargDep.ToString("F02") + ";" + FactorManager.FactorDep.ToString("F02") + ";" + factors.Length + ";" + countedDict[vstr].LastResult.AUC + ";" + countedDict[vstr].BestResult.AUC + ";" + vstr);
                        sw.Flush();
                        Logger.Log("td="+ td.ToString("F02") + "; fd="+ fd.ToString("F02")+"; cnt="+factors.Length + ";" + countedDict[vstr].LastResult.AUC);
                    }
                }
            }
        }

        private static void CreateRFStat(string trainPath, string testPath, string id, string target)
        {
            var cls = new RFClassifier(trainPath, testPath, target);
            cls.AddIdColumn(id);
            cls.LoadData();

            using (var sw = new StreamWriter(new FileStream("rfstat.csv", FileMode.Create, FileAccess.Write)))
            {
                sw.WriteLine("n;d;auc");
                for (double d = 0.01; d <= 1; d += 0.1)
                {
                    cls.SetRFParams(500, d, 2);
                    var result = cls.Build();
                    foreach (int n in result.ResDict.Keys)
                    {
                        sw.WriteLine(n + ";" + d.ToString("F02") + ";" + result.ResDict[n].AUC.ToString("F03"));
                        sw.Flush();
                    }
                }
            }
        }
    }
}

