using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BlazorTodo.Api.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        var connectionString = context.Configuration["BLOB_CONNECTION_STRING"]
            ?? context.Configuration["AzureWebJobsStorage"];

        services.AddSingleton(_ => new BlobServiceClient(connectionString));
        services.AddSingleton<DbService>();
        services.AddSingleton<TaskRepository>();
    })
    .Build();

await host.RunAsync();
