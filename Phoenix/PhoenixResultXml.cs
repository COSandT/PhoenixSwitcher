using System.Xml.Serialization;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;
using CosntCommonLibrary.Xml;

namespace PhoenixSwitcher.Phoenix
{
	[XmlRoot("PhoenixInfo")]
	public class PhoenixResultXml : XmlBase<PhoenixResultXml>
	{
		public PhoenixResultXml() : base("PhoenixInfo") { }
		public PhoenixResultXml(PhoenixSwitcherDone switcherDone) : base("PhoenixInfo")
		{
			Vin = switcherDone.Vin;
			Vin_9Char = switcherDone.Vin_9char;
			Van = switcherDone.Van;
			Bundle_Version = switcherDone.Bundle_version;
			Timestamp = switcherDone.TimeStamp;
			Machine_Type = switcherDone.Machine_type;
			Display_Type = switcherDone.Display_type;
			Pcm_Type = switcherDone.Pcm_type;
			Pcm_Gen = switcherDone.Pcm_gen;
			Num_Displays++;
		}
		[XmlElement("Vin")]public string Vin { get; set; } = "";
		[XmlElement("Vin_9Char")]public string Vin_9Char { get; set; } = "";
		[XmlElement("Van")]public string Van { get; set; } = "";
		[XmlElement("BundleVersion")]public string Bundle_Version { get; set; } = "";
		[XmlElement("TimeStamp")]public DateTime Timestamp { get; set; }
		[XmlElement("Machine_Type")]public string Machine_Type { get; set; } = "";
		[XmlElement("Display_Type")]public string Display_Type { get; set; } = "";
		[XmlElement("Pcm_Type")]public string Pcm_Type { get; set; } = "";
		[XmlElement("Pcm_Gen")]public string Pcm_Gen { get; set; } = "";
		[XmlElement("Num_Displays")] public int Num_Displays { get; set; } = 0;
	}
}
