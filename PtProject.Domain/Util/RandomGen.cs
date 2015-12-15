using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Domain.Util
{
    public class RandomGen
    {
        private static Random _rnd = new Random(DateTime.Now.Millisecond + DateTime.Now.Second * 1000 + DateTime.Now.Second * 1000 * 60);

        public static double GetDouble()
        {
            return _rnd.NextDouble();
        }

        public static int Next(int n)
        {
            return _rnd.Next(n);
        }

        public static double GetNormal(double mean, double stddev)
        {
            double u1 = _rnd.NextDouble();
            double u2 = _rnd.NextDouble();

            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double randNormal = mean + stddev * randStdNormal;
            return randNormal;
        }

        public static double GetTrangle()
        {
            double u1 = _rnd.NextDouble()-0.5;
            double u2 = _rnd.NextDouble()-0.5;

            return Math.Abs(u1 + u2);
        }
    }
}
