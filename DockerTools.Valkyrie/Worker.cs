using System.ComponentModel;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Kenbi.DockerTools.Valkyrie;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string? _uri;
    private readonly string _instanceId;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _uri = configuration.GetSection("Uri").Value;
        _instanceId = configuration.GetSection("InstanceId").Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Valkyrie...");
        _logger.LogInformation("Uri: {Uri}", _uri);
        _logger.LogInformation("Instance Id: {InstanceId}", _instanceId);
        
        
        Thread.Sleep(new TimeSpan(0, 1, 30));

        using (var client = string.IsNullOrWhiteSpace(_uri) ? new DockerClientConfiguration().CreateClient() : new DockerClientConfiguration(new Uri(_uri)).CreateClient())
        {
            _logger.LogInformation("Getting affected containers...");
            var ids = (await client.Containers.ListContainersAsync(
                    new ContainersListParameters
                    {
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            {
                                "label",
                                new Dictionary<string, bool>
                                {
                                    { "DockerTools=True", true },
                                    { $"InstanceId={_instanceId}", true }
                                }
                            }
                        }
                    },
                    CancellationToken.None))
                .Select(x => x.ID);
            _logger.LogInformation("Found {number} containers...", ids.Count());

            var tasks = ids.Select(id => client.Containers.RemoveContainerAsync(
                id,
                new ContainerRemoveParameters
                {
                    Force = true,
                    RemoveVolumes = true
                },
                CancellationToken.None));

            await Task.WhenAll(tasks);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }
}