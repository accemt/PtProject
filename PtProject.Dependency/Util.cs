using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Dependency
{
    public static class Util
    {
        public static double InvChi2CDF(double freedom, double p)
        {
            double res = -1;
            try
            {
                res = MathNet.Numerics.Distributions.ChiSquared.InvCDF(freedom, p);
            }
            catch {}
            return res;
        }
    }
}
