using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using System.Text.Json.Serialization;

namespace CS2_AntiVPN;

[MinimumApiVersion(228)]

public class AntiVpnConfig : BasePluginConfig
{
	[JsonPropertyName("ApiKey")] public string ApiKey { get; set; } = "";
	[JsonPropertyName("DetectVpn")] public bool DetectVpn { get; set; } = true;
	[JsonPropertyName("BlockedCountry")] public List<string> BlockedCountry { get; set; } = [];
	[JsonPropertyName("AllowedCountry")] public List<string> AllowedCountry { get; set; } = [];
	[JsonPropertyName("AllowedIps")] public List<string> AllowedIps { get; set; } = [];
	[JsonPropertyName("PunishCommand")] public string PunishCommand { get; set; } = "css_ban #{userid} 18000 \"VPN Detected\"";

	[JsonPropertyName("DatabaseHost")]
	public string DatabaseHost { get; set; } = "";

	[JsonPropertyName("DatabasePort")]
	public int DatabasePort { get; set; } = 3306;

	[JsonPropertyName("DatabaseUser")]
	public string DatabaseUser { get; set; } = "";

	[JsonPropertyName("DatabasePassword")]
	public string DatabasePassword { get; set; } = "";

	[JsonPropertyName("DatabaseName")]
	public string DatabaseName { get; set; } = "";
}