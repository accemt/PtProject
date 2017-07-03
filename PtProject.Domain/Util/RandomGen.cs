using System;

namespace PtProject.Domain.Util
{
    public class RandomGen
    {
        private static readonly Random Rnd = new Random(DateTime.Now.Millisecond + DateTime.Now.Second * 1000 + DateTime.Now.Second * 1000 * 60);
        private static readonly object Locker = new object();

        public static double GetDouble()
        {
            lock (Locker)
            {
                return Rnd.NextDouble();
            }
        }

        public static int Next(int n)
        {
            lock (Locker)
            {
                return Rnd.Next(n);
            }
        }

        public static double GetNormal(double mean, double stddev)
        {
            double u1 = GetDouble();
            double u2 = GetDouble();

            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double randNormal = mean + stddev * randStdNormal;
            return randNormal;
        }

        public static double GetTrangle()
        {
            double u1 = GetDouble() - 0.5;
            double u2 = GetDouble() - 0.5;

            return Math.Abs(u1 + u2);
        }
    }
}
