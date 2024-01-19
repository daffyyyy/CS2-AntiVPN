using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using static CounterStrikeSharp.API.Core.Listeners;

namespace CS2_AntiVPN;

[MinimumApiVersion(142)]

public class AntiVPNConfig : BasePluginConfig
{
	[JsonPropertyName("ApiKey")] public string ApiKey { get; set; } = "";
}

public class CS2_AntiVPN : BasePlugin, IPluginConfig<AntiVPNConfig>
{
	public required AntiVPNConfig Config { get; set; }
	private string? connectionString;

	public override string ModuleName => "CS2-AntiVPN";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "daffyy";
	public override string ModuleDescription => "Kicks players using VPNs";

	public override void Load(bool hotReload)
	{
		connectionString = $"Data Source={ModuleDirectory}/CS2-AntiVPN.db3";

		RegisterListener<OnClientConnected>(OnClientConnectedHandler);

		using (var connection = new SqliteConnection(connectionString))
		{
			connection.Open();

			using (var command = new SqliteCommand("CREATE TABLE IF NOT EXISTS IpsList (Id INTEGER PRIMARY KEY AUTOINCREMENT, IpAddress TEXT, IsUsingVPN INTEGER);", connection))
			{
				command.ExecuteNonQuery();
			}
		}
	}

	public void OnConfigParsed(AntiVPNConfig config)
	{
		Config = config;
	}

	private void OnClientConnectedHandler(int playerSlot)
	{
		CCSPlayerController? player = Utilities.GetPlayerFromSlot(playerSlot);

		if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || player.IpAddress == null) return;

		string ipAddress = player.IpAddress.Split(":")[0];

		Task.Run(async () =>
		{
			(bool exists, bool isUsingVPN) localCheck = await IsIPInDatabase(ipAddress);

			if (localCheck.exists)
			{
				Server.NextFrame(() =>
				{
					if (localCheck.isUsingVPN)
						Server.ExecuteCommand($"kickid {player.UserId} Forbidden to use VPN!");
				});

				return;
			}

			bool isUsingVPN = await CheckVPN(ipAddress);

			if (isUsingVPN)
			{
				Server.NextFrame(() =>
				{
					Server.ExecuteCommand($"kickid {player.UserId} Forbidden to use VPN!");
				});
			}

			await SaveIpToDatabase(ipAddress, isUsingVPN);
		});
	}

	private async Task<bool> CheckVPN(string ipAddress)
	{
		using (HttpClient client = new HttpClient())
		{
			try
			{
				string url = string.IsNullOrEmpty(Config.ApiKey) ? $"https://proxycheck.io/v2/{ipAddress}?vpn=2" : $"https://proxycheck.io/v2/{ipAddress}?key={Config.ApiKey}&vpn=2";
				HttpResponseMessage response = await client.GetAsync(url);

				if (response.IsSuccessStatusCode)
				{
					string responseBody = await response.Content.ReadAsStringAsync();

					dynamic? jsonResult = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);

					if (jsonResult == null) return false;

					if (jsonResult.status == "ok" && jsonResult[ipAddress].proxy == "yes")
					{
						return true;
					}
				}
			}
			catch (Exception)
			{
				Logger.LogError($"Unable to fetch ip `{ipAddress} info!");
			}
		}
		return false;
	}

	public async Task<(bool exists, bool isUsingVPN)> IsIPInDatabase(string ipAddress)
	{
		using (var connection = new SqliteConnection(connectionString))
		{
			await connection.OpenAsync();

			using (var command = new SqliteCommand("SELECT IsUsingVPN FROM IpsList WHERE IpAddress = @IpAddress;", connection))
			{
				command.Parameters.AddWithValue("@IpAddress", ipAddress);

				using (var reader = await command.ExecuteReaderAsync())
				{
					if (await reader.ReadAsync())
					{
						return (true, Convert.ToBoolean(reader["IsUsingVPN"]));
					}
				}
			}

			return (false, false);
		}
	}
	public async Task SaveIpToDatabase(string ipAddress, bool isUsingVPN)
	{
		using (var connection = new SqliteConnection(connectionString))
		{
			await connection.OpenAsync();

			using (var command = new SqliteCommand("INSERT INTO IpsList (IpAddress, IsUsingVPN) VALUES (@IpAddress, @IsUsingVPN);", connection))
			{
				command.Parameters.AddWithValue("@IpAddress", ipAddress);
				command.Parameters.AddWithValue("@IsUsingVPN", isUsingVPN ? 1 : 0);
				await command.ExecuteNonQueryAsync();
			}
		}
	}
}