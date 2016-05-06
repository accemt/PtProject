using PtProject.Classifier;
using PtProject.Domain.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

            string Mode = ConfigReader.Read("Mode");
            string TrainPath = ConfigReader.Read("TrainPath");
            string TestPath = ConfigReader.Read("TestPath");
            string IdName = ConfigReader.Read("IdName");
            string TargetName = ConfigReader.Read("TargetName");
            string DepstatPath = ConfigReader.Read("DepstatPath");
            string MeasureField = ConfigReader.Read("MeasureField");

            Logger.Log("Mode = " + Mode);
            Logger.Log("TrainPath = " + TrainPath);
            Logger.Log("TestPath = " + TestPath);
            Logger.Log("TargetName = " + TargetName);
            Logger.Log("IdName = " + IdName);

            if (Mode == "nd") // тогда подбираем параметры d и ntrees
            {
                Logger.Log("ntrees-d mode");
                CreateRFStat(TrainPath, TestPath, IdName, TargetName);
            }
            else // подбираем параметры зависимости с целевой и попарной мерой
            {
                Logger.Log("td-fd mode");
                Logger.Log("depstat = " + DepstatPath);
                Logger.Log("measure = " + MeasureField);
                CreateDepStat(TrainPath, TestPath, IdName, TargetName, DepstatPath, MeasureField);
            }
        }

        private static void CreateDepStat(string trainPath, string testPath, string ids, string target, string depstatPath, string measureField)
        {
            var fmngr = new FactorManager();
            fmngr.Load(depstatPath, target, measureField);

            var fdList = new List<double>();
            fdList.Add(0);
            fdList.Add(0.5);
            fdList.Add(1);
            fdList.Add(1.5);
            fdList = fdList.OrderByDescending(c => c).ToList();

            var tdList = new List<double>(fmngr.GetTargetValues());
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
                                var cls = new DecisionForest();
                                var fdict = factors.ToDictionary(c => c);

                                foreach (string variable in fmngr.FactorDict.Keys)
                                {
                                    if (!fdict.ContainsKey(variable))
                                        cls.AddDropColumn(variable);
                                }

                                cls.LoadData();
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
                double StartCoeff = 0.01;
                string sd = ConfigReader.Read("StartCoeff");
                if (sd != null)
                    StartCoeff = double.Parse(sd.Replace(',', '.'), CultureInfo.InvariantCulture);

                double Delta = 0.05;
                string sdl = ConfigReader.Read("Delta");
                if (sdl!=null)
                    Delta = double.Parse(sdl.Replace(',', '.'), CultureInfo.InvariantCulture);

                Logger.Log("StartCoeff = " + StartCoeff);
                Logger.Log("Delta = " + Delta);

                for (; StartCoeff <= 1; StartCoeff += Delta)
                {
                    var cls = new DecisionForest();
                    cls.LoadData();
                    cls.RfCoeff = StartCoeff;
                    var result = cls.Build();
                    foreach (int n in result.ResDict.Keys)
                    {
                        sw.WriteLine(n + ";" + StartCoeff.ToString("F03") + ";" + result.ResDict[n].AUC.ToString("F06"));
                        sw.Flush();
                    }
                }
            }
        }
    }
}

