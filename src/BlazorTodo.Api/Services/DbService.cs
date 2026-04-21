using Azure.Storage.Blobs;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace BlazorTodo.Api.Services;

public class DbService
{
    private const string ContainerName = "taskdb";
    private const string BlobName = "todos.db";

    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<DbService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _dbPath;

    public DbService(BlobServiceClient blobServiceClient, ILogger<DbService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<SqliteConnection> GetOpenConnectionAsync()
    {
        if (_dbPath is null)
            await EnsureDatabaseAsync();

        var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        return connection;
    }

    public async Task PersistAsync()
    {
        if (_dbPath is null) return;

        await _lock.WaitAsync();
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blob = container.GetBlobClient(BlobName);
            await using var stream = File.OpenRead(_dbPath);
            await blob.UploadAsync(stream, overwrite: true);
            _logger.LogInformation("Database persisted to blob storage.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureDatabaseAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_dbPath is not null) return;

            _dbPath = Path.Combine(Path.GetTempPath(), BlobName);

            var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlobClient(BlobName);
            if (await blob.ExistsAsync())
            {
                _logger.LogInformation("Downloading existing database from blob storage.");
                await blob.DownloadToAsync(_dbPath);
            }
            else
            {
                _logger.LogInformation("No existing database found, creating new.");
            }

            await CreateSchemaAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task CreateSchemaAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var createSql = """
            CREATE TABLE IF NOT EXISTS Tasks (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Title       TEXT    NOT NULL,
                Description TEXT,
                DueDate     TEXT,
                Priority    INTEGER NOT NULL DEFAULT 1,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                CreatedAt   TEXT    NOT NULL,
                CompletedAt TEXT
            );
            """;
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = createSql;
        await cmd.ExecuteNonQueryAsync();

        // Migrate existing databases that may not have the CompletedAt column
        await using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA table_info(Tasks);";
        await using var reader = await pragmaCmd.ExecuteReaderAsync();
        bool hasCompletedAt = false;
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1) == "CompletedAt") { hasCompletedAt = true; break; }
        }
        if (!hasCompletedAt)
        {
            await using var migrateCmd = connection.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE Tasks ADD COLUMN CompletedAt TEXT;";
            await migrateCmd.ExecuteNonQueryAsync();
        }
    }
}
