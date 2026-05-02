namespace Rexo.Tui;

using Microsoft.AspNetCore.Components.Routing;

internal sealed class NoopNavigationInterception : INavigationInterception
{
    public Task EnableNavigationInterceptionAsync() => Task.CompletedTask;
}
