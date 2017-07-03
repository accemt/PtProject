using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PtProject.Eval
{
    public class Source
    {
        public static string[] Create(string expr, int num)
        {
            var result = new List<string>();

            string nexpr = ModifyExpression(expr);

            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace PtProject.Eval");
            sb.AppendLine("{");
            sb.AppendLine("    public class Temp" + num);
            sb.AppendLine("    {");
            sb.AppendLine("        public static double f(IDictionary<string, double> v)");
            sb.AppendLine("        {");
            sb.AppendLine("            double r = "+nexpr+";");
            sb.AppendLine("            if (double.IsNaN(r) || double.IsInfinity(r)) r = -1;");
            sb.AppendLine("            return r;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");


            result.Add(sb.ToString());

            return result.ToArray();
        }

        private static string ModifyExpression(string expr)
        {
            string pattern = "[[]\\s*(?<field>\\w+)\\s*[]]";
            var matches = Regex.Matches(expr, pattern);
            string ret = expr;
            foreach (Match m in matches)
            {
                string field = m.Groups["field"].Value.ToLower();
                string mval = m.Value;

                ret = ret.Replace(mval, "v[\"" + field + "\"]");
            }

            return ret;
        }
    }
}
