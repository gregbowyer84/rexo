namespace Rexo.Core.Models;

public static class RuntimeCapabilityCatalog
{
    public const string ContractVersion = "1.0";

    private static readonly string[] SupportedCapabilitiesInternal =
    [
        "commands.merge.layer",
        "commands.merge.replace",
        "commands.merge.append",
        "commands.merge.prepend",
        "commands.merge.wrap",
        "commands.step-ops.v1",
        "outputs.contract.v1",
        "settings.bag.v1",
        "vars.bag.v1",
        "policy.sources.v1",
        "runtime.parity.artifact-provider.v1",
        "runtime.parity.version-provider.v1",
        "diagnostics.capabilities-command.v1",
    ];

    public static IReadOnlyList<string> SupportedCapabilities => SupportedCapabilitiesInternal;

    public static bool IsSupported(string capability) =>
        SupportedCapabilitiesInternal.Contains(capability, StringComparer.OrdinalIgnoreCase);
}
