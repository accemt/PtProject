using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Calibrator.Domain
{
    public class FactorStatItem
    {
        public string Factor1;
        public string Factor2;

        public double S1;
        public double S2;
        public double S3;
        public double Chi2;
        public double Chi2Coeff;
        public double CorrAbs;
        public double InfValue;

        public override string ToString()
        {
            return Factor1 + " " + Factor2 + ": " + Chi2Coeff;
        }

        public static FactorStatItem[] ParseFromFile(string path)
        {
            var results = new List<FactorStatItem>();
            string[] lines = File.ReadAllLines(path);

            var idxByName = new Dictionary<string, int>();
            var nameByIdx = new Dictionary<int, string>();

            int idx = 0;
            foreach (var line in lines)
            {
                idx++;

                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] blocks = line.ToLower().Split(';');

                if (idx == 1) //header
                {
                    for (int j = 0; j < blocks.Length; j++)
                    {
                        string lname = blocks[j].Trim();

                        if (!idxByName.ContainsKey(lname))
                            idxByName.Add(lname, j);
                    }
                    continue;
                }

                var item = new FactorStatItem();
                item.Factor1 = GetStringValue(blocks, "Factor1", idxByName);
                item.Factor2 = GetStringValue(blocks, "Factor2", idxByName);

                item.S1 = GetDoubleValue(blocks, "S1", idxByName);
                item.S2 = GetDoubleValue(blocks, "S2", idxByName);
                item.S3 = GetDoubleValue(blocks, "S3", idxByName);
                item.Chi2 = GetDoubleValue(blocks, "Chi2", idxByName);
                item.Chi2Coeff = GetDoubleValue(blocks, "Chi2Coeff", idxByName);
                item.CorrAbs = GetDoubleValue(blocks, "CorrAbs", idxByName);
                item.InfValue = GetDoubleValue(blocks, "InfValue", idxByName);

                results.Add(item);
            }

            return results.ToArray();
        }

        private static string GetStringValue(string[] blocks, string name, Dictionary<string, int> idxByName)
        {
            if (name == null) return null;
            string lname = name.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(lname)) return null;

            if (!idxByName.ContainsKey(lname)) return null;

            return blocks[idxByName[lname]];
        }

        private static double GetDoubleValue(string[] blocks, string name, Dictionary<string, int> idxByName)
        {
            if (name == null) return 0;
            string lname = name.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(lname)) return 0;

            if (!idxByName.ContainsKey(lname)) return 0;

            double res;
            if (!double.TryParse(blocks[idxByName[lname]], out res)) return 0;

            return res;
        }
    }
}
