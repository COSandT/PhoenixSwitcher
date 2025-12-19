using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Policy;
using CosntCommonLibrary.Json;
using CosntCommonLibrary.Rest;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using RestSharp;

namespace PhoenixSwitcher
{
    public class PhoenixRest
    {
        private static PhoenixRest? _instance;
        private static readonly object _lock = new object();

        private RestClient _restClient = new RestClient();
        private string _baseServerURL = "";
        public static PhoenixRest GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new PhoenixRest();
                    }
                }
            }
            return _instance;
        }
        private PhoenixRest()
        {
            XmlProjectSettings projectSettings = Helpers.GetProjectSettings();
            _baseServerURL = projectSettings.RestAPIBaseURL;
            _restClient = new RestClient();
        }

        public async Task<bool> IsApiRunning()
        {
            try
            {
                var result = await _restClient.GetAsync<string>(_baseServerURL + "state/");
                if (result == "Running") return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<AppSettingPcm>?> GetPcmAppSettings()
        {
            try
            {
                return await _restClient.GetAsync<List<AppSettingPcm>>(_baseServerURL + "PcmSettings");
            }
            catch
            {
                return new List<AppSettingPcm>();
            }
        }

        public async Task<List<FileDetail>?> GetBundleFiles()
        {
            try
            {
                return await _restClient.GetAsync<List<FileDetail>>(_baseServerURL + "pcmbundlefiles");
            }
            catch
            {
                return new List<FileDetail>();
            }
        }

        // Download methods
        public async Task<string> DownloadBundleFiles(FileDetail detail, string drive)
        {
            try
            {
                string filePath = drive + detail.FileName;
                if (File.Exists(filePath)) File.Delete(filePath);

                RestRequest request = new RestRequest($"{_baseServerURL}pcmbundlefiles/{detail.Id}");
                byte[]? bytes = await _restClient.DownloadDataAsync(request);
                if (bytes != null && bytes.Length == detail.FileSize)
                {
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        fileStream.Write(bytes, 0, bytes.Length);
                        fileStream.Close();
                    }
                }
                return filePath;
            }
            catch
            {
                return "";
            }
        }
        public async Task<bool> DownloadBundleFilesWithCallback(FileDetail detail, string drive)
        {
            try
            {
                string filePath = drive + detail.FileName;
                if (File.Exists(filePath)) File.Delete(filePath); 
                
                using HttpClient http = new HttpClient();
                using var response = await http.GetAsync($"{_baseServerURL}/pcmbundlefiles/{detail.Id}", HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var canReport = total != -1;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;


                    if (canReport)
                    {
                        //TODO: report downloadPercent.
                        double percent = (double)totalRead / total;
                    }
                }

                fileStream.Close();

                // TODO: Localization.
                Console.WriteLine("Download voltooid!");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
