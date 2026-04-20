using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BlazorTodo.Api.Models;
using BlazorTodo.Api.Services;

namespace BlazorTodo.Api.Functions;

public class TaskFunctions
{
    private readonly TaskRepository _repo;
    private readonly ILogger<TaskFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public TaskFunctions(TaskRepository repo, ILogger<TaskFunctions> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    [Function("GetTasks")]
    public async Task<HttpResponseData> GetTasks(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks")] HttpRequestData req)
    {
        bool? completed = null;
        if (req.Query["completed"] is { } val)
            completed = val.ToLower() == "true";

        var tasks = await _repo.GetAllAsync(completed);
        return await OkJson(req, tasks);
    }

    [Function("CreateTask")]
    public async Task<HttpResponseData> CreateTask(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks")] HttpRequestData req)
    {
        var task = await ReadBody<TodoTask>(req);
        if (task is null || string.IsNullOrWhiteSpace(task.Title))
            return req.CreateResponse(HttpStatusCode.BadRequest);

        if (string.IsNullOrWhiteSpace(task.TaskTime) ||
            !TimeOnly.TryParseExact(task.TaskTime, "HH:mm", out _))
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var created = await _repo.CreateAsync(task);
        var response = await OkJson(req, created);
        response.StatusCode = HttpStatusCode.Created;
        return response;
    }

    [Function("UpdateTask")]
    public async Task<HttpResponseData> UpdateTask(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tasks/{id:int}")] HttpRequestData req,
        int id)
    {
        var task = await ReadBody<TodoTask>(req);
        if (task is null || string.IsNullOrWhiteSpace(task.Title))
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var updated = await _repo.UpdateAsync(id, task);
        if (updated is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await OkJson(req, updated);
    }

    [Function("DeleteTask")]
    public async Task<HttpResponseData> DeleteTask(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tasks/{id:int}")] HttpRequestData req,
        int id)
    {
        var deleted = await _repo.DeleteAsync(id);
        return req.CreateResponse(deleted ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
    }

    private static async Task<HttpResponseData> OkJson<T>(HttpRequestData req, T data)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(data, JsonOptions));
        return response;
    }

    private static async Task<T?> ReadBody<T>(HttpRequestData req)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(req.Body, JsonOptions);
        }
        catch
        {
            return default;
        }
    }
}
