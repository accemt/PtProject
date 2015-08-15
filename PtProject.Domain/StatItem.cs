using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Domain
{
    public class StatItem<T>
    {
        public int Count;
        public int Targets;
        public T TargetProb;
        public T ItemProb;
    }
}
