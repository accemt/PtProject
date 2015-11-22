using PtProject.Calibrator.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Calibrator
{
    public static class FactorManager
    {
        public static FactorStatItem[] Items { get; set; }
        public static Dictionary<string, Dictionary<string, FactorStatItem>> FactorDict { get; set; }
        public static string[] VisibleFactors { get; set; }
        public static Dictionary<string, int> ExcludeFactros = new Dictionary<string, int>();

        public static double TargDep = 1;
        public static double FactorDep = 1;

        public static string TargetField;
        public static string MeasureField;

        private static Dictionary<double, int> _tdList = new Dictionary<double, int>();
        private static Dictionary<double, int> _pdList = new Dictionary<double, int>();

        public static void Load(string path, string target= "target", string measureFiled= "Chi2Coeff")
        {
            TargetField = target;
            MeasureField = measureFiled;
            Items = FactorStatItem.ParseFromFile(path);

            FactorDict = new Dictionary<string, Dictionary<string, FactorStatItem>>();
            foreach (var item in Items)
            {
                double measure = GetMeasureValue(item);

                if (!FactorDict.ContainsKey(item.Factor1))
                    FactorDict.Add(item.Factor1, new Dictionary<string, FactorStatItem>());

                if (!FactorDict[item.Factor1].ContainsKey(item.Factor2))
                    FactorDict[item.Factor1].Add(item.Factor2, item);

                if (!FactorDict.ContainsKey(item.Factor2))
                    FactorDict.Add(item.Factor2, new Dictionary<string, FactorStatItem>());

                if (!FactorDict[item.Factor2].ContainsKey(item.Factor1))
                    FactorDict[item.Factor2].Add(item.Factor1, item);

                if (item.Factor1 == TargetField || item.Factor2 == TargetField)
                    if (!_tdList.ContainsKey(measure)) _tdList.Add(measure, 1); else _tdList[measure]++;
                else
                    if (!_pdList.ContainsKey(measure)) _pdList.Add(measure, 1); else _pdList[measure]++;
            }

            if (!FactorDict.ContainsKey(TargetField)) FactorDict.Add(TargetField, new Dictionary<string, FactorStatItem>());
            if (!FactorDict[TargetField].ContainsKey(TargetField)) FactorDict[TargetField].Add(TargetField, new FactorStatItem());

            //FactorDict[TargetField][TargetField].Chi2Coeff = 0;

            SetVisibleFactors(FactorDict.Keys.ToArray());
        }

        private static void SetVisibleFactors(string[] factors)
        {
            VisibleFactors = factors.OrderByDescending(n => FactorDict[TargetField].ContainsKey(n) ? GetMeasureValue(FactorDict[TargetField][n]) : -1).ToArray();
        }

        public static void SelectFactors()
        {
            var flist = new List<string>();
            foreach (var f in FactorDict[TargetField].Keys)
            {
                if (ExcludeFactros.ContainsKey(f)) continue;

                if (GetMeasureValue(FactorDict[TargetField][f]) >= TargDep)
                    flist.Add(f);
            }

            var farray = flist.ToArray();

            var nlist = new Dictionary<string, int>();
            var droplist = new Dictionary<string, int>();
            for (int i = 0; i < farray.Length - 1; i++)
            {
                for (int j = i + 1; j < farray.Length; j++)
                {
                    string f1 = farray[i];
                    string f2 = farray[j];

                    if (ExcludeFactros.ContainsKey(f1)) continue;
                    if (ExcludeFactros.ContainsKey(f2)) continue;

                    if (droplist.ContainsKey(f1)) continue;
                    if (droplist.ContainsKey(f2)) continue;

                    double d1 = GetMeasureValue(FactorDict[TargetField][f1]);
                    double d2 = GetMeasureValue(FactorDict[TargetField][f2]);

                    double fdep = double.MinValue;
                    if (FactorDict.ContainsKey(f1) && FactorDict[f1].ContainsKey(f2))
                        fdep = GetMeasureValue(FactorDict[f1][f2]);

                    if (fdep >= FactorDep)
                    {
                        if (d1 > d2)
                        {
                            if (!nlist.ContainsKey(f1)) nlist.Add(f1, 0);
                            if (!droplist.ContainsKey(f2)) droplist.Add(f2, 0);
                        }
                        else
                        {
                            if (!nlist.ContainsKey(f2)) nlist.Add(f2, 0);
                            if (!droplist.ContainsKey(f1)) droplist.Add(f1, 0);
                        }
                    }
                    else
                    {
                        if (!nlist.ContainsKey(f1)) nlist.Add(f1, 0);
                        if (!nlist.ContainsKey(f2)) nlist.Add(f2, 0);
                    }
                }
            }

            SetVisibleFactors(nlist.Keys.ToArray());
        }

        public static void SetExcludeFactors(string text)
        {
            ExcludeFactros.Clear();
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (string line in text.Split('\n'))
            {
                if (line == null) continue;
                string nline = line.ToLower().Trim();
                if (string.IsNullOrWhiteSpace(nline)) continue;
                if (!ExcludeFactros.ContainsKey(nline))
                    ExcludeFactros.Add(nline, 1);
            }
        }

        private static double GetMeasureValue(object obj)
        {
            return Convert.ToDouble(obj.GetType().GetField(MeasureField).GetValue(obj));
        }

        public static double[] GetTargetValues()
        {
            return _tdList.Keys.ToArray();
        }
    }
}
