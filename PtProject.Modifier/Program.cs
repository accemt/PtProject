using PtProject.Domain.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PtProject.Modifier
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
            {
                Logger.Log("usage: program.exe <data_in.csv> <data_out.csv> [conf=conf.csv] ");
                return;
            }

            string dataInPath = args[0];
            string dataOutPath = args[1];
            string confPath = args.Length >=3 ? args[2] : "conf.csv";

            Logger.Log("data_in: " + dataInPath);
            Logger.Log("conf : " + confPath);
            Logger.Log("data_out: " + dataOutPath);

            try
            {
                string nextline = null;
                var sr = new StreamReader(new FileStream(dataInPath, FileMode.Open, FileAccess.Read), Encoding.GetEncoding(1251));
                var sw = new StreamWriter(new FileStream(dataOutPath, FileMode.Create, FileAccess.Write), Encoding.GetEncoding(1251));

                int idx = 0;
                var colByIdx = new Dictionary<int, string>();
                var idxByCol = new Dictionary<string, int>();

                var modifier = new DataModifier(File.ReadAllLines(confPath));

                while ((nextline = sr.ReadLine()) != null)
                {
                    string[] blocks = nextline.ToLower().Split(';');
                    idx++;
                    if (idx == 1) // header
                    {
                        for (int j = 0; j < blocks.Length; j++)
                        {
                            string rname = blocks[j].Trim();
                            if (!colByIdx.ContainsKey(j)) colByIdx.Add(j, rname);
                            if (!idxByCol.ContainsKey(rname)) idxByCol.Add(rname, j);
                        }

                        var hb = new StringBuilder();
                        int s = 0;
                        foreach (var col in modifier.Fields.Values.OrderBy(c=>c.Idx))
                        {
                            s++;
                            string cname = col.Alias!=null?col.Alias:col.ExprStr.Replace(" ", "").Replace("[", "").Replace("]", "");
                            if (s == 1)
                                hb.Append(cname);
                            else
                                hb.Append(';' + cname);
                        }
                        sw.WriteLine(hb.ToString());
                        continue;
                    }

                    // parse values
                    var dataDict = new Dictionary<string, double>();
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        var fname = colByIdx[i];
                        double dval = Convert.ToDouble(blocks[i]);

                        dataDict.Add(fname, dval);
                    }

                    // get modified values
                    var vals = modifier.GetDictData(dataDict);

                    // create modified values string
                    var sb = new StringBuilder();
                    int k = 0;
                    foreach (var col in modifier.Fields.Values.OrderBy(c => c.Idx))
                    {
                        k++;
                        string estr = vals[col.ExprStr].ToString("F06");
                        if (k == 1)
                            sb.Append(estr);
                        else
                        {
                            sb.Append(';');
                            sb.Append(estr);
                        }
                    }
                    sw.WriteLine(sb.ToString());

                    // flush data
                    if (idx % 12345 == 0)
                    {
                        Logger.Log(idx + " lines writed");
                        sw.Flush();
                        break;
                    }
                }

                sr.Close();
                sw.Close();

                Logger.Log(idx + " lines parsed");
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }
    }
}
