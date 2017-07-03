using System;
using System.Configuration;

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
            catch (Exception e)
            {
                Logger.Log(e);
            }

            return value;
        }
    }
}
