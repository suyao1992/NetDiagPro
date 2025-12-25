using Microsoft.Data.Sqlite;
using NetDiagPro.Views;

namespace NetDiagPro.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(appData, "NetDiagPro", "history.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS test_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                target TEXT NOT NULL,
                latency_ms REAL,
                dns_ms REAL,
                tcp_ms REAL,
                status_code INTEGER,
                success INTEGER
            )";
        command.ExecuteNonQuery();
    }

    public async Task SaveRecordAsync(string target, double latencyMs, double dnsMs, double tcpMs, int statusCode, bool success)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO test_history (timestamp, target, latency_ms, dns_ms, tcp_ms, status_code, success)
            VALUES (@timestamp, @target, @latency, @dns, @tcp, @status, @success)";
        
        command.Parameters.AddWithValue("@timestamp", DateTime.Now.ToString("o"));
        command.Parameters.AddWithValue("@target", target);
        command.Parameters.AddWithValue("@latency", latencyMs);
        command.Parameters.AddWithValue("@dns", dnsMs);
        command.Parameters.AddWithValue("@tcp", tcpMs);
        command.Parameters.AddWithValue("@status", statusCode);
        command.Parameters.AddWithValue("@success", success ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<TestRecord>> GetRecentRecordsAsync(int limit = 50)
    {
        var records = new List<TestRecord>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT timestamp, target, latency_ms, success 
            FROM test_history 
            ORDER BY id DESC 
            LIMIT @limit";
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new TestRecord
            {
                Timestamp = DateTime.Parse(reader.GetString(0)),
                Target = reader.GetString(1),
                LatencyMs = reader.GetDouble(2),
                Success = reader.GetInt32(3) == 1
            });
        }

        return records;
    }

    public async Task ClearAllRecordsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM test_history";
        await command.ExecuteNonQueryAsync();
    }
}
