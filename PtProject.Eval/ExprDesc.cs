using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Eval
{
    public class ExprDesc : IComparable
    {
        public string ExprStr { get; set; }
        public int Idx { get; set; }
        public string Alias { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ExprDesc;
            if (other == null) return false;
            return ExprStr.Equals(other.ExprStr);
        }

        public override int GetHashCode()
        {
            return ExprStr.GetHashCode();
        }

        public override string ToString()
        {
            return ExprStr;
        }

        public int CompareTo(object obj)
        {
            var other = obj as ExprDesc;
            if (other == null) return 0;
            return ExprStr.CompareTo(other.ExprStr);
        }
    }
}
