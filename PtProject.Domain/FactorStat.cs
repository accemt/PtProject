using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Domain
{
    public class FactorStat<T>
    {
        public Dictionary<long, StatItem<T>> ModifiedStat { get; set; }
        public Dictionary<T, StatItem<T>> SourceStat { get; set; }

        public T Avg { get; set; }
        public T Stddev { get; set; }

        public int SourceCount { get; set; }
        public int ModifiedCount { get; set; }
    }
}
