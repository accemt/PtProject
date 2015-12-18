using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtProject.Domain.Util
{
    public class ConfigReader
    {
        public static string Read(string name)
        {
            string value = null;
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                if (appSettings[name] != null)
                    value = appSettings[name];
                if (string.IsNullOrWhiteSpace(value))
                    value = null;
            }
            catch (Exception)
            {
            }

            return value;
        }
    }
}
