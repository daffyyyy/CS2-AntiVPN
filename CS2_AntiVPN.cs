using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace CS2_AntiVPN;

public class CS2_AntiVPN : BasePlugin, IPluginConfig<AntiVpnConfig>
{
	public required AntiVpnConfig Config { get; set; }
	private string? _connectionString;
	private Database? _database;
	private readonly HashSet<int> _bannedPlayers = [];
	    
	public override string ModuleName => "CS2-AntiVPN";
	public override string ModuleVersion => "1.0.4";
	public override string ModuleAuthor => "daffyy";
	public override string ModuleDescription => "Kicks players using VPNs";

	public override void Load(bool hotReload)
	{
		if (hotReload && _database != null)
		{
			foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, Connected: PlayerConnectedState.PlayerConnected }))
			{
				var ipAddress = player.IpAddress?.Split(":")[0];
				if (string.IsNullOrEmpty(ipAddress) || Config.AllowedIps.Contains(ipAddress)) continue;
				
				Task.Run(() =>
				{
					_ =  VpnAction(ipAddress, player);
				});
			}
		}
	}

	public void OnConfigParsed(AntiVpnConfig config)
	{
		if (config.DatabaseHost.Length < 1 || config.DatabaseName.Length < 1 || config.DatabaseUser.Length < 1)
		{
			throw new Exception("[CS2-AntiVPN] You need to setup Database credentials in config!");
		}

		MySqlConnectionStringBuilder builder = new()
		{
			Server = config.DatabaseHost,
			Database = config.DatabaseName,
			UserID = config.DatabaseUser,
			Password = config.DatabasePassword,
			Port = (uint)config.DatabasePort,
			Pooling = true,
			MinimumPoolSize = 0,
			MaximumPoolSize = 640,
		};

		_connectionString = builder.ConnectionString;
		_database = new Database(_connectionString);

		Task.Run(async () =>
		{
			await using var connection = await _database.GetConnectionAsync();

			const string sql = """
                               CREATE TABLE IF NOT EXISTS `vpn_check` (
                                        `address` varchar(128) NOT NULL,
                                        `status` int(1) NOT NULL DEFAULT 0,
                                        `countryCode` varchar(64) NOT NULL,
                                        `timestamp` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                        UNIQUE KEY `address` (`address`)
                                       ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
                               """;

			var command = new MySqlCommand(sql, connection);
			command.ExecuteNonQuery();
		});

		Config = config;
	}

	[GameEventHandler]
	public HookResult EventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo _)
	{
		CCSPlayerController? player = @event.Userid;
		if (player == null || !player.IsValid || player.IsBot || player.IpAddress == null) return HookResult.Continue;
		var ipAddress = player.IpAddress.Split(":")[0];

		if (Config.AllowedIps.Contains(ipAddress))
			return HookResult.Continue;

		Task.Run(async () =>
		{
			await VpnAction(ipAddress, player);
		});

		return HookResult.Continue;
	}

	[GameEventHandler]
	public HookResult EventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo _)
	{
		CCSPlayerController? player = @event.Userid;
		if (player == null || !player.IsValid || player.IsBot || !player.UserId.HasValue) return HookResult.Continue;
		_bannedPlayers.Remove(player.UserId.Value);
		return HookResult.Continue;
	}

	private async Task VpnAction(string ipAddress, CCSPlayerController player)
	{
		// if (!player.UserId.HasValue)
		// 	return;
		
        var (exists, isUsingVpn, countryCode) = await IsIpInDatabase(ipAddress);
	
		if (exists)
		{
			if (Config.DetectVpn && isUsingVpn || Config.BlockedCountry.Any(country =>
				                                   country.Equals(countryCode,
					                                   StringComparison.OrdinalIgnoreCase))

			                                   || Config.AllowedCountry.Count > 0 &&
			                                   !Config.AllowedCountry.Any(country =>
				                                   country.Equals(countryCode,
					                                   StringComparison.OrdinalIgnoreCase))
			   )
			{
				await Server.NextFrameAsync(() =>
				{
					if (player == null || !player.IsValid || _bannedPlayers.Contains(player.UserId.Value))
						return;
					
					var punishCommand = PunishCommand(player);
					Server.ExecuteCommand(punishCommand);
					
					_bannedPlayers.Add(player.UserId.Value);
				});
			}
		}
		else
		{
			var info = await CheckVpn(ipAddress);
			
			if (Config.DetectVpn && info.status || Config.BlockedCountry.Any(country =>
				                                    country.Equals(info.countryCode,
					                                    StringComparison.OrdinalIgnoreCase))
			                                    || Config.AllowedCountry.Count > 0 &&
			                                    !Config.AllowedCountry.Any(country =>
				                                    country.Equals(info.countryCode,
					                                    StringComparison.OrdinalIgnoreCase))
			   )
			{
				await Server.NextFrameAsync(() =>
				{
					if (player == null || !player.IsValid || _bannedPlayers.Contains(player.UserId.Value))
						return;

					var punishCommand = PunishCommand(player);
					Server.ExecuteCommand(punishCommand);
					
					_bannedPlayers.Add(player.UserId.Value);
				});
			}
		
			_ = SaveIpToDatabase(ipAddress, info.status, info.countryCode);
		}
	}

	private string PunishCommand(CCSPlayerController player)
	{
		var punishCommand = Config.PunishCommand.Replace("{userid}", player.UserId.ToString())
			.Replace("{steamid}", player.SteamID.ToString())
			.Replace("{name}", player.PlayerName);
		
		return punishCommand;
	}

	private async Task<(bool status, string countryCode)> CheckVpn(string ipAddress)
	{
		using var client = new HttpClient();
		var url = string.IsNullOrEmpty(Config.ApiKey) ? $"https://proxycheck.io/v2/{ipAddress}?vpn=2&asn=1" : $"http://proxycheck.io/v2/{ipAddress}?key={Config.ApiKey}&vpn=2&asn=1";

		try
		{
			if (string.IsNullOrEmpty(Config.ApiKey))
			{
				var response = await client.GetAsync(url);
				var responseBody = await response.Content.ReadAsStringAsync();

				if (response.IsSuccessStatusCode)
				{
					dynamic? jsonResult = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
					if (jsonResult == null) return (false, "Unknown");

					if (jsonResult.status == "ok")
					{
						return (jsonResult[ipAddress].proxy == "yes", jsonResult[ipAddress].isocode);
					}
				}
			}
			else
			{
				var response = await client.GetAsync(url);
				var responseBody = await response.Content.ReadAsStringAsync();
				
				if (response.IsSuccessStatusCode)
				{
					dynamic? jsonResult = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
					return jsonResult == null ? (false, "Unknown") : ((bool status, string countryCode))(jsonResult[ipAddress].proxy == "yes", jsonResult[ipAddress].isocode ?? "Unknown");
				}
			}
		}
		catch (Exception)
		{
			Logger.LogError($"Unable to fetch ip `{ipAddress} info!");
		}

		return (false, "Unknown");
	}

	private async Task<(bool exists, bool isUsingVPN, string countryCode)> IsIpInDatabase(string ipAddress)
	{
		if (_database == null) return (false, false, "Unknown");

		await using var connection = await _database.GetConnectionAsync();
		
		try
		{
			const string sql = "SELECT `status`, `countryCode` FROM `vpn_check` WHERE address = @ipAddress";
			var result = await connection.QuerySingleOrDefaultAsync(sql, new { ipAddress });
			
			return result == null
				? (false, false, "Unknown")
				: ((bool exists, bool isUsingVPN, string countryCode))(true, result.status != 0, result.countryCode);
		}
		catch (Exception e)
		{
			Logger.LogError(e.Message);
		}
		
		return (true, false, "Unknown");
	}

	private async Task SaveIpToDatabase(string ipAddress, bool isUsingVpn, string countryCode = "Unknown")
	{
		if (_database == null) return;

		await using var connection = await _database.GetConnectionAsync();
		
		try
		{
			const string sql = "INSERT INTO `vpn_check` (`address`, `status`, `countryCode`) VALUES (@ipAddress, @status, @countryCode)";
			await connection.ExecuteAsync(sql, new { ipAddress, status = isUsingVpn ? 1 : 0, countryCode });
		}
		catch (Exception e)
		{
			Logger.LogError(e.Message);
			throw;
		}
	}
}