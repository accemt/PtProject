using PtProject.Domain;
using PtProject.Domain.Util;
using PtProject.Eval;
using PtProject.Loader;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using FType = System.Double;
 
namespace PtProject.Modifier
{
    class Program
    {
        static DataLoader<FType> _loader = new DataLoader<FType>();
        static StreamWriter _sw;
        static DataModifier _modifier;
        static string _header;
        static int _idx; //
        static object _obj = new object();

        static void Main(string[] args)
        {
            string DataInPath = ConfigReader.Read("DataInPath");
            string DataOutPath = ConfigReader.Read("DataOutPath");
            string ConfigPath = ConfigReader.Read("ConfigPath");

            Logger.Log("DataInPath: " + DataInPath);
            Logger.Log("DataOutPath : " + DataOutPath);
            Logger.Log("ConfigPath: " + ConfigPath);

            try
            {
                _modifier = ConfigPath == null ? null : new DataModifier(File.ReadAllLines(ConfigPath));
                _sw = new StreamWriter(new FileStream(DataOutPath, FileMode.Create, FileAccess.Write), Encoding.GetEncoding(1251));

                _loader.ProceedRowFunc = ProceedRow;
                _loader.Load(DataInPath);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
            finally
            {
                _sw.Close();
            }
        }

        static object ProceedRow(DataRow<FType> row)
        {
            if (_header==null)
            {
                _header = CreateHeader();
                _sw.WriteLine(_header);
            }
            _idx++;

            // get modified values
            var vals = GetRowValues(row);

            // create modified values string
            _sw.WriteLine(GetDataVector(vals));

            if (_idx % 12345 == 0) _sw.Flush();
            return _obj;
        }

        private static StringBuilder GetDataVector(double[] vals)
        {
            var sb = new StringBuilder();
            int k = 0;
            foreach (var col in vals)
            {
                k++;
                string estr = col.ToString("F06");
                if (k == 1)
                    sb.Append(estr);
                else
                {
                    sb.Append(_loader.SplitSymbol);
                    sb.Append(estr);
                }
            }

            return sb;
        }

        static string CreateHeader()
        {
            if (_modifier==null)
            {
                var sb = new StringBuilder();
                int n = 0;
                foreach (var col in _loader.RowColumnByIdx.OrderBy(c => c.Key))
                {
                    n++;
                    string cname = col.Value;
                    if (n == 1)
                        sb.Append(cname);
                    else
                        sb.Append(_loader.SplitSymbol + cname);
                }
                return sb.ToString();
            }

            var hb = new StringBuilder();
            int s = 0;
            foreach (var col in _modifier.Fields.Values.OrderBy(c => c.Idx))
            {
                s++;
                string cname = col.Alias != null ? col.Alias : col.ExprStr.Replace(" ", "").Replace("[", "").Replace("]", "");
                if (s == 1)
                    hb.Append(cname);
                else
                    hb.Append(_loader.SplitSymbol + cname);
            }
            return hb.ToString();
        }

        private static double[] GetRowValues(DataRow<FType> row)
        {
            if (_modifier == null)
                return Array.ConvertAll(row.Values, x => (double)x);

            var vals = new Dictionary<string, double>();
            for (int i = 0; i < row.Values.Length; i++)
            {
                string colname = _loader.RowColumnByIdx[i];
                vals.Add(colname, row.Values[i]);
            }
            var mvals = _modifier.GetModifiedDataVector(vals);
            return mvals;
        }

    }
}
