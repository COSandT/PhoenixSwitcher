using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CosntCommonLibrary.Helpers;
using CosntCommonLibrary.Xml.PhoenixSwitcher;

namespace PhoenixSwitcher
{
    public static class Helpers
    {
        public static XmlProjectSettings GetProjectSettings()
        {
            XmlSettingsHelper<XmlProjectSettings> projectSettings = new XmlSettingsHelper<XmlProjectSettings>("ProjectSettings.xml", $"{AppContext.BaseDirectory}//Settings//");
            if (!projectSettings.Load())
            {
                projectSettings.CreateBlank();
                projectSettings.Save();
            }
            return projectSettings.Settings;
        }
    }
}
