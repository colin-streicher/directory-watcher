using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
namespace DirectoryWatcher;





public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    
    private string? WatchDirectory;
    private string? UploadContainer;
    private string? StorageAccountName;
    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    private void Setconfig()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        WatchDirectory = config.GetRequiredSection("WatchDirectory")
            .GetSection("Directory")
            .Get<string>();
        StorageAccountName = config.GetRequiredSection("UploadStorageAccount")
            .GetSection("AccountName")
            .Get<string>();
        UploadContainer = config.GetRequiredSection("UploadStorageAccount")
            .GetSection("ContainerName")
            .Get<string>();
    }
    
    private BlobServiceClient GetBlobServiceClient(string? accountName)
    {
        var blobServiceClient = new BlobServiceClient(
            new Uri($"https://{accountName}.blob.core.windows.net"),
            new DefaultAzureCredential());
        return blobServiceClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Setconfig();
        _logger.LogInformation($"Watching Directory {WatchDirectory}, " +
                               $"uploading to storage account {StorageAccountName} " +
                               $"in container {UploadContainer}");
        await Task.Run(RunWatcher , stoppingToken);
    }

    private void RunWatcher()
    {

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        if (!Directory.Exists(WatchDirectory))
        {
            _logger.LogCritical($"Directory {WatchDirectory} does not exist, nothing to do");
            return;
        }
        
        var watcher = new FileSystemWatcher($"{WatchDirectory}");
        _logger.LogInformation("Watching directory: {directory}", watcher.Path);
        watcher.Filter = "";
        watcher.IncludeSubdirectories = false;
        watcher.EnableRaisingEvents = true;
        watcher.Created += OnCreated;
    }
    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        
        var client = GetBlobServiceClient(StorageAccountName);
        var containerClient = client.GetBlobContainerClient(UploadContainer);
        
        var blobClient = containerClient.GetBlobClient($"backups/{DateTime.Now.ToString("yyyy-MM-dd")}/{e.Name}");
        Thread.Sleep(5000);
        _logger.LogInformation($"Uploading: {e.FullPath} to backups/{DateTime.Now.ToString("yyyy-MM-dd")}/{e.Name}");
        
        try
        {
            blobClient.Upload(e.FullPath, true);
            _logger.LogInformation("Upload complete");
        }
        catch (RequestFailedException exception)
        {
            _logger.LogError($"Failed to upload file {e.FullPath}: {exception}");
        }
    }
}