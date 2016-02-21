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
                _modifier = new DataModifier(File.ReadAllLines(ConfigPath));
                _sw = new StreamWriter(new FileStream(DataOutPath, FileMode.Create, FileAccess.Write), Encoding.GetEncoding(1251));
                string header = CreateHeader();
                _sw.WriteLine(header);

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
            // parse values
            var dataDict = new Dictionary<string, double>();
            for (int i = 0; i < row.Coeffs.Length; i++)
            {
                var fname = _loader.FileColumnByIdx[i];
                double dval = row.Coeffs[i];

                dataDict.Add(fname, dval);
            }

            // get modified values
            var vals = _modifier.GetModifiedDataDict(dataDict);

            // create modified values string
            var sb = new StringBuilder();
            int k = 0;
            foreach (var col in _modifier.Fields.Values.OrderBy(c => c.Idx))
            {
                k++;
                string estr = vals[col].ToString("F06");
                if (k == 1)
                    sb.Append(estr);
                else
                {
                    sb.Append(_loader.SplitSymbol);
                    sb.Append(estr);
                }
            }
            _sw.WriteLine(sb.ToString());

            _idx++;

            if (_idx % 12345 == 0)
            {
                _sw.Flush();
            }

            return _obj;
        }

        static string CreateHeader()
        {
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

    }
}
