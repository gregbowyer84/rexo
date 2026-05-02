namespace Rexo.Tui;

using Microsoft.AspNetCore.Components.Routing;

internal sealed class NoopScrollToLocationHash : IScrollToLocationHash
{
    public Task RefreshScrollPositionForHash(string locationAbsolute) => Task.CompletedTask;
}
