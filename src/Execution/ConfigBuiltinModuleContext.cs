namespace Rexo.Execution;

using Rexo.Configuration.Models;

internal sealed record ConfigBuiltinModuleContext(
    ConfigCommandLoader Loader,
    RepoConfig Config,
    string RepositoryRoot);
