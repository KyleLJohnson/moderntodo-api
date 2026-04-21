using Dapper;
using BlazorTodo.Api.Models;

namespace BlazorTodo.Api.Services;

public class TaskRepository
{
    private readonly DbService _db;

    public TaskRepository(DbService db) => _db = db;

    public async Task<IEnumerable<TodoTask>> GetAllAsync(bool? completed = null)
    {
        await using var conn = await _db.GetOpenConnectionAsync();
        var sql = completed.HasValue
            ? "SELECT * FROM Tasks WHERE IsCompleted = @IsCompleted ORDER BY CreatedAt DESC"
            : "SELECT * FROM Tasks ORDER BY CreatedAt DESC";
        return await conn.QueryAsync<TodoTask>(sql, completed.HasValue ? new { IsCompleted = completed.Value ? 1 : 0 } : null);
    }

    public async Task<TodoTask?> GetByIdAsync(int id)
    {
        await using var conn = await _db.GetOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<TodoTask>(
            "SELECT * FROM Tasks WHERE Id = @Id", new { Id = id });
    }

    public async Task<TodoTask> CreateAsync(TodoTask task)
    {
        task.CreatedAt = DateTime.UtcNow;
        task.CompletedAt = task.IsCompleted ? DateTime.UtcNow : null;
        await using var conn = await _db.GetOpenConnectionAsync();
        var id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO Tasks (Title, Description, DueDate, Priority, IsCompleted, CreatedAt, CompletedAt)
            VALUES (@Title, @Description, @DueDate, @Priority, @IsCompleted, @CreatedAt, @CompletedAt);
            SELECT last_insert_rowid();
            """,
            task);
        task.Id = id;
        await _db.PersistAsync();
        return task;
    }

    public async Task<TodoTask?> UpdateAsync(int id, TodoTask updated)
    {
        await using var conn = await _db.GetOpenConnectionAsync();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE Tasks SET
                Title       = @Title,
                Description = @Description,
                DueDate     = @DueDate,
                Priority    = @Priority,
                IsCompleted = @IsCompleted,
                CompletedAt = CASE WHEN @IsCompleted = 1 THEN COALESCE(CompletedAt, @Now) ELSE NULL END
            WHERE Id = @Id
            """,
            new
            {
                updated.Title,
                updated.Description,
                updated.DueDate,
                updated.Priority,
                updated.IsCompleted,
                Now = DateTime.UtcNow,
                Id = id
            });

        if (rows == 0) return null;
        await _db.PersistAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var conn = await _db.GetOpenConnectionAsync();
        var rows = await conn.ExecuteAsync("DELETE FROM Tasks WHERE Id = @Id", new { Id = id });
        if (rows > 0) await _db.PersistAsync();
        return rows > 0;
    }
}
