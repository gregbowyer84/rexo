namespace Rexo.Core.Abstractions;

using Rexo.Core.Models;

public interface ITemplateRenderer
{
    string Render(string templateText, ExecutionContext context);
}
