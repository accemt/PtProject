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
                Logger.Log("type can be only 'full' or 'short'");
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
            //loader.MaxRowsLoaded = 10000;
            if (targetname!=null) loader.RemoveSkipColumn(targetname);
            loader.Load(filename);
            var cols = loader.FileIdxByColumn.Keys.ToArray();

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
                    sw.WriteLine("Factor1;Factor2;src_cnt1;src_cnt2;mod_cnt1;mod_cnt2;src_chi2;src_chi2max;src_chi2coeff;mod_chi2;mod_chi2max;mod_chi2coeff;corr;corrabs;inf_val");

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

                        int col1idx = loader.RowIdxByColumn[col1];
                        int col2idx = loader.RowIdxByColumn[col2];

                        // просчитаны ли уже статиситки
                        bool stat1Exist = factorStatDict.ContainsKey(col1);
                        bool stat2Exist = factorStatDict.ContainsKey(col2);

                        // объекты статистик по признакам
                        var col1Stats = stat1Exist ? factorStatDict[col1].ModifiedStat : new Dictionary<FType, StatItem>();
                        var col2Stats = stat2Exist ? factorStatDict[col2].ModifiedStat : new Dictionary<FType, StatItem>();
                        var scol1Stats = stat1Exist ? factorStatDict[col1].SourceStat : new Dictionary<FType, StatItem>();
                        var scol2Stats = stat2Exist ? factorStatDict[col2].SourceStat : new Dictionary<FType, StatItem>();

                        var f1stat = stat1Exist ? factorStatDict[col1] : new FactorStat<FType>();
                        var f2stat = stat2Exist ? factorStatDict[col2] : new FactorStat<FType>();

                        // статистики по парам признаков
                        var commonStats = new Dictionary<TupleData, StatItem>(); // модифицированным
                        var scommonStats = new Dictionary<TupleData, StatItem>(); // исходным

                        // находим среднее, дисперсию и корреляцию по признакам
                        var colStats = PairStat<FType>.GetPairStat(loader, col1, col2);

                        int rowscount = loader.TotalDataLines; // всего строк
                        int allTargets = 0; // всего целевых строк

                        // собираем общую статистику по всем строкам
                        foreach (var row in loader.Rows)
                        {
                            // исходные признаки
                            FType fval1 = row.Values[col1idx];
                            FType fval2 = row.Values[col2idx];

                            // модифицированные признаки
                            FType val1 = (long)(Math.Round((fval1 - colStats.F1Avg) / colStats.F1Stddev * factor));
                            FType val2 = (long)(Math.Round((fval2 - colStats.F2Avg) / colStats.F2Stddev * factor));

                            if (!stat1Exist) // восможно уже просчитана
                            {
                                if (!col1Stats.ContainsKey(val1)) col1Stats.Add(val1, new StatItem());
                                var stat1 = col1Stats[val1];
                                stat1.Count++; // статистика встречаемости значений первого признака (модифицированного)
                                stat1.Targets += row.Target > 0 ? 1 : 0;

                                if (!scol1Stats.ContainsKey(fval1)) scol1Stats.Add(fval1, new StatItem());
                                var sstat1 = scol1Stats[fval1];
                                sstat1.Count++; // статистика встречаемости значений первого признака (исходного)
                                sstat1.Targets += row.Target > 0 ? 1 : 0;
                            }

                            if (!stat2Exist) // восможно уже просчитана
                            {
                                if (!col2Stats.ContainsKey(val2)) col2Stats.Add(val2, new StatItem());
                                var stat2 = col2Stats[val2];
                                stat2.Count++; // статистика встречаемости значений первого признака (модифицированного)
                                stat2.Targets += row.Target > 0 ? 1 : 0;

                                if (!scol2Stats.ContainsKey(fval2)) scol2Stats.Add(fval2, new StatItem());
                                var sstat2 = scol2Stats[fval2];
                                sstat2.Count++; // статистика встречаемости значений первого признака (исходного)
                                sstat2.Targets += row.Target > 0 ? 1 : 0;
                            }

                            allTargets += row.Target > 0 ? 1 : 0;

                            // статистики астречаемости пар признаков (модифицированные)
                            var tuple = new TupleData(new List<object> { val1, val2 });
                            if (!commonStats.ContainsKey(tuple)) commonStats.Add(tuple, new StatItem());
                            var stat = commonStats[tuple];
                            stat.Count++;  // пары признаков

                            // статистики астречаемости пар признаков (исходные)
                            var stuple = new TupleData(new List<object> { fval1, fval2 });
                            if (!scommonStats.ContainsKey(stuple)) scommonStats.Add(stuple, new StatItem());
                            var fstat = scommonStats[stuple];
                            fstat.Count++;  // пары признаков
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


                        // далее идет расчет вероятностей встречи признаков
                        if (!stat1Exist)
                        {
                            foreach (var v in col1Stats.Values)
                            {
                                // вероятность встретить значение первого признака
                                v.ItemProb = v.Count / (FType)rowscount;
                            }

                            foreach (var v in scol1Stats.Values)
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

                            foreach (var v in scol2Stats.Values)
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

                        foreach (var v in scommonStats.Values)
                        {
                            // вероятность встретить пару
                            v.ItemProb = v.Count / (FType)rowscount;
                        }


                        double chi2 = 0; // хи-квадрат по модифицированным признакам
                        double schi2 = 0; // хи-квадрат по исхдным признакам

                        // высчитываем статистики по модифицированным признакам
                        chi2 = GetChi2Stat(col1Stats, col2Stats, commonStats, rowscount);

                        // высчитываем статистики по исходным признакам
                        schi2 = GetChi2Stat(scol1Stats, scol2Stats, scommonStats, rowscount);

                        int cnt = (f1stat.ModifiedCount - 1) * (f2stat.ModifiedCount - 1);
                        int scnt = (f1stat.SourceCount - 1) * (f2stat.SourceCount - 1);

                        double chi2max = Util.InvChi2CDF(cnt, 0.95);
                        double schi2max = Util.InvChi2CDF(scnt, 0.95);
                        double chifactor = chi2 / chi2max;
                        double schifactor = schi2 / schi2max;


                        // information value
                        double iv = 0;
                        if (col1 == loader.TargetName || col2 == loader.TargetName)
                        {
                            if (col1 == loader.TargetName)
                                iv = GetInvormationValue(f2stat, allTargets, rowscount);
                            else
                                iv = GetInvormationValue(f1stat, allTargets, rowscount);
                        }

                        sw.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13};{14}",
                            col1,
                            col2,
                            f1stat.SourceCount,
                            f2stat.SourceCount,
                            f1stat.ModifiedCount,
                            f2stat.ModifiedCount,
                            schi2.ToString("F09", CultureInfo.InvariantCulture),
                            schi2max,
                            schifactor,
                            chi2.ToString("F09", CultureInfo.InvariantCulture),
                            chi2max,
                            chifactor,
                            colStats.Correlation.ToString(),
                            Math.Abs(Convert.ToDecimal(colStats.Correlation)).ToString(),
                            iv.ToString("F09", CultureInfo.InvariantCulture)
                        );
                        sw.Flush();

                        Logger.Log(col1 + "," + col2);
                    }
                }

                sw.Close();
            }
        }

        private static double GetInvormationValue(FactorStat<double> f2stat, int allTargets, int rowscount)
        {
            double iv = 0;

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

            return iv;
        }

        private static double GetChi2Stat(Dictionary<FType, StatItem> col1Stats,
            Dictionary<FType, StatItem> col2Stats,
            Dictionary<TupleData, StatItem> commonStats,
            int rowscount)
        {
            double chi2 = 0;

            foreach (var k1 in col1Stats.Keys)
            {
                foreach (var k2 in col2Stats.Keys)
                {
                    // составляем пару
                    var t = new TupleData(new List<object> { k1, k2 });
                    // количество пар (может быть нуль)
                    int pn = 0;
                    if (commonStats.ContainsKey(t))
                        pn = commonStats[t].Count;

                    // модифицированные признаки
                    double p1 = col1Stats[k1].ItemProb; // вероятность первого
                    double p2 = col2Stats[k2].ItemProb; // вероятность второго

                    // статиситка хи-квадрат
                    double chidiff = (pn - rowscount * p1 * p2);
                    chi2 += (chidiff * chidiff) / (rowscount * p1 * p2);
                }
            }

            return chi2;
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
