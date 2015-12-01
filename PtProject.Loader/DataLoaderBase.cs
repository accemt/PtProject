using PtProject.Domain;
using System;
using System.Collections.Generic;

namespace PtProject.Loader
{
    public abstract class DataLoaderBase
    {
        /// <summary>
        /// column name by index in file
        /// </summary>
        public Dictionary<int, string> ColumnByIdx = new Dictionary<int, string>();

        /// <summary>
        /// index in file by column name
        /// </summary>
        public Dictionary<string, int> IdxByColumn = new Dictionary<string, int>();

        /// <summary>
        /// column name by index in row
        /// </summary>
        public Dictionary<int, string> ColumnByIdxRow = new Dictionary<int, string>();

        /// <summary>
        /// target column index
        /// </summary>
        public int TargetIdx = -1;

        /// <summary>
        /// stop loading file at that count
        /// </summary>
        public int MaxRowsLoaded = 0;

        /// <summary>
        /// skeep column loading if rnd() < LoadFactor
        /// </summary>
        public double LoadFactor = 1;

        public bool IsLoadForLearning = false;
        public int TotalDataLines = 0;

        public int NVars { get; protected set; }
        public string TargetName { get; protected set; }
        public Dictionary<string, int> Ids { get; protected set; }

        /// <summary>
        /// columns for skeeping
        /// </summary>
        public readonly Dictionary<string, int> SkippedColumns = new Dictionary<string, int>();

        public abstract List<DataRow<object>> GetRows();
        public abstract Type GetItemType();

        public static Dictionary<string, int> StringValues = new Dictionary<string, int>();

        public char SplitSymbol;
    }
}
