using PtProject.Domain.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Classifier
{
    [Serializable]
    public class DecisionBatch
    {
        public static int NCls = 0;

        /// <summary>
        /// List of decision trees
        /// </summary>
        private List<DecisionTree> _batch;

        /// <summary>
        /// Classifier id
        /// </summary>
        public int Id { get; private set; }

        public DecisionBatch()
        {
            _batch = new List<DecisionTree>();
            Id = NCls++;
        }

        public void AddTree(DecisionTree tree)
        {
            _batch.Add(tree);
        }

        /// <summary>
        /// Количество деревьев в классификаторе
        /// </summary>
        public int CountTreesInBatch
        {
            get { return _batch.Count(); }
        }

        public void Clear()
        {
            _batch.Clear();
        }

        /// <summary>
        /// Predict probability by current forest (between 0 and 1)
        /// </summary>
        /// <param name="sarr">object coeffs to classify</param>
        /// <returns></returns>
        public double[] PredictProba(double[] sarr)
        {
            int ntrees = _batch.Count;
            int nclasses = _batch.First().NClasses;
            var sy = PredictCounts(sarr);
            
            for (int i=0;i<nclasses;i++)
                sy[i] /= nclasses;

            return sy;
        }

        /// <summary>
        /// Count trees for each classes (between 0 and 1)
        /// </summary>
        /// <param name="sarr">object coeffs to classify</param>
        /// <returns></returns>
        public double[] PredictCounts(double[] sarr)
        {
            int ntrees = _batch.Count;
            if (ntrees == 0)
                throw new InvalidOperationException("forest is empty");

            int nclasses = _batch.First().NClasses;

            var sy = new double[nclasses];
            foreach (var tree in _batch)
            {
                if (tree.NClasses!=nclasses)
                    throw new InvalidOperationException("every tree must have equal NClasses parameter");

                var probs = tree.PredictCounts(sarr);
                for (int i = 0; i < nclasses; i++)
                    sy[i] += probs[i];
            }

            return sy;
        }

        public void Save()
        {
            string treesDir = Environment.CurrentDirectory + "\\batches";
            if (!Directory.Exists(treesDir))
                Directory.CreateDirectory(treesDir);
            var dinfo = new DirectoryInfo(treesDir);

            string fullname = dinfo.FullName + "\\" + "batch_" + string.Format("{0:0000.#}", Id) + ".dmp";
            var fs = new FileStream(fullname, FileMode.Create, FileAccess.Write);

            var formatter = new BinaryFormatter();
            try
            {
                formatter.Serialize(fs, this);
            }
            catch (SerializationException e)
            {
                Logger.Log(e);
            }
            finally
            {
                fs.Close();
            }
        }

        public static DecisionBatch Load(string path)
        {
            DecisionBatch cls = null;
            FileStream fs = null;

            try
            {
                fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                var formatter = new BinaryFormatter();
                cls = (DecisionBatch)formatter.Deserialize(fs);
                cls.Id = NCls++;
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
            finally
            {
                if (fs!=null) fs.Close();
            }

            return cls;
        }
    }
}
