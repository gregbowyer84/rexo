namespace Rexo.Tui;

using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RazorConsole.Core;
using Rexo.Configuration.Models;
using Rexo.Execution;

public static class RexoTuiHost
{
    public static async Task RunAsync(
        DefaultCommandExecutor executor,
        RepoConfig? config,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        TuiRuntimeContext.Initialize(executor, config, workingDirectory);

        var hostBuilder = Host.CreateDefaultBuilder([])
            .UseRazorConsole<Routes>(configure: builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.TryAddSingleton<INavigationInterception, NoopNavigationInterception>();
                    services.TryAddSingleton<IScrollToLocationHash, NoopScrollToLocationHash>();
                    services.AddSingleton<TuiNavigationService>();
                    services.AddSingleton(executor);
                    if (config is not null)
                    {
                        services.AddSingleton(config);
                    }
                });
            });

        using var host = hostBuilder.Build();
        await host.RunAsync(cancellationToken);
    }
}
