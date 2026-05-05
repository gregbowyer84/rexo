namespace Rexo.Execution;

internal sealed class VerificationBuiltinModule : IConfigBuiltinModule
{
    public void Register(BuiltinRegistry registry, ConfigBuiltinModuleContext context)
    {
        _ = registry;
        _ = context;
    }
}
