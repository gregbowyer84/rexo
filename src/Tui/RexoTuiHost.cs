namespace Rexo.Tui;

using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        await AppHost.RunAsync<Routes>(
            parameters: null,
            configure: builder =>
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
            },
            cancellationToken: cancellationToken);
    }
}
