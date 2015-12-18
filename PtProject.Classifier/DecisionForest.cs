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
    public class DecisionForest
    {
        public static int NCls = 0;

        /// <summary>
        /// List of decision trees
        /// </summary>
        private List<DecisionTree> _forest;

        /// <summary>
        /// Classifier id
        /// </summary>
        public int Id { get; private set; }

        public DecisionForest()
        {
            _forest = new List<DecisionTree>();
            Id = NCls++;
        }

        public void AddTree(DecisionTree tree)
        {
            _forest.Add(tree);
        }

        /// <summary>
        /// Количество деревьев в классификаторе
        /// </summary>
        public int CountTrees
        {
            get { return _forest.Count(); }
        }

        public void Clear()
        {
            _forest.Clear();
        }

        /// <summary>
        /// Predict probability by current forest (between 0 and 1)
        /// </summary>
        /// <param name="sarr">object coeffs to classify</param>
        /// <returns></returns>
        public double[] PredictProba(double[] sarr)
        {
            int ntrees = _forest.Count;
            int nclasses = _forest.First().NClasses;
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
            int ntrees = _forest.Count;
            if (ntrees == 0)
                throw new InvalidOperationException("forest is empty");

            int nclasses = _forest.First().NClasses;

            var sy = new double[nclasses];
            foreach (var tree in _forest)
            {
                if (tree.NClasses!=nclasses)
                    throw new InvalidOperationException("every tree must have equal NClasses parameter");

                var probs = tree.PredictCounts(sarr);
                for (int i = 0; i < nclasses; i++)
                    sy[i] += probs[i];
            }

            return sy;
        }

        public void Serialize()
        {
            string treesDir = Environment.CurrentDirectory + "\\batches";
            if (!Directory.Exists(treesDir))
                Directory.CreateDirectory(treesDir);
            var dinfo = new DirectoryInfo(treesDir);

            var fs = new FileStream(dinfo.FullName + "\\" + "batch_" + string.Format("{0:0000.#}", Id) + ".dmp", FileMode.Create, FileAccess.Write);

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

        public static DecisionForest Deserialize(string path)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var formatter = new BinaryFormatter();
            DecisionForest cls = null;

            try
            {
                cls = (DecisionForest)formatter.Deserialize(fs);
            }
            catch (SerializationException e)
            {
                Logger.Log(e);
            }
            finally
            {
                fs.Close();
            }

            return cls;
        }
    }
}
