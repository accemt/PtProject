using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SVM
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Problem train = Problem.Read("a1a.train.txt");
            Problem test = Problem.Read("a1a.test.txt");


            //For this example (and indeed, many scenarios), the default
            //parameters will suffice.
            Parameter parameters = new Parameter();
            double C;
            double Gamma;


            //This will do a grid optimization to find the best parameters
            //and store them in C and Gamma, outputting the entire
            //search to params.txt.

            ParameterSelection.Grid(train, parameters, "params.txt", out C, out Gamma);
            parameters.C = C;
            parameters.Gamma = Gamma;


            //Train the model using the optimal parameters.

            Model model = Training.Train(train, parameters);


            //Perform classification on the test data, putting the
            //results in results.txt.

            Prediction.Predict(test, "results.txt", model, false);
        }
    }
}
