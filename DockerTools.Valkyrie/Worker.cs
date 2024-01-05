using Docker.DotNet;
using Docker.DotNet.Models;

namespace Kenbi.DockerTools.Valkyrie;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _instanceId;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        this._logger = logger;
        this._instanceId = configuration.GetSection("InstanceId").Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation("Starting Valkyrie...");
        this._logger.LogInformation("Instance Id: {InstanceId}", this._instanceId);

        Thread.Sleep(new TimeSpan(0, 1, 30));

        DockerClient? client = null;
        try
        {
            client = await this.CreateClientAsync();

            if (client != null)
            {
                await this.KillContainersAsync(client);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                this._logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }
        finally
        {
            if (client != null)
            {
                await this.SeppukuAsync(client);
            }

            client?.Dispose();
            this._logger.LogInformation("Application has shut down");
        }
    }

    private async Task<DockerClient?> CreateClientAsync()
    {
        DockerClient? client = null;
        var count = -1;

        do
        {
            count++;
            try
            {
                this._logger.LogInformation("Attempting to connect to Docker; attempt #{Count}", count+1);
                client = new DockerClientConfiguration().CreateClient();
                await client.System.PingAsync();
            }
            catch (Exception ex)
            {
                this._logger.LogError("Attempt #{Count} failed: {Message}", count+1, ex.Message);
                client?.Dispose();
                client = null;
            }
        } while (client == null && count < 10);

        return client;
    }

    private async Task KillContainersAsync(IDockerClient client)
    {
        this._logger.LogInformation("Getting affected containers...");
        var ids = (await client.Containers.ListContainersAsync(
                this.CreateFilter(),
                CancellationToken.None))
            .Select(x => x.ID)
            .ToList();
        this._logger.LogInformation("Found {Number} containers...", ids.Count);

        await this.ForceRemoveContainersAsync(client, ids);
    }

    private async Task SeppukuAsync(IDockerClient client)
    {
        this._logger.LogInformation("Getting Valkyrie instances...");
        var ids = (await client.Containers.ListContainersAsync(
                this.CreateFilter(true),
                CancellationToken.None))
            .Select(x => x.ID)
            .ToList();
        this._logger.LogInformation("Found {Number} valkyries...", ids.Count);

        await this.ForceRemoveContainersAsync(client, ids);
    }

    private ContainersListParameters CreateFilter(bool includeValkyrie = false)
    {
        if (includeValkyrie)
        {
            return new ContainersListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    {
                        "label",
                        new Dictionary<string, bool>
                        {
                            //{ $"DockerTools={bool.TrueString}", false },
                            //{ $"InstanceId={this._instanceId}", true },
                            { $"de.kenbi.dockertools.valkyrie={bool.TrueString}", true },
                        }
                    }
                }
            };
        }
        
        return new ContainersListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                {
                    "label",
                    new Dictionary<string, bool>
                    {
                        { $"de.kenbi.dockertools.instance={this._instanceId}", true },
                        { $"de.kenbi.dockertools={bool.TrueString}", true },
                    }
                }
            }
        };
    }

    private Task ForceRemoveContainersAsync(IDockerClient client, IEnumerable<string> ids)
    {
        this._logger.LogInformation("Deleting the following containers: {Ids}", string.Concat(ids,", "));
        var tasks = ids.Select(id => client.Containers.RemoveContainerAsync(
            id,
            new ContainerRemoveParameters
            {
                Force = true,
                RemoveVolumes = true
            },
            CancellationToken.None));

        return Task.WhenAll(tasks);
    }
}