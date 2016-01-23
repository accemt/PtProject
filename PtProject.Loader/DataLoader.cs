using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using PtProject.Domain;
using PtProject.Domain.Util;
using System.Configuration;

namespace PtProject.Loader
{
    public class DataLoader<T> : DataLoaderBase
    {
        /// <summary>
        /// // By default loading strategy data will be load in that list
        /// </summary>
        public List<DataRow<T>> Rows = new List<DataRow<T>>();

        /// <summary>
        /// For machine-learning data will be load in that array
        /// </summary>
        public T[,] LearnRows;

        /// <summary>
        /// That function will be use insted parsing row
        /// </summary>
        public Func<DataRow<T>, object> ProceedRowFunc;

        /// <summary>
        /// Targets values probability
        /// </summary>
        public Dictionary<T, double> TargetProb = new Dictionary<T, double>();

        /// <summary>
        /// Summary counts for target variable
        /// </summary>
        public SortedDictionary<T, int> TargetStat = new SortedDictionary<T, int>();

        /// <summary>
        /// target variable will be encoded by classes
        /// that dict used to get class id by target var value
        /// </summary>
        public Dictionary<T, int> ClassNumByValue = new Dictionary<T, int>();

        /// <summary>
        /// target variable will be encoded by classes
        /// that dict used to get target var value by class id
        /// </summary>
        public Dictionary<int, T> ValueByClassNum = new Dictionary<int, T>();

        /// <summary>
        /// variables count distiribution
        /// </summary>
        public Dictionary<string, Dictionary<T, int>> VarsDistr = new Dictionary<string, Dictionary<T, int>>();

        /// <summary>
        /// if true stats will collect to VarsDistr
        /// </summary>
        public bool CollectDistrStat = false;

        /// <summary>
        /// id indexes (many)
        /// </summary>
        private SortedDictionary<int, int> _idIdx = new SortedDictionary<int, int>();

        public DataLoader()
        {
            Ids = new Dictionary<string, int>();

            SplitSymbol = ';';
            var cval = ConfigReader.Read("SplitSymbol");
            if (cval!=null)
                SplitSymbol = cval[0];

            DateFormat = "yyyy-MM-dd";
            var dfval = ConfigReader.Read("DateFormat");
            if (dfval != null)
                DateFormat = dfval;
        }

        public DataLoader(string target) : this()
        {
            AddTargetColumn(target);
        }

        public DataLoader(string target, string id) : this()
        {
            AddTargetColumn(target);
            AddIdColumn(id);
        }

        public void Load(string filename)
        {
            try
            {
                if (IsLoadForLearning)
                    TotalDataLines = GetDataLinesCount(filename);


                using (var sr = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read), Encoding.GetEncoding(1251)))
                {
                    int idx = 0;
                    int nrow = 0;
                    string nextline;
                    int classNum = 0;
                    while ((nextline = sr.ReadLine()) != null)
                    {
                        idx++;
                        if (string.IsNullOrWhiteSpace(nextline)) continue;
                        string[] blocks = GetStringBlocks(nextline);

                        // header row
                        if (idx == 1)
                        {
                            for (int i = 0; i < blocks.Length; i++)
                            {
                                string cname = blocks[i]; // column name

                                if (!FileColumnByIdx.ContainsKey(i))
                                    FileColumnByIdx.Add(i, cname);
                                if (!FileIdxByColumn.ContainsKey(cname))
                                    FileIdxByColumn.Add(cname, i);
                                else
                                    Logger.Log("duplicate column name: " + cname + ", exiting");
                            }

                            if (TargetName != null)
                            {
                                if (!FileIdxByColumn.ContainsKey(TargetName))
                                {
                                    Logger.Log("data don`t have a target (" + TargetName + ") column, exiting");
                                    break;
                                }
                            }

                            if (TargetName != null)
                                TargetIdx = FileIdxByColumn[TargetName]; // target column index

                            // id columns
                            foreach (var iname in Ids.Keys)
                            {
                                if (!FileIdxByColumn.ContainsKey(iname))
                                {
                                    throw new InvalidDataException("id column '" + iname + "' not found");
                                }
                                int sidx = FileIdxByColumn[iname];
                                if (!_idIdx.ContainsKey(sidx)) _idIdx.Add(sidx, 1);
                            }
                            if (Ids.Count>0)
                            {
                                IdName = GetStringId(Ids.Keys.ToArray());
                            }

                            // skip columns
                            var toDel = (from t in SkippedColumns.Keys where !FileIdxByColumn.ContainsKey(t) select t).ToList();
                            toDel.ForEach(c => SkippedColumns.Remove(c));

                            // count of variables except skipped
                            NVars = FileColumnByIdx.Count - SkippedColumns.Count;

                            continue;
                        }

                        // data row
                        nrow++;

                        if (blocks.Length > FileIdxByColumn.Count)
                        {
                            Logger.Log("error parsing row #" + nrow);
                            continue;
                        }

                        if (RandomGen.GetDouble() >= LoadFactor) continue;

                        var row = new DataRow<T>();

                        // parse target 
                        if (TargetName != null) row.Target = ParseValue(blocks[TargetIdx]);

                        // creating composite id
                        row.Id = GetStringId(blocks);
                        if (string.IsNullOrEmpty(row.Id)) row.Id = nrow.ToString(); //using row_number if ids not set

                        // save stats for target value
                        if (!TargetStat.ContainsKey(row.Target))
                            TargetStat.Add(row.Target, 0);
                        TargetStat[row.Target]++;   // count by target

                        // class id by target
                        if (!ClassNumByValue.ContainsKey(row.Target))
                            ClassNumByValue.Add(row.Target, classNum++);

                        // target by class id
                        if (!ValueByClassNum.ContainsKey(ClassNumByValue[row.Target]))
                            ValueByClassNum.Add(ClassNumByValue[row.Target], row.Target); 


                        // --------------------------- loading for learning -------------------------------
                        if (IsLoadForLearning)
                        {
                            if (LearnRows == null)
                                LearnRows = new T[TotalDataLines, NVars + 1]; // all variables +1 for target

                            for (int i = 0, k = 0; i < blocks.Length; i++)
                            {
                                string cval = blocks[i];
                                string colname = FileColumnByIdx[i];
                                if (SkippedColumns.ContainsKey(colname))
                                    continue;

                                T pval = ParseValue(cval);
                                LearnRows[nrow - 1, k++] = pval;
                                SaveVarDistr(colname, pval);
                            }
                            LearnRows[nrow - 1, NVars] = row.Target;
                        }
                        else
                        {
                            // --------------------------- loading for analyse -----------------------------------
                            var carray = new T[NVars];

                            for (int i = 0, k = 0; i < blocks.Length; i++)
                            {
                                string cval = blocks[i];
                                if (FileColumnByIdx.ContainsKey(i))
                                {
                                    string colname = FileColumnByIdx[i];
                                    if (SkippedColumns.ContainsKey(colname))
                                        continue;

                                    if (!RowColumnByIdx.ContainsKey(k))
                                        RowColumnByIdx.Add(k, colname);

                                    if (!RowIdxByColumn.ContainsKey(colname))
                                        RowIdxByColumn.Add(colname,k);

                                    T pval = ParseValue(cval);
                                    carray[k] = pval;
                                    k++;
                                    SaveVarDistr(colname, pval);
                                }
                                else
                                {
                                    Logger.Log("error parsing id=" + row.Id);
                                }
                            }

                            row.Coeffs = carray;
                            if (ProceedRowFunc == null) // don't save row in case of ProceedRowFunc not null
                                Rows.Add(row);
                            else
                                ProceedRowFunc(row);

                            TotalDataLines++;
                        }

                        if (idx % 12345 == 0) Logger.Log(idx + " lines loaded");
                        if (MaxRowsLoaded != 0 && idx > MaxRowsLoaded) break;
                    }

                    GetTargetProbs();
                    Logger.Log((idx - 1) + " lines loaded;");
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
                throw e;
            }
        }

        private void GetTargetProbs()
        {
            int allcnt = 0;

            foreach (var key in TargetStat.Keys)
            {
                allcnt += TargetStat[key];
            }

            if (allcnt == 0) return;

            foreach (var key in TargetStat.Keys)
            {
                TargetProb.Add(key, TargetStat[key]/(double)allcnt);
            }
        }

        private void SaveVarDistr(string colname, T pval)
        {
            if (CollectDistrStat)
            {
                if (!VarsDistr.ContainsKey(colname))
                    VarsDistr.Add(colname, new Dictionary<T, int>());
                if (!VarsDistr[colname].ContainsKey(pval))
                    VarsDistr[colname].Add(pval, 0);
                VarsDistr[colname][pval]++;
            }
        }

        private string[] GetStringBlocks(string nextline)
        {
            string[] blocks;
            string modstring = RemoveSplitterFromString(nextline);
            if (SplitSymbol != ',')
                blocks = modstring.ToLower().Replace(',', '.').Split(SplitSymbol);
            else
                blocks = modstring.ToLower().Split(SplitSymbol);
            if (blocks != null)
            {
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (blocks[i] == null) continue;
                    blocks[i] = blocks[i].Trim('"');
                }
            }

            return blocks;
        }

        private string RemoveSplitterFromString(string nextline)
        {
            bool insub = false;
            int len = nextline.Length;
            var sb = new StringBuilder();
            for (int i=0;i<len;i++)
            {
                if (nextline[i] == '\"' && !insub)
                {
                    insub = true;
                    continue;
                }
                if (nextline[i] == '\"' && insub)
                {
                    insub = false;
                    continue;
                }
                if (nextline[i] == SplitSymbol && insub)
                    sb.Append(' ');
                else
                    sb.Append(nextline[i]);

            }
            return sb.ToString();
        }

        private string GetStringId(string[] blocks)
        {
            var sb = new StringBuilder();
            int nidx = 0;
            foreach (var sidx in _idIdx.Keys)
            {
                if (nidx == 0)
                    sb.Append(blocks[sidx]);
                else
                    sb.Append(";" + blocks[sidx]);
                nidx++;
            }
            return sb.ToString();
        }

        private int GetDataLinesCount(string filename)
        {
            int result = 0;
            try
            {
                using (var sr = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read), Encoding.GetEncoding(1251)))
                {
                    string nextline = null;
                    int idx = 0;
                    while ((nextline = sr.ReadLine()) != null)
                    {
                        idx++;
                        if (idx == 1) continue; // skip header

                        if (!string.IsNullOrWhiteSpace(nextline))
                            result++;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }

            return result;
        }

        private T ParseValue(string str)
        {
            double val;
            bool isok = double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
            T fval = default(T);
            if (!isok)
            {
                DateTime dval;
                bool dateok = DateTime.TryParseExact(str, DateFormat, null, DateTimeStyles.None, out dval);

                if (dateok)
                {
                    DateTime def = new DateTime();
                    val = (dval - def).TotalDays;
                }
                else
                {
                    if (!StringValues.ContainsKey(str))
                    {
                        int ival = -1;
                        if (!string.IsNullOrWhiteSpace(str))
                        {
                            var md5hash = ComputeMD5Hash(GetBytes(str));
                            var hash4 = Compute4BytesHash(md5hash);
                            ival = (ushort)(BitConverter.ToInt32(md5hash, 0));
                        } 
                        StringValues.Add(str, ival);
                    }
                    val = StringValues[str];
                }
            }
            fval = (T)Convert.ChangeType(val, typeof(T));
            return fval;
        }

        private byte[] Compute4BytesHash(byte[] data)
        {
            if (data.Length <= 4) return data;
            byte[] sarr = new byte[4];
            int slen = data.Length / 4;
            for (int i=0;i<slen;i++)
            {
                sarr[0] ^= i + 0 < data.Length ? data[i + 0] : (byte)0;
                sarr[1] ^= i + 1 < data.Length ? data[i + 1] : (byte)0;
                sarr[2] ^= i + 2 < data.Length ? data[i + 2] : (byte)0;
                sarr[3] ^= i + 3 < data.Length ? data[i + 3] : (byte)0;
            }

            return sarr;
        }

        static System.Security.Cryptography.MD5 md5Algorithm = System.Security.Cryptography.MD5.Create();
        static byte[] ComputeMD5Hash(byte[] data)
        {
            return md5Algorithm.ComputeHash(data);
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }


        /// <summary>
        /// Id column by default skipped and can be multiply
        /// </summary>
        /// <param name="col"></param>
        public void AddIdColumn(string col)
        {
            string ncol = col.ToLower();

            if (!Ids.ContainsKey(ncol)) Ids.Add(ncol.ToLower(), 1);
            AddSkipColumn(ncol);
        }

        public void AddIdsString(string ids)
        {
            string[] blocks = ids.Split(',');
            foreach (string b in blocks)
            {
                if (string.IsNullOrWhiteSpace(b)) continue;
                AddIdColumn(b);
            }
        }

        public void AddSkipColumns(string skipColumns)
        {
            string[] blocks = skipColumns.Split(',');
            foreach (string b in blocks)
            {
                if (string.IsNullOrWhiteSpace(b)) continue;
                AddSkipColumn(b);
            }
        }

        /// <summary>
        /// Get all loaded rows
        /// </summary>
        /// <returns></returns>
        public override List<DataRow<object>> GetRows()
        {
            var list = new List<DataRow<object>>();
            foreach (var r in Rows)
            {
                list.Add((DataRow<object>)r);
            }
            return list;
        }

        /// <summary>
        /// Template type, double by default
        /// </summary>
        /// <returns></returns>
        public override Type GetItemType()
        {
            return typeof(T);
        }

        /// <summary>
        /// Target column by default skipped and can be only one
        /// </summary>
        /// <param name="col"></param>
        public void AddTargetColumn(string col)
        {
            string ncol = col.ToLower();
            TargetName = ncol;
            AddSkipColumn(ncol);
        }

        public void AddSkipColumn(string col)
        {
            string ncol = col.ToLower();

            if (string.IsNullOrWhiteSpace(ncol)) return;
            if (!SkippedColumns.ContainsKey(ncol))
                SkippedColumns.Add(ncol, 1);
        }

        public void RemoveSkipColumn(string col)
        {
            string ncol = col.ToLower();

            if (string.IsNullOrWhiteSpace(ncol)) return;
            if (SkippedColumns.ContainsKey(ncol))
                SkippedColumns.Remove(ncol);
        }
    }
}
