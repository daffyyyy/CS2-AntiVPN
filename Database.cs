using MySqlConnector;

namespace CS2_AntiVPN;

public class Database(string dbConnectionString)
{
	public MySqlConnection GetConnection()
	{
		var connection = new MySqlConnection(dbConnectionString);
		connection.Open();
		return connection;
	}

	public async Task<MySqlConnection> GetConnectionAsync()
	{
		var connection = new MySqlConnection(dbConnectionString);
		await connection.OpenAsync();
		
		return connection;
	}
}