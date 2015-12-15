using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Classifier
{
    [Serializable]
    public class DecisionForest
    {
        public alglib.decisionforest Forest { get; set; }
        public double? Coeff;
    }
}
