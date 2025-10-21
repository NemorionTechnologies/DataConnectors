using System.Threading;
using System.Threading.Tasks;

namespace DataWorkflows.Engine.Templating;

public interface ITemplateEngine
{
    Task<string> RenderAsync(string templateJson, object model, CancellationToken ct);
}

