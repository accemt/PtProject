using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Classifier
{
    public interface IClassifier
    {

        void LoadData(string trainPath, string testPath, string ids, string target);

        ClassifierResult Build();

        double[] PredictProba(double[] sarr);
    }
}
