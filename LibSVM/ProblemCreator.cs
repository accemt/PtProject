using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SVM
{
    public class ProblemCreator
    {
        private int _maxIndex = 0;
        private List<double> _vy = new List<double>();
        private List<Node[]> _vx = new List<Node[]>();

        public void ReadRow(double[] coeffs, double target)
        {
            int len = coeffs.Length;
            if (_maxIndex == 0) _maxIndex = len;
            _vy.Add(target);

            Node[] x = new Node[len];
            for (int j = 0; j < len; j++)
            {
                x[j] = new Node();
                x[j].Index = j+1;
                x[j].Value = coeffs[j];
            }

            _vx.Add(x);
        }

        public Problem CreateProblem()
        {
            return new Problem(_vy.Count, _vy.ToArray(), _vx.ToArray(), _maxIndex);
        }
    }
}
