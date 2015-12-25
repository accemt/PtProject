using PtProject.Domain;
using PtProject.Domain.Util;
using PtProject.Loader;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using FType = System.Double;

namespace PtProject.CategoryModifier
{
    class Program
    {
        static DataLoader<FType> _loader = new DataLoader<FType>();
        static object _obj = new object();
        static Dictionary<TupleData, StatItem> _targDistr = new Dictionary<TupleData, StatItem>();

        static void Main(string[] args)
        {
            if (args.Length < 4 || args.Length > 4)
            {
                Logger.Log("usage: program.exe <train.csv> <conf.csv> <id> <target_name>");
                return;
            }

            string dataPath = args[0];
            string confPath = args[1];
            string id = args[2];
            string target = args[3];

            Logger.Log("data: " + dataPath);
            Logger.Log("conf : " + confPath);
            Logger.Log("id : " + id);
            Logger.Log("target : " + target);

            try
            {
                var fmgr = new FactorManager();
                fmgr.Load(confPath, target);
                fmgr.TargDep = 10;
                fmgr.FactorDep = 100;
                fmgr.SelectFactors();
                var cols = fmgr.VisibleFactors.ToArray();

                //_loader.MaxRowsLoaded = 10000;
                _loader.AddTargetColumn(target);
                _loader.AddIdColumn(id);
                _loader.CollectDistrStat = true;
                _loader.Load(dataPath);

                var statDict = new Dictionary<TupleData, Dictionary<TupleData, StatItem>>();

                // collecting stats
                int idx = 0;
                int n = 4;
                var iter = new CombinationIterator(cols, n);
                while (iter.MoveNext())
                {
                    idx++;

                    var cval = iter.Current;
                    var ftuple = new TupleData(cval);

                    statDict.Add(ftuple, new Dictionary<TupleData, StatItem>());

                    foreach (var row in _loader.Rows)
                    {
                        var vtuple = CreateValueTuple(cval, row);
                        if (!statDict[ftuple].ContainsKey(vtuple))
                            statDict[ftuple].Add(vtuple, new StatItem());
                        if (row.Target<=1)
                        {
                            statDict[ftuple][vtuple].Count++;
                            statDict[ftuple][vtuple].Targets += (int)row.Target;
                        }
                    }

                    foreach (var t in statDict[ftuple].Keys)
                    {
                        statDict[ftuple][t].TargetProb = statDict[ftuple][t].Targets / (double)statDict[ftuple][t].Count;
                    }

                    Logger.Log(ftuple + " done;");
                }

                // creating modified file
                using (var sw = new StreamWriter(new FileStream(dataPath + "_cat.csv", FileMode.Create, FileAccess.Write)))
                {
                    idx = 0;
                    sw.WriteLine(CreateHeader(cols, n));
                    sw.Flush();
                    double defProb = (double)_loader.TargetStat[1] / (_loader.TargetStat[1] + _loader.TargetStat[0]);

                    foreach (var row in _loader.Rows)
                    {
                       idx++;

                        var sb = new StringBuilder();
                        iter = new CombinationIterator(cols, n);
                        sb.Append(row.Id);

                        while (iter.MoveNext())
                        {
                            var cval = iter.Current;
                            var ftuple = new TupleData(cval);
                            var t = CreateValueTuple(cval, row);

                            double prob = statDict[ftuple].ContainsKey(t) ? statDict[ftuple][t].TargetProb : defProb;   

                            sb.Append(";" + prob.ToString("F05"));
                        }
                        sb.Append(";" + row.Target);
                        sw.WriteLine(sb);

                        if (idx%12345==0)
                        {
                            Logger.Log(idx + " lines writed;");
                            sw.Flush();
                        }
                    }
                    Logger.Log(idx + " lines writed; done;");
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private static TupleData CreateValueTuple(string[] cval, DataRow<double> row)
        {
            var vals = new List<object>(2);
            for (int i = 0; i < cval.Length; i++)
            {
                int cidx = _loader.RowIdxByColumn[cval[i]];
                double dval = row.Coeffs[cidx];
                vals.Add(dval);
            }
            var vtuple = new TupleData(vals);
            return vtuple;
        }

        private static string CreateHeader(string[] cols, int n)
        {
            var sb = new StringBuilder();
            var iter = new CombinationIterator(cols, n);
            sb.Append(_loader.IdName);

            while (iter.MoveNext())
            {
                var ftuple = new TupleData(iter.Current);
                sb.Append(";" + ftuple);
            }

            sb.Append(";" + _loader.TargetName);
            return sb.ToString();
        }
    }
}
