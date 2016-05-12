using PtProject.Classifier;
using PtProject.Domain.Util;
using System;
using System.Reflection;

namespace PtProject.Train
{
    public class Program
    {   
        static void Main(string[] args)
        {
            string classifierType = ConfigReader.Read("classifierType");
            Logger.Log("classifierType:" + classifierType);

            try
            {
                AbstractClassifier cls = LoadClassifier(classifierType);

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

        private static AbstractClassifier LoadClassifier(string classifierType)
        {
            var assm = Assembly.LoadFrom("PtProject.Classifier.dll");
            Type clsType = assm.GetType(classifierType);
            var cls = (AbstractClassifier)Activator.CreateInstance(clsType);
            return cls;
        }
    }
}
