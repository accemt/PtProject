using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Domain
{
    public class CombinationIterator : IEnumerator<string[]>
    {
        private readonly string[] _array;
        private readonly int[] _indexes;
        private readonly int _n;
        private string[] _current;

        public CombinationIterator(string[] array, int n)
        {
            _array = array;
            _n = n;

            _indexes = new int[n];
            for (int j = 0; j < n; j++)
            {
                _indexes[j] = j;
            }
        }

        private bool HasNext()
        {
            if (_indexes[_n - 1] < _array.Length)
                return true;
            return false;
        }

        public bool MoveNext()
        {
            if (!HasNext()) return false;


            var ret = new string[_n];
            for (int k = 0; k < _n; k++)
            {
                ret[k] = _array[_indexes[k]];
            }
            int s = _n - 1;

            _indexes[s]++;

            if (_indexes[s] >= _array.Length)
            {
                for (int t = 1; t <= s; t++)
                {
                    if (_indexes[s - t] < _array.Length - 1 - t)
                    { // нашли индекс, который можно двинуть
                        _indexes[s - t]++;

                        for (int j = 1; j <= t; j++)
                        {
                            _indexes[s - t + j] = _indexes[s - t + j - 1] + 1;
                        }
                        break;
                    }
                }
            }

            _current = ret;
            return true;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public string[] Current { get { return _current; } }

        object IEnumerator.Current
        {
            get { return _current; }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
