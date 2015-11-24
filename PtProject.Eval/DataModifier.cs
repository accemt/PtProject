using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PtProject.Eval
{
    public class DataModifier
    {
        public IDictionary<string, ExprDesc> Fields { get; private set; }
        private Dictionary<ExprDesc, Expression> _exprs = new Dictionary<ExprDesc, Expression>();

        public DataModifier(IDictionary<string, ExprDesc> fields)
        {
            Fields = fields;
        }

        public DataModifier(string[] rows)
        {
            var result = new Dictionary<string, ExprDesc>();

            int idx = 0;
            foreach (string row in rows)
            {
                string trow = row.ToLower().Trim();
                if (trow.StartsWith("#")) continue;
                if (string.IsNullOrWhiteSpace(trow)) continue;
                string[] blocks = trow.Split(';');

                string expr = blocks[0];
                if (string.IsNullOrWhiteSpace(expr)) continue;

                var descr = new ExprDesc();
                descr.ExprStr = expr;
                descr.Idx = idx;
                descr.Alias = blocks.Length >= 2 ? (string.IsNullOrWhiteSpace(blocks[1]) ? null : blocks[1]) : null;

                if (!result.ContainsKey(expr))
                    result.Add(expr, descr);
                else
                    throw new Exception("duplicate expr: " + expr);
            }

            Fields = result;
        }

        public double[] GetModifiedDataVector(Dictionary<string, double> values)
        {
            var dlist = new List<double>();
            var mdata = GetModifiedDataDict(values);

            foreach (var c in Fields.Values.OrderBy(t => t.Idx))
            {
                dlist.Add(mdata[c]);
            }

            return dlist.ToArray();
        }

        public Dictionary<ExprDesc, double> GetModifiedDataDict(Dictionary<string, double> values)
        {
            var result = new Dictionary<ExprDesc, double>();

            foreach (var desc in Fields.Values)
            {
                Expression exp = null;
                if (_exprs.ContainsKey(desc))
                    exp = _exprs[desc];
                else
                {
                    exp = new Expression(desc.ExprStr);
                    exp.Compile();

                    _exprs.Add(desc, exp);
                }

                var val = exp.Eval(values);
                result.Add(desc, val);
            }

            return result;
        }
    }
}
