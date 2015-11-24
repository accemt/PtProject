using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PtProject.Domain;
using PtProject.Domain.Util;
using PtProject.Loader;
using System.Globalization;

using FType = System.Double;

namespace PtProject.Dependency
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length <= 1 || args.Length >= 5)
            {
                Logger.Log("usage: program.exe <datafile.csv> <full/short> [target_name [factor=1.0]]");
                return;
            }

            string filename = args[0];
            string stype = args[1].ToLower();
            string targetname = args.Length >= 3 ? args[2] : null;

            // множетель преобразования для категорирования признаков
            double factor = double.Parse(args.Length >= 4 ? args[3].Replace(',', '.') : "1", CultureInfo.InvariantCulture);

            if (stype!="full" && stype!="short")
            {
                Logger.Log("type can be only full or short");
                return;
            }

            if (stype=="short" && targetname==null)
            {
                Logger.Log("you must specify target_name in sort mode");
                return;
            }

            Logger.Log("datafile = " + filename);
            Logger.Log("type = " + stype);
            Logger.Log("target_name = " + targetname);
            Logger.Log("factor = " + factor.ToString("F04"));

            if (!File.Exists(filename))
            {
                Logger.Log("file " + filename + " not found");
                return;
            }

            // загружаем данные
            var loader = targetname!=null?(new DataLoader<FType>(targetname)) : new DataLoader<FType>();
            if (targetname!=null) loader.RemoveSkipColumn(targetname);
            loader.Load(filename);
            var cols = loader.IdxByColumn.Keys.ToArray();

            // выходной файл
            string statname = filename + "_stats.csv";

            // если часть данных уже просчитана, смотрим какая, чтобы повторно не считать
            var counted = LoadCountedData(statname);

            // просчитанная статистика по признакам
            var factorStatDict = new Dictionary<string, FactorStat<FType>>();

            // начинаем просчет
            using (var sw = new StreamWriter(new FileStream(statname, counted.Count > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write),
                                      Encoding.UTF8))
            {
                if (counted.Count == 0)
                    sw.WriteLine("Factor1;Factor2;src_cnt1;src_cnt2;mod_cnt1;mod_cnt2;s1;s2;s3;chi2;chi2max;chi2coeff;corr;corrabs;inf_val");

                for (int i = 0; i < cols.Length - 1; i++)
                {
                    for (int j = i + 1; j < cols.Length; j++)
                    {
                        var col1 = cols[i]; // первый признак
                        var col2 = cols[j]; // второй признак

                        if (stype == "short")
                        {
                            if (targetname != null)
                                if (col1 != loader.TargetName && col2 != loader.TargetName) continue;
                        }

                        if (counted.ContainsKey(col1) && counted[col1].ContainsKey(col2)) continue;

                        int col1idx = loader.IdxByColumn[col1];
                        int col2idx = loader.IdxByColumn[col2];

                        // просчитаны ли уже статиситки
                        bool stat1Exist = factorStatDict.ContainsKey(col1);
                        bool stat2Exist = factorStatDict.ContainsKey(col2);

                        // объекты статистик по признакам
                        var col1Stats = stat1Exist ? factorStatDict[col1].ModifiedStat : new Dictionary<long, StatItem<FType>>();
                        var col2Stats = stat2Exist ? factorStatDict[col2].ModifiedStat : new Dictionary<long, StatItem<FType>>();
                        var scol1Stats = stat1Exist ? factorStatDict[col1].SourceStat : new Dictionary<FType, StatItem<FType>>();
                        var scol2Stats = stat2Exist ? factorStatDict[col2].SourceStat : new Dictionary<FType, StatItem<FType>>();

                        var f1stat = stat1Exist ? factorStatDict[col1] : new FactorStat<FType>();
                        var f2stat = stat2Exist ? factorStatDict[col2] : new FactorStat<FType>();

                        var commonStats = new Dictionary<TupleData, StatItem<FType>>();

                        int allTargets = 0; // всего ненулевых целевых значений

                        // сумма значений для поиска среднего
                        FType sum1 = 0;
                        FType sum2 = 0;

                        int rowscount = loader.Rows.Count; // всего строк

                        // сначала находим матожидание
                        foreach (var row in loader.Rows)
                        {
                            FType fval1 = row.Coeffs[col1idx];
                            FType fval2 = row.Coeffs[col2idx];

                            sum1 += fval1;
                            sum2 += fval2;
                        }

                        f1stat.Avg = sum1 / rowscount; // среднее по первому признаку
                        f2stat.Avg = sum2 / rowscount; // среднее по второму признаку

                        // теперь находим дисперсию и корреляцию
                        FType ds1 = 0;
                        FType ds2 = 0;
                        FType cov = 0;
                        FType disp1 = 0;
                        FType disp2 = 0;
                        foreach (var row in loader.Rows)
                        {
                            ds1 = (row.Coeffs[col1idx] - f1stat.Avg);
                            disp1 += ds1 * ds1;

                            ds2 = (row.Coeffs[col2idx] - f2stat.Avg);
                            disp2 += ds2 * ds2;

                            cov += ds1 * ds2;
                        }

                        f1stat.Stddev = (FType)Math.Sqrt(disp1 / ((double)rowscount - 1));
                        f2stat.Stddev = (FType)Math.Sqrt(disp2 / ((double)rowscount - 1));

                        FType div = (FType)(Math.Sqrt(disp1 * disp2));

                        FType? corr = null; // коэффициент корреляции
                        if (Math.Abs(div) > 0.000000000000001) corr = cov / div;

                        // собираем общую статистику по всем строкам
                        foreach (var row in loader.Rows)
                        {
                            FType fval1 = row.Coeffs[col1idx];
                            FType fval2 = row.Coeffs[col2idx];

                            long val1 = (long)(Math.Round((fval1 - f1stat.Avg) / f1stat.Stddev * factor));
                            long val2 = (long)(Math.Round((fval2 - f2stat.Avg) / f2stat.Stddev * factor));

                            if (!stat1Exist) // восможно уже просчитана
                            {
                                if (!col1Stats.ContainsKey(val1)) col1Stats.Add(val1, new StatItem<FType>());
                                var stat1 = col1Stats[val1];
                                stat1.Count++; // статистика встречаемости значений первого признака (модифицированного)
                                stat1.Targets += row.Target > 0 ? 1 : 0;

                                if (!scol1Stats.ContainsKey(fval1)) scol1Stats.Add(fval1, new StatItem<FType>());
                                var sstat1 = scol1Stats[fval1];
                                sstat1.Count++; // статистика встречаемости значений первого признака (исходного)
                                sstat1.Targets += row.Target > 0 ? 1 : 0;
                            }

                            if (!stat2Exist) // восможно уже просчитана
                            {
                                if (!col2Stats.ContainsKey(val2)) col2Stats.Add(val2, new StatItem<FType>());
                                var stat2 = col2Stats[val2];
                                stat2.Count++; // статистика встречаемости значений первого признака (модифицированного)
                                stat2.Targets += row.Target > 0 ? 1 : 0;

                                if (!scol2Stats.ContainsKey(fval2)) scol2Stats.Add(fval2, new StatItem<FType>());
                                var sstat2 = scol2Stats[fval2];
                                sstat2.Count++; // статистика встречаемости значений первого признака (исходного)
                                sstat2.Targets += row.Target > 0 ? 1 : 0;
                            }

                            allTargets += row.Target > 0 ? 1 : 0;

                            var tuple = new TupleData(new List<object> { val1, val2 });
                            if (!commonStats.ContainsKey(tuple)) commonStats.Add(tuple, new StatItem<FType>());
                            var stat = commonStats[tuple];
                            stat.Count++;  // пары признаков
                        }

                        // сохраняем расчитанные признаки
                        if (!stat1Exist)
                        {
                            f1stat.ModifiedStat = col1Stats;
                            f1stat.SourceStat = scol1Stats;
                            f1stat.ModifiedCount = col1Stats.Count;
                            f1stat.SourceCount = scol1Stats.Count;
                        }
                        if (!stat2Exist)
                        {
                            f2stat.ModifiedStat = col2Stats;
                            f2stat.SourceStat = scol2Stats;
                            f2stat.ModifiedCount = col2Stats.Count;
                            f2stat.SourceCount = scol2Stats.Count;
                        }


                        // далее идет расчет зависимостей признаков

                        if (!stat1Exist)
                        {
                            foreach (var v in col1Stats.Values)
                            {
                                // вероятность встретить значение первого признака
                                v.ItemProb = v.Count / (FType)rowscount;
                            }
                        }

                        if (!stat2Exist)
                        {
                            foreach (var v in col2Stats.Values)
                            {
                                // вероятность встретить значение второго признака
                                v.ItemProb = v.Count / (FType)rowscount;
                            }
                        }

                        foreach (var v in commonStats.Values)
                        {
                            // вероятность встретить пару
                            v.ItemProb = v.Count / (FType)rowscount;
                        }

                        double s1 = 0;
                        double s2 = 0;
                        double s3 = 0;
                        double chi2 = 0;

                        // берем по каждой паре признаков
                        foreach (var k1 in col1Stats.Keys)
                        {
                            foreach (var k2 in col2Stats.Keys)
                            {
                                double p1 = col1Stats[k1].ItemProb; // вероятность первого
                                double p2 = col2Stats[k2].ItemProb; // вероятность второго

                                // составляем пару
                                var t = new TupleData(new List<object> { k1, k2 });

                                // вероятность пары (может быть нулевой)
                                double p = commonStats.ContainsKey(t) ? commonStats[t].ItemProb : 0;

                                // количество пар (может быть нуль)
                                int pn = commonStats.ContainsKey(t) ? commonStats[t].Count : 0;

                                // разница между вероятностью встретьи пару и вероятностью встретить первый * второй
                                double diff = Module(p1 * p2 - p);

                                // статиситка хи-квадрат
                                double chidiff = (pn - rowscount * p1 * p2);

                                s1 += diff;
                                s2 += diff * p;

                                chi2 += (chidiff * chidiff) / (rowscount * p1 * p2);
                            }
                        }

                        int cnt = f1stat.ModifiedCount * f2stat.ModifiedCount;
                        int cnt2 = (f1stat.ModifiedCount - 1) * (f2stat.ModifiedCount - 1);
                        s3 = s1 / cnt;
                        double chi2max = Util.InvChi2CDF(cnt2, 0.95);
                        double chifactor = chi2 / chi2max;


                        // information value
                        double iv = 0;
                        if (col1 == loader.TargetName || col2==loader.TargetName)
                        {
                            if (col1 == loader.TargetName)
                            {
                                foreach (long v2 in f2stat.ModifiedStat.Keys)
                                {
                                    double tprob = f2stat.ModifiedStat[v2].Targets / (double)allTargets;
                                    double ntprob = (f2stat.ModifiedStat[v2].Count - f2stat.ModifiedStat[v2].Targets) / (double)(rowscount - allTargets);
                                    double woe = Math.Log(tprob / ntprob) * 100;
                                    double ivp = (tprob * 100 - ntprob * 100) * Math.Log(tprob / ntprob);
                                    if (ntprob < 0.00000000000001 || tprob < 0.00000000000001)
                                    {
                                        woe = 0;
                                        ivp = 0;
                                    }
                                    iv += ivp;
                                }
                            }
                            else
                            {
                                foreach (long v1 in f1stat.ModifiedStat.Keys)
                                {
                                    double tprob = f1stat.ModifiedStat[v1].Targets / (double)allTargets;
                                    double ntprob = (f1stat.ModifiedStat[v1].Count - f1stat.ModifiedStat[v1].Targets) / (double)(rowscount - allTargets);
                                    double woe = Math.Log(tprob / ntprob) * 100;
                                    double ivp = (tprob * 100 - ntprob * 100) * Math.Log(tprob / ntprob);
                                    if (ntprob < 0.00000000000001 || tprob < 0.00000000000001)
                                    {
                                        woe = 0;
                                        ivp = 0;
                                    }
                                    iv += ivp;
                                }
                            }
                        }

                        sw.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13};{14}",
                            col1,
                            col2,
                            f1stat.SourceCount,
                            f2stat.SourceCount,
                            f1stat.ModifiedCount,
                            f2stat.ModifiedCount,
                            s1.ToString("F09", CultureInfo.InvariantCulture),
                            s2.ToString("F09", CultureInfo.InvariantCulture),
                            s3.ToString("F09", CultureInfo.InvariantCulture),
                            chi2.ToString("F09", CultureInfo.InvariantCulture),
                            chi2max,
                            chifactor,
                            corr.ToString(),
                            Math.Abs(Convert.ToDecimal(corr)).ToString(),
                            iv.ToString("F09", CultureInfo.InvariantCulture)
                        );
                        sw.Flush();

                        Logger.Log(col1 + "," + col2);
                    }
                }

                sw.Close();
            }
        }

        private static Dictionary<string, Dictionary<string, int>> LoadCountedData(string statname)
        {
            var counted = new Dictionary<string, Dictionary<string, int>>();

            bool statexist = File.Exists(statname);

            if (statexist)
            {
                foreach (string line in File.ReadAllLines(statname))
                {
                    if (line.Contains("col")) continue;
                    string[] blocks = line.Split(';');
                    string col1 = blocks[0];
                    string col2 = blocks[1];
                    if (!counted.ContainsKey(col1))
                        counted.Add(col1, new Dictionary<string, int>());
                    if (!counted[col1].ContainsKey(col2))
                        counted[col1].Add(col2, 1);
                }
            }
            return counted;
        }

        private static double Module(double p)
        {
            return Math.Abs(p);
        }
    }
}
