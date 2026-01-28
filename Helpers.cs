using AdonisUI.Controls;

using CosntCommonLibrary.Helpers;
using CosntCommonLibrary.Settings;
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
            // Normally Settings should always be filled in at this point. 
            return projectSettings.Settings != null ? projectSettings.Settings : new XmlProjectSettings();
        }
        public static string TryGetLocalizedText(string localizedTextID, string fallBackText)
        {
            LocalizationManager localizationManager = LocalizationManager.GetInstance();
            if (localizationManager == null) return fallBackText;
            return localizationManager.GetEntryForActiveLanguage(localizedTextID, fallBackText);
        }

        public static string RemoveExtraZeroFromVersionName(string versionName)
        {
            string result = "";
            List<string> splitVersions = versionName.Split(".").ToList();
            foreach (string splitVersion in splitVersions)
            {
                string temp = splitVersion;
                if (splitVersion.Length > 1)
                {
                    for (int i = 0; i < temp.Length - 1; ++i)
                    {
                        if (temp[i] != '0') break;
                        temp = temp.Remove(i, 1);
                        --i;
                    }
                }
                result = $"{result}{temp}.";
            }
            return result.Substring(0, result.Length - 1);
        }

        public static MessageBoxResult ShowLocalizedYesNoMessageBox(string localizedTextId, string fallbackText)
        {
            MessageBoxModel model = new MessageBoxModel();
            model.Buttons = model.Buttons.Append(new MessageBoxButtonModel(TryGetLocalizedText("ID_08_0002", "yes"), MessageBoxResult.Yes));
            model.Buttons = model.Buttons.Append(new MessageBoxButtonModel(TryGetLocalizedText("ID_08_0003", "no"), MessageBoxResult.No));
            model.Text = TryGetLocalizedText(localizedTextId, fallbackText);
            return MessageBox.Show(model);
        }
        public static MessageBoxResult ShowLocalizedOkMessageBox(string localizedTextId, string fallbackText)
        {
            MessageBoxModel model = new MessageBoxModel();
            model.Buttons = model.Buttons.Append(new MessageBoxButtonModel(TryGetLocalizedText("ID_08_0001", "ok"), MessageBoxResult.OK));
            model.Text = TryGetLocalizedText(localizedTextId, fallbackText);
            return MessageBox.Show(model);
        }
        public static MessageBoxResult ShowLocalizedOkCancelMessageBox(string localizedTextId, string fallbackText)
        {
            MessageBoxModel model = new MessageBoxModel();
            model.Buttons = model.Buttons.Append(new MessageBoxButtonModel(TryGetLocalizedText("ID_08_0001", "ok"), MessageBoxResult.OK));
            model.Buttons = model.Buttons.Append(new MessageBoxButtonModel(TryGetLocalizedText("ID_08_0004", "cancel"), MessageBoxResult.Cancel));
            model.Text = TryGetLocalizedText(localizedTextId, fallbackText);
            return MessageBox.Show(model);
        }
    }
}
