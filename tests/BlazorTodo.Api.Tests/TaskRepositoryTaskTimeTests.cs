using Dapper;
using Microsoft.Data.Sqlite;
using BlazorTodo.Api.Models;
using BlazorTodo.Api.Services;

namespace BlazorTodo.Api.Tests;

/// <summary>
/// Tests for <see cref="TaskRepository"/> that verify TaskTime is persisted and retrieved correctly.
/// Each test creates its own named in-memory SQLite database for full isolation.
/// A "keeper" connection is held open so the in-memory database survives between repository calls.
/// </summary>
public class TaskRepositoryTaskTimeTests : IAsyncLifetime
{
    private SqliteConnection _keeper = null!;
    private TaskRepository _repo = null!;
    private string _dbName = null!;

    public async Task InitializeAsync()
    {
        // Use a unique connection string per test instance so tests don't interfere.
        var connectionString = $"Data Source=testdb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _dbName = connectionString;

        // Keep one connection open for the full lifetime of the test so the
        // in-memory database is not destroyed when other connections close.
        _keeper = new SqliteConnection(_dbName);
        await _keeper.OpenAsync();

        await _keeper.ExecuteAsync("""
            CREATE TABLE Tasks (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Title       TEXT    NOT NULL,
                Description TEXT,
                DueDate     TEXT,
                TaskTime    TEXT,
                Priority    INTEGER NOT NULL DEFAULT 1,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                CreatedAt   TEXT    NOT NULL
            );
            """);

        _repo = new TaskRepository(new InMemoryDbService(_dbName));
    }

    public async Task DisposeAsync()
    {
        await _keeper.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_WithTaskTime_PersistsTaskTime()
    {
        var task = new TodoTask
        {
            Title = "Test task",
            TaskTime = "09:30"
        };

        var created = await _repo.CreateAsync(task);

        Assert.NotEqual(0, created.Id);
        Assert.Equal("09:30", created.TaskTime);
    }

    [Fact]
    public async Task CreateAsync_WithNullTaskTime_PersistsNull()
    {
        var task = new TodoTask
        {
            Title = "No time task",
            TaskTime = null
        };

        var created = await _repo.CreateAsync(task);

        Assert.Null(created.TaskTime);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTaskTime()
    {
        await _repo.CreateAsync(new TodoTask { Title = "Task A", TaskTime = "14:00" });
        await _repo.CreateAsync(new TodoTask { Title = "Task B", TaskTime = "08:15:30" });

        var tasks = (await _repo.GetAllAsync()).ToList();

        Assert.Equal(2, tasks.Count);
        Assert.Contains(tasks, t => t.TaskTime == "14:00");
        Assert.Contains(tasks, t => t.TaskTime == "08:15:30");
    }

    [Fact]
    public async Task UpdateAsync_ChangesTaskTime()
    {
        var created = await _repo.CreateAsync(new TodoTask { Title = "Original", TaskTime = "10:00" });

        var updated = await _repo.UpdateAsync(created.Id, new TodoTask
        {
            Title = "Updated",
            TaskTime = "18:30"
        });

        Assert.NotNull(updated);
        Assert.Equal("18:30", updated!.TaskTime);
    }

    [Fact]
    public async Task UpdateAsync_ClearsTaskTimeWhenSetToNull()
    {
        var created = await _repo.CreateAsync(new TodoTask { Title = "With time", TaskTime = "07:00" });

        var updated = await _repo.UpdateAsync(created.Id, new TodoTask
        {
            Title = "Without time",
            TaskTime = null
        });

        Assert.NotNull(updated);
        Assert.Null(updated!.TaskTime);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectTaskTime()
    {
        var created = await _repo.CreateAsync(new TodoTask { Title = "Precise", TaskTime = "23:59:59" });

        var retrieved = await _repo.GetByIdAsync(created.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("23:59:59", retrieved!.TaskTime);
    }

    /// <summary>
    /// A <see cref="DbService"/> substitute that opens fresh connections to a named
    /// shared-cache in-memory SQLite database for testing. This allows each repository
    /// call to own and dispose its connection without destroying the database.
    /// </summary>
    private sealed class InMemoryDbService : DbService
    {
        private readonly string _connectionString;

        public InMemoryDbService(string dbName)
            : base(null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<DbService>.Instance)
        {
            _connectionString = dbName;
        }

        public override async Task<SqliteConnection> GetOpenConnectionAsync()
        {
            var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public override Task PersistAsync()
            => Task.CompletedTask;
    }
}

