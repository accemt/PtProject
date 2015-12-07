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
    }
}
