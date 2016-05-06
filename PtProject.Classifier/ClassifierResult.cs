using PtProject.Domain;
using System.Collections.Generic;

namespace PtProject.Classifier
{
    public class ClassifierResult
    {
        public Dictionary<int, FinalFuncResult> ResDict = new Dictionary<int, FinalFuncResult>();
        private double _best;
        public FinalFuncResult BestResult;
        public FinalFuncResult LastResult;

        public void AddStepResult(FinalFuncResult res, int n)
        {
            if (!ResDict.ContainsKey(n))
                ResDict.Add(n, res);

            if (_best < res.AUC)
            {
                BestResult = res;
                _best = res.AUC;
            }

            LastResult = res;
        }
    }
}
