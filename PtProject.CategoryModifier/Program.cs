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
        static int _idx;
        static object _obj = new object();
        static Dictionary<TupleData, StatItem<FType>> _targDistr = new Dictionary<TupleData, StatItem<FType>>();

        static void Main(string[] args)
        {
            if (args.Length < 4 || args.Length > 5)
            {
                Logger.Log("usage: program.exe <train.csv> <conf.csv> <id> <target_name> [<test.csv>]");
                return;
            }

            string trainPath = args[0];
            string confPath = args[1];
            string id = args[2];
            string target = args[3];
            string testPath = args.Length >= 5 ? args[4] : null;

            Logger.Log("train: " + trainPath);
            Logger.Log("conf : " + confPath);
            Logger.Log("id : " + id);
            Logger.Log("target : " + target);
            Logger.Log("test: " + testPath);

            try
            {
                var fmgr = new FactorManager();
                fmgr.Load(confPath, target);
                fmgr.TargDep = 1;
                fmgr.SelectFactors();
                var cols = fmgr.VisibleFactors.Take(16).ToArray();

                _loader.MaxRowsLoaded = 10000;
                _loader.AddTargetColumn(target);
                _loader.AddIdColumn(id);
                _loader.Load(trainPath);

                var iter = new CombinationIterator(cols, 2);
                while (iter.MoveNext())
                {
                    var cval = iter.Current;
                    var ftuple = new TupleData(cval);

                    foreach (var row in _loader.Rows)
                    {
                        _idx++;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }
    }
}
