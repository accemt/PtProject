using PtProject.Classifier;
using PtProject.Domain.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Train
{
    public class Program
    {
        /*
        static void Main(string[] args)
        {
            if (args.Length < 3 || args.Length > 6)
            {
                Logger.Log("usage: program.exe <train.csv> <test.csv> <target_name> <id1,id2,id3=,>");
                return;
            }

            string trainPath = args[0];
            string testPath = args[1];
            string target = args[2];
            string ids = args[3];

            Logger.Log("train = " + trainPath);
            Logger.Log("test = " + testPath);
            Logger.Log("target = " + target);
            Logger.Log("ids = " + ids);

            try
            {
                var cls = new SVMClassifier();
                cls.LoadData(trainPath, testPath, ids, target);
                var result = cls.Build();

                Logger.Log("AUC = " + result.LastResult.AUC);
                Logger.Log("LogLoss = " + result.LastResult.LogLoss);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }
        */

        
        static void Main(string[] args)
        {
            if (args.Length < 3 || args.Length > 6)
            {
                Logger.Log("usage: program.exe <train.csv> <test.csv> <target_name> [id1,id2,id3=, [nbatches=300 [d=0.07]]]");
                return;
            }

            string trainPath = args[0];
            string testPath = args[1];
            string target = args[2];
            string ids = args.Length >= 4 ? args[3] : ",";
            int nbatches = int.Parse(args.Length >= 5 ? args[4] : "300");
            double d = double.Parse(args.Length >= 6 ? args[5] : "0.07");

            Logger.Log("train = " + trainPath);
            Logger.Log("test = " + testPath);
            Logger.Log("target = " + target);
            Logger.Log("ids = " + ids);
            Logger.Log("ntrees = " + nbatches);
            Logger.Log("d = " + d);

            try
            {
                var cls = new RFClassifier(nbatches, d, 2);
                cls.PrintParams();
                cls.LoadData(trainPath, testPath, ids, target);
                var result = cls.Build(savetrees: true, boost: true);

                Logger.Log("AUC = " + result.LastResult.AUC);
                Logger.Log("LogLoss = " + result.LastResult.LogLoss);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }
        
    }
}
