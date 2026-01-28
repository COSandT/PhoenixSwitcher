using System.IO;
using System.Text.Json;

using RestSharp;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Rest;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

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

        public async Task<BundleSelection?> GetPcmAppSettings(string machineType, string pcmType, string pcmGen, string displayType, string department = "croix")
        {
            try
            {
                return await _restClient.GetAsync<BundleSelection>($"{_baseServerURL}PcmSettings/" +
                    $"Department={department}" +
                    $"&MachineType={machineType}" +
                    $"&PcmType={pcmType}" +
                    $"&PcmGen={pcmGen}" +
                    $"&DisplayType={displayType}");
            }
            catch
            {
                return new BundleSelection();
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

        public async void PostMachineResults(PhoenixSwitcherDone machineResult)
        {
            try
            {
                RestRequest request = new RestRequest($"{_baseServerURL}dones", Method.Post);
                request.AddJsonBody(JsonSerializer.Serialize(machineResult));

                await _restClient.ExecuteAsync(request);
            }
            catch { }
        }
        public Task<XmlProductionDataPCM?> GetPCMMachineFile(string department = "croix")
        {
            try
            {
                return  _restClient.GetAsync<XmlProductionDataPCM>($"{_baseServerURL}pcmmachines/Department={department}");
            }
            catch
            {
                return Task.FromResult<XmlProductionDataPCM?>(new XmlProductionDataPCM());
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
    }
}
