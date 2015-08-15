using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Domain.Util
{
    public class ResultCalc
    {
        public static FinalFuncResult GetResult(IEnumerable<RocItem> rows, double prct)
        {
            int listLen = rows.Count();
            int nup = (int)(listLen * prct) + 1; // outflow set length

            int idx = 0;
            int tp = 0;
            int closed = 0;
            double loss = 0;
            double eps = 0.001;
            foreach (var item in rows)
            {
                idx++;
                //item.Predicted = idx <= nup ? 1 : 0;
                if (item.Predicted > 0 && item.Target > 0) tp++;
                if (item.Target > 0) closed++;

                double ypred = Math.Min(Math.Max(item.Prob, eps), 1 - eps);
                double yi = item.Target;
                double lpart = yi * Math.Log(ypred) + (1 - yi) * Math.Log(1 - ypred);
                loss += lpart;
            }

            var points = new List<RocPoint>();
            double auc = GetRocArea(rows, points, listLen);
            double recall = tp / (double)closed;
            double precision = tp / (double)nup;
            double logLoss = -loss / listLen;
            double fmeasure = 2*precision*recall/(precision + recall);

            var result = new FinalFuncResult();
            result.AUC = auc;
            result.Recall = recall;
            result.Precision = precision;
            result.LogLoss = logLoss;
            result.FMeasure = fmeasure;
            result.RocPoints = points;
            result.OutflowLength = nup;

            return result;
        }

        public static double GetRocArea(IEnumerable<RocItem> scoreList, List<RocPoint> points, int length)
        {
            if (scoreList==null || length == 0) return -1;

            double fp = 0;
            double tp = 0;

            double fpPrev = 0;
            double tpPrev = 0;

            int fPrev = -1000;

            double a = 0;

            int p = scoreList.Count(item => item.Target == 1);
            int n = length - p;

            foreach (var item in scoreList)
            {
                if (item.Target != fPrev)
                {
                    a += TRAPEZOID_AREA(fp, fpPrev, tp, tpPrev);
                    points.Add(new RocPoint(fp / n, tp / p));

                    fPrev = item.Target;
                    fpPrev = fp;
                    tpPrev = tp;
                }

                if (item.Target == 1)
                    tp++;
                else
                    fp++;
            }

            a += TRAPEZOID_AREA(n, fpPrev, p, tpPrev);
            points.Add(new RocPoint(fp / n, tp / p));

            var dA = (double)a;
            var dP = (double)p;
            var dN = (double)n;
            a = (double)(dA / (dP * dN));

            if (Math.Abs(a - 0) < 0.0000000000001) a = 1;

            return a;
        }

        private static double TRAPEZOID_AREA(double x1, double x2, double y1, double y2)
        {
            double Base = Math.Abs(x1 - x2);
            double height = (y1 + y2) / 2;
            return Base * height;
        }
    }

    public class RocItem
    {
        public double Prob;
        public int Target;
        public int Predicted;
        public string Id;

        public override string ToString()
        {
            return Prob.ToString("F03") + " -> " + Target;
        }
    }

    public class RocPoint
    {
        public double X;
        public double Y;

        public RocPoint(double _x, double _y)
        {
            X = _x;
            Y = _y;
        }
    }
}
