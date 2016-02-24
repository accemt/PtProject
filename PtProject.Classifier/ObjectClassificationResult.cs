using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Classifier
{
    public class ObjectClassificationResult
    {
        public double[] Probs { get; set; }

        public string ObjectInfo { get; set; }
    }
}
