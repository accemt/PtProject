using PtProject.Domain;
using System;
using System.Collections.Generic;

namespace PtProject.DataLoader
{
    public abstract class DataLoaderBase
    {
        public Dictionary<int, string> ColumnByIdx = new Dictionary<int, string>();
        public Dictionary<string, int> IdxByColumn = new Dictionary<string, int>();
        public int TargetIdx = -1;
        public int MaxRowsLoaded = 0;
        public double LoadFactor = 1;

        public bool IsLoadForLearning = false;
        public int TotalDataLines = 0;

        public int NVars { get; protected set; }
        public string TargetName { get; protected set; }
        public Dictionary<string, int> IdName { get; protected set; }

        protected readonly Dictionary<string, int> _skippedColumns = new Dictionary<string, int>();

        public abstract List<DsfDataRow<object>> GetRows();
        public abstract Type GetItemType();

        public static Dictionary<string, int> StringValues = new Dictionary<string, int>();
    }
}
