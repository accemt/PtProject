﻿using PtProject.Classifier;
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
            if (args.Length < 2 || args.Length > 6)
            {
                Logger.Log("usage: program.exe <data.csv> <conf.csv> [bucketsize=0 [savestat=false [id1,id2=, [target]]]]");
                return;
            }

            string dataPath = args[0];
            string confPath = args[1];
            int bucketsize = int.Parse(args.Length >= 3 ? args[2] : "0");
            string save = args.Length >= 4 ? args[3] : "false";
            string ids = args.Length >= 5 ? args[4] : ",";
            string target = args.Length >= 6 ? args[5] : null;

            Logger.Log("data = " + dataPath);
            Logger.Log("conf = " + confPath);
            Logger.Log("savestat = " + save);

            try
            {
                // loading modifier
                var modifier = new DataModifier(File.ReadAllLines(confPath));

                if (bucketsize > 0)
                {
                    // by tree bucket mode
                    Logger.Log("by tree bucket mode, bucket = " + bucketsize);
                    ByBucketMode(dataPath, ids, target, bucketsize, modifier);
                }
                else
                {
                    // by client mode
                    Logger.Log("by client mode");
                    ByClientMode(dataPath, ids, target, modifier);
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private static void ByClientMode(string dataPath, string ids, string target, DataModifier modifier)
        {
            try
            {
                // loading classifier
                var cls = new RFClassifier(null, null, target);
                cls.AddIdsString(ids);
                cls.LoadTrees("trees");

                // loading data
                var loader = target == null ? new DataLoader() : new DataLoader(target);
                foreach (string key in cls.IdsDict.Keys)
                    loader.AddIdColumn(key);
                loader.Load(dataPath);

                using (var sw = new StreamWriter(new FileStream(dataPath + "_calc.csv", FileMode.Create, FileAccess.Write)))
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

                        sw.WriteLine(row.Id + ";" + prob[1].ToString("F06") + ";" + row.Target);

                        if (idx % 12345 == 0)
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

        private static void ByBucketMode(string dataPath, string ids, string target, int bucketsize, DataModifier modifier)
        {
            try
            {
                // classifier
                var cls = new RFClassifier(null, null, target);
                cls.AddIdsString(ids);

                // loading data
                var loader = target == null ? new DataLoader() : new DataLoader(target);
                foreach (string key in cls.IdsDict.Keys)
                    loader.AddIdColumn(key);
                loader.Load(dataPath);

                int cnt=0;
                int idx = 0;
                int totaltrees = 0;
                var probDict = new Dictionary<string, double>();
                do
                {
                    Logger.Log("Processing bucket #" + idx);

                    cls.Clear();
                    cnt = cls.LoadTrees("trees", bucketsize, idx);
                    totaltrees += cnt;

                    int nc = 0;
                    // calculating prob for each row
                    foreach (var row in loader.Rows)
                    {
                        nc++;

                        var vals = new Dictionary<string, double>();
                        for (int i = 0; i < row.Coeffs.Length; i++)
                        {
                            string colname = loader.ColumnByIdxRow[i];
                            vals.Add(colname, row.Coeffs[i]);
                        }
                        var mvals = modifier.GetModifiedDataVector(vals);
                        var prob = cls.PredictProba(mvals, false);

                        if (!probDict.ContainsKey(row.Id))
                            probDict.Add(row.Id, 0);
                        probDict[row.Id] += prob[1];
                    }

                    idx++;
                }
                while (cnt >= bucketsize);

                using (var sw = new StreamWriter(new FileStream(dataPath + "_calc.csv", FileMode.Create, FileAccess.Write)))
                {
                    sw.WriteLine("id_client;id_model;prob;target");

                    foreach (var row in loader.Rows)
                    {
                        double prob = probDict[row.Id] / totaltrees;
                        sw.WriteLine(row.Id + ";" + prob.ToString("F06") + ";" + row.Target);
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
