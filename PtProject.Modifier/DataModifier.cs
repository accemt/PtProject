using PtProject.Eval;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PtProject.Modifier
{
    public class DataModifier
    {
        public IDictionary<string, ExprDesc> Fields { get; private set; }
        private Dictionary<string, Expression> _exprs = new Dictionary<string, Expression>();

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
                string trow = row.Trim();
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

        public double[] GetVectorData(Dictionary<string, double> values)
        {
            var dlist = new List<double>();

            foreach (var c in Fields.Values.OrderBy(c => c.Idx))
            {
                dlist.Add(values[c.ExprStr]);
            }

            return dlist.ToArray();
        }

        public Dictionary<string, double> GetDictData(Dictionary<string, double> values)
        {
            var result = new Dictionary<string, double>();

            foreach (var col in Fields.Keys)
            {
                Expression exp = null;
                if (_exprs.ContainsKey(col))
                    exp = _exprs[col];
                else
                {
                    exp = new Expression(col);
                    exp.Compile();

                    _exprs.Add(col, exp);
                }

                var val = exp.Eval(values);
                result.Add(col, val);
            }

            return result;
        }
    }
}
