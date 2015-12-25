using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Domain
{
    public class TupleData
    {
        public readonly List<object> Vals;
        public int Count;
        public int Clicks;

        public TupleData(List<object> invals)
        {
            Vals = invals;
        }

        public TupleData(IEnumerable<string> svals)
        {
            Vals = new List<object>();
            Vals.AddRange(svals);
        }

        public TupleData(string str)
        {
            Vals = new List<object>();
            Vals.Add(str);
        }

        public bool Has(string key)
        {
            return Vals.Any(k => key == k.ToString());
        }

        public override int GetHashCode()
        {
            int code = 0;
            foreach (var val in Vals)
            {
                code += val.GetHashCode();
            }
            return code;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var val in Vals)
            {
                sb.Append(val);
                sb.Append('$');
            }

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = (TupleData)obj;

            if (Vals.Count != other.Vals.Count) return false;

            var en1 = Vals.GetEnumerator();
            var en2 = other.Vals.GetEnumerator();

            bool hasnext = en1.MoveNext() && en2.MoveNext();

            while (hasnext)
            {
                var obj1 = en1.Current;
                var obj2 = en2.Current;

                if (obj1 == null || obj2 == null) return false;
                if (!obj1.Equals(obj2)) return false;

                hasnext = en1.MoveNext() && en2.MoveNext();
            }

            return true;
        }
    }

}
