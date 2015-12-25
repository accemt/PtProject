using PtProject.Domain;
using PtProject.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Dependency
{
    public class PairStat<T>
    {
        public double F1Avg { get; set; }
        public double F1Stddev { get; set; }

        public double F2Avg { get; set; }
        public double F2Stddev { get; set; }
    
        public double? Correlation;

        public static PairStat<T> GetPairStat(DataLoader<T> loader, string col1, string col2)
        {
            var result = new PairStat<T>();

            int col1idx = loader.RowIdxByColumn[col1];
            int col2idx = loader.RowIdxByColumn[col2];

            int rowscount = loader.Rows.Count; // всего строк
            double sum1 = 0;
            double sum2 = 0;

            // сначала находим матожидание
            foreach (var row in loader.Rows)
            {
                T fval1 = row.Coeffs[col1idx];
                T fval2 = row.Coeffs[col2idx];

                sum1 += Convert.ToDouble(fval1);
                sum2 += Convert.ToDouble(fval2);
            }

            result.F1Avg = sum1 / rowscount; // среднее по первому признаку
            result.F2Avg = sum2 / rowscount; // среднее по второму признаку

            // теперь находим дисперсию и корреляцию
            double ds1 = 0;
            double ds2 = 0;
            double cov = 0;
            double disp1 = 0;
            double disp2 = 0;
            foreach (var row in loader.Rows)
            {
                ds1 = (Convert.ToDouble(row.Coeffs[col1idx]) - result.F1Avg);
                disp1 += ds1 * ds1;

                ds2 = (Convert.ToDouble(row.Coeffs[col2idx]) - result.F2Avg);
                disp2 += ds2 * ds2;

                cov += ds1 * ds2;
            }

            result.F1Stddev = Math.Sqrt(disp1 / ((double)rowscount - 1));
            result.F2Stddev = Math.Sqrt(disp2 / ((double)rowscount - 1));

            double div = Math.Sqrt(disp1 * disp2);

            double? corr = null; // коэффициент корреляции
            if (Math.Abs(div) > 0.000000000000001) corr = cov / div;

            result.Correlation = corr;

            return result;
        }
    }
}
