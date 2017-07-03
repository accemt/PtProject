using Microsoft.CSharp;
using PtProject.Domain.Util;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;

namespace PtProject.Eval
{
    public class Expression
    {
        private static int _expNum;

        private readonly string _expression;
        private readonly int _n;
        private Assembly _assembly;

        public Expression(string expression)
        {
            _expression = expression;
            _n = _expNum++;
        }

        public void Compile()
        {
            try
            {
                string[] sources = Source.Create(_expression, _n);

                CSharpCodeProvider cp = new CSharpCodeProvider();
                CompilerParameters cparams = new CompilerParameters();
                cparams.GenerateInMemory = true;
                var result = cp.CompileAssemblyFromSource(cparams, sources);
                if (result.Errors.Count>0)
                {
                    Logger.Log("compilation errors in expression: " + _expression);
                    foreach (CompilerError err in result.Errors)
                    {
                        Logger.Log("compilation error: " + err.ErrorText);
                    }
                }
                _assembly = result.CompiledAssembly;

                Logger.Log("assm done for: " + _expression);
            }
            catch (Exception e)
            {
                Logger.Log(e);
                throw;
            }
        }

        public double Eval(Dictionary<string, double> values)
        {
            double ret = 0;
            try
            {
                var stype = _assembly.GetType("PtProject.Eval.Temp" + _n);
                ret = (double)stype.GetMethod("f").Invoke(null, new Object[] { values });
            }
            catch (Exception e)
            {
                Logger.Log(e.Message + (e.InnerException!=null?("\n\t"+e.InnerException.Message):"")  + "\n\texpr: " + _expression);
            }
            return ret;
        }
    }
}
