using System.Collections.Generic;

namespace PtProject.Domain
{
    public class FactorStat<T>
    {
        public Dictionary<T, StatItem> ModifiedStat { get; set; }
        public Dictionary<T, StatItem> SourceStat { get; set; }

        public int SourceCount { get; set; }
        public int ModifiedCount { get; set; }
    }
}
