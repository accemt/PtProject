using PtProject.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Classifier
{
    public class ClassifierResult
    {
        public Dictionary<int, FinalFuncResult> ResDict = new Dictionary<int, FinalFuncResult>();
        private double best = 0;
        public FinalFuncResult BestResult;
        public FinalFuncResult LastResult;

        public void AddStepResult(FinalFuncResult res, int n)
        {
            if (!ResDict.ContainsKey(n))
                ResDict.Add(n, res);

            if (best < res.AUC)
            {
                BestResult = res;
                best = res.AUC;
            }

            LastResult = res;
        }
    }
}
