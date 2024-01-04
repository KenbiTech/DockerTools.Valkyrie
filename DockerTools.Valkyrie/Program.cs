namespace Kenbi.DockerTools.Valkyrie;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services => { services.AddHostedService<Worker>(); })
            .Build();

        using (var source = new CancellationTokenSource())
        {
            source.CancelAfter(new TimeSpan(0, 2, 0));
            var token = source.Token;
            await host.RunAsync(token);
        }
    }
}