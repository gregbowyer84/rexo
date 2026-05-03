namespace Rexo.Execution;

internal interface IConfigBuiltinModule
{
    void Register(BuiltinRegistry registry, ConfigBuiltinModuleContext context);
}
