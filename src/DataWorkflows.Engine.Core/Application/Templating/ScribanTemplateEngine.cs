using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace DataWorkflows.Engine.Core.Application.Templating;

public sealed class ScribanTemplateEngine : ITemplateEngine
{
    private readonly TemplateEngineOptions _options;

    public ScribanTemplateEngine(TemplateEngineOptions options)
    {
        _options = options;
    }

    public async Task<string> RenderAsync(string templateJson, object model, CancellationToken ct)
    {
        var parsed = Template.Parse(templateJson);
        if (parsed.HasErrors)
        {
            var errors = string.Join("; ", parsed.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Template parse error: {errors}")
            ;
        }

        // Enforce sandbox: no loops/functions
        EnforceSandbox(parsed);

        var context = new TemplateContext
        {
            MemberRenamer = member => member.Name, // keep names as-is
            StrictVariables = _options.StrictMode,
            TemplateLoader = null // disables includes/files
        };

        // Prevent built-in functions registration by using a minimal script object
        var scriptObj = new ScriptObject();
        scriptObj.Import(model, renamer: member => member.Name);
        context.PushGlobal(scriptObj);

        var renderTask = parsed.RenderAsync(context).AsTask();
        var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(_options.RenderTimeoutMs), ct);
        var completed = await Task.WhenAny(renderTask, timeoutTask).ConfigureAwait(false);
        if (completed != renderTask)
        {
            throw new TimeoutException("Template render timed out");
        }
        return await renderTask.ConfigureAwait(false);
    }

    private void EnforceSandbox(Template parsed)
    {
        // Walk AST and reject loops/functions
        var page = parsed.Page;
        var checker = new SandboxChecker(_options);
        checker.Visit(page);
    }

    private sealed class SandboxChecker : ScriptVisitor
    {
        private readonly TemplateEngineOptions _options;
        public SandboxChecker(TemplateEngineOptions options) => _options = options;

        public override void Visit(ScriptForStatement node)
        {
            if (!_options.EnableLoops) throw new InvalidOperationException("Loops are disabled in templates");
            base.Visit(node);
        }

        public override void Visit(ScriptWhileStatement node)
        {
            if (!_options.EnableLoops) throw new InvalidOperationException("Loops are disabled in templates");
            base.Visit(node);
        }

        public override void Visit(ScriptFunctionCall node)
        {
            if (!_options.EnableFunctions) throw new InvalidOperationException("Functions are disabled in templates");
            base.Visit(node);
        }

        public override void Visit(ScriptAnonymousFunction node)
        {
            if (!_options.EnableFunctions) throw new InvalidOperationException("Functions are disabled in templates");
            base.Visit(node);
        }
    }
}
