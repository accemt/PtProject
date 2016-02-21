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
        static void Main(string[] args)
        {
            string ClassifierType = ConfigReader.Read("ClassifierType");
            Logger.Log("ClassifierType:" + ClassifierType);

            try
            {
                AbstractClassifier cls = new RFClassifier();

                cls.PrintParams();
                cls.LoadData();
                var result = cls.Build();

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
