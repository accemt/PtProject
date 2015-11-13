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

        public static string TargetField = "is_closed";

        public static void Load(string path, string target)
        {
            TargetField = target;
            Items = FactorStatItem.ParseFromFile(path);

            var tdList = new List<double>();
            var pdList = new List<double>();

            FactorDict = new Dictionary<string, Dictionary<string, FactorStatItem>>();
            foreach (var item in Items)
            {
                if (!FactorDict.ContainsKey(item.Factor1))
                    FactorDict.Add(item.Factor1, new Dictionary<string, FactorStatItem>());

                if (!FactorDict[item.Factor1].ContainsKey(item.Factor2))
                    FactorDict[item.Factor1].Add(item.Factor2, item);

                if (!FactorDict.ContainsKey(item.Factor2))
                    FactorDict.Add(item.Factor2, new Dictionary<string, FactorStatItem>());

                if (!FactorDict[item.Factor2].ContainsKey(item.Factor1))
                    FactorDict[item.Factor2].Add(item.Factor1, item);

                if (item.Factor1 == TargetField || item.Factor2 == TargetField)
                    tdList.Add(item.Chi2Coeff);
                else
                    pdList.Add(item.Chi2Coeff);
            }

            if (!FactorDict.ContainsKey(TargetField)) FactorDict.Add(TargetField, new Dictionary<string, FactorStatItem>());
            if (!FactorDict[TargetField].ContainsKey(TargetField)) FactorDict[TargetField].Add(TargetField, new FactorStatItem());

            FactorDict[TargetField][TargetField].Chi2Coeff = 0;

            SetVisibleFactors(FactorDict.Keys.ToArray());
        }

        private static void SetVisibleFactors(string[] factors)
        {
            VisibleFactors = factors.OrderByDescending(n => FactorDict[TargetField].ContainsKey(n) ? FactorDict[TargetField][n].Chi2Coeff : -1).ToArray();
        }

        public static void SelectFactors()
        {
            var flist = new List<string>();
            foreach (var f in FactorDict[TargetField].Keys)
            {
                if (ExcludeFactros.ContainsKey(f)) continue;

                if (FactorDict[TargetField][f].Chi2Coeff > TargDep)
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

                    double d1 = FactorDict[TargetField][f1].Chi2Coeff;
                    double d2 = FactorDict[TargetField][f2].Chi2Coeff;

                    double fdep = double.MaxValue;
                    if (FactorDict.ContainsKey(f1) && FactorDict[f1].ContainsKey(f2))
                        fdep = FactorDict[f1][f2].Chi2Coeff;

                    if (fdep > FactorDep)
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
            FactorManager.ExcludeFactros.Clear();
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (string line in text.Split('\n'))
            {
                if (line == null) continue;
                string nline = line.ToLower().Trim();
                if (string.IsNullOrWhiteSpace(nline)) continue;
                if (!FactorManager.ExcludeFactros.ContainsKey(nline))
                    FactorManager.ExcludeFactros.Add(nline, 1);
            }
        }
    }
}
