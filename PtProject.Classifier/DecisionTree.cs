using PtProject.Domain.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Classifier
{
    [Serializable]
    public class DecisionTree
    {
        public static int N;
        private static object _locker = new object();

        public alglib.decisionforest AlglibTree { get; private set; }
        public int NClasses { get; private set; }

        public int Id;

        public DecisionTree(alglib.decisionforest tree, int nclasses)
        {
            AlglibTree = tree;
            NClasses = nclasses;
        }

        /// <summary>
        /// Predict probability by exact tree (0 or 1)
        /// </summary>
        /// <param name="sarr">object coeffs to classify</param>
        /// <returns></returns>
        public double[] PredictCounts(double[] sarr)
        {
            var sy = new double[NClasses];
            alglib.dfprocess(AlglibTree, sarr, ref sy);

            return sy;
        }

        public static DecisionTree CreateTree(double[,] xy, int npoints, int nvars, int nclasses, double coeff, int id=0)
        {
            int info;
            alglib.decisionforest df;
            alglib.dfreport rep;
            alglib.dfbuildrandomdecisionforest(xy, npoints, nvars, nclasses, 1, coeff, out info, out df, out rep);

            var tree = new DecisionTree(df, nclasses);
            tree.Id = id==0?CreateId():id;
            return tree;
        }

        public static DecisionTree CreateTree(int[] indexes, double[,] xy, int npoints, int nvars, int nclasses, double coeff)
        {
            if (indexes == null)
                return CreateTree(xy, npoints, nvars, nclasses, coeff);

            int modNpoints = (int)(npoints * coeff); // столько значений надо нагенерировать
            int nk = 0; // столько нагенерировали
            double[,] nxy = new double[modNpoints, nvars + 1]; // сами значения

            var exists = new Dictionary<int, int>();

            while (nk < modNpoints)
            {
                for (int i = 0; i < modNpoints; i++)
                {
                    int sn = (int)(RandomGen.GetTrangle() * npoints);
                    if (sn >= indexes.Length) sn = indexes.Length - 1;

                    if (exists.ContainsKey(sn)) continue; // такой ключ уже был

                    exists.Add(sn, 0);
                    int sidx = indexes[sn];
                    for (int j = 0; j < nvars + 1; j++)
                        nxy[i, j] = xy[sidx, j];
                    nk++;

                    if (nk >= modNpoints) break;
                }

                if (nk >= modNpoints) break;
            }

            int id = CreateId();

            return CreateTree(nxy, modNpoints, nvars, nclasses, 1, id);
        }

        private static int CreateId()
        {
            int id;
            lock (_locker)
            {
                id = N++;
            }
            return id;
        }
    }
}
