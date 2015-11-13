using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PtProject.Domain.Util;

namespace PtProject.Domain
{
    public class FinalFuncResult
    {
        public double AUC;
        public double Recall;
        public double Precision;
        public double FMeasure;
        public double LogLoss;

        public List<RocPoint> RocPoints;

        public int OutflowLength;

        public override string ToString()
        {
            return AUC.ToString("F03");
        }
    }
}
