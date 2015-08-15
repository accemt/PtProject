using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Domain
{
    public class DsfDataRow<T>
    {
        public string Id;
        public T Target;
        public T[] Coeffs;

        public static explicit operator DsfDataRow<object>(DsfDataRow<T> row)
        {
            var nrow = new DsfDataRow<object>();
            nrow.Id = row.Id;
            nrow.Target = row.Target;
            nrow.Coeffs = row.Coeffs.Cast<object>().ToArray();
            return nrow;
        }

        public override string ToString()
        {
            return Id + ": " + Target;
        }
    }
}
