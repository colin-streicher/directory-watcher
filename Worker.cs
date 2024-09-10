using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace DirectoryWatcher;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    private BlobServiceClient GetBlobServiceClient(string accountName)
    {
        var blobServiceClient = new BlobServiceClient(
            new Uri($"https://{accountName}.blob.core.windows.net"),
            new DefaultAzureCredential());
        return blobServiceClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        var watcher = new FileSystemWatcher("C:\\test");
        _logger.LogInformation("Watching directory: {directory}", watcher.Path);
        watcher.Filter = "";
        watcher.IncludeSubdirectories = false;
        watcher.EnableRaisingEvents = true;
        watcher.Created += OnCreated;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        var client = GetBlobServiceClient("wizmostorage");
        var containerClient = client.GetBlobContainerClient("uploads");
        var blobClient = containerClient.GetBlobClient($"backups/{DateTime.Now.ToString("yyyy-MM-dd")}/{e.Name}");
        Thread.Sleep(5000);
        var value = $"Uploading: {e.FullPath} to backups/{DateTime.Now.ToString("yyyy-MM-dd")}/{e.Name}";
        Console.WriteLine(value);
        blobClient.Upload(e.FullPath, true);
        Console.WriteLine("Upload complete");
    }
}