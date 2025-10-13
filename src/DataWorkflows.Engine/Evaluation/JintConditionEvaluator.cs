using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace DataWorkflows.Engine.Evaluation;

public sealed class ConditionScope
{
    public ConditionScope(
        object trigger,
        IReadOnlyDictionary<string, object?> context,
        IReadOnlyDictionary<string, object?> vars)
    {
        Trigger = trigger;
        Context = context;
        Vars = vars;
    }

    public object Trigger { get; }
    public IReadOnlyDictionary<string, object?> Context { get; }
    public IReadOnlyDictionary<string, object?> Vars { get; }

    public static readonly ConditionScope Empty = new(
        trigger: new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>()),
        context: new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>()),
        vars: new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>()));
}

public sealed class JintConditionEvaluator
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    private const int DefaultMaxStatements = 500;
    private const int DefaultMaxRecursionDepth = 10;
    private const long DefaultMemoryLimitBytes = 4 * 1024 * 1024;

    public bool Evaluate(string? condition, ConditionScope scope)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        try
        {
            var engine = new Jint.Engine(options =>
            {
                options.TimeoutInterval(DefaultTimeout);
                options.LimitRecursion(DefaultMaxRecursionDepth);
                options.MaxStatements(DefaultMaxStatements);
                options.LimitMemory(DefaultMemoryLimitBytes);
            });

            engine.SetValue("trigger", scope.Trigger);
            engine.SetValue("context", scope.Context);
            engine.SetValue("vars", scope.Vars);

            var result = engine.Evaluate(condition);
            return JsValueToBoolean(result);
        }
        catch (JavaScriptException ex)
        {
            Console.WriteLine($"Condition evaluation error: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Condition evaluation timeout or error: {ex.Message}");
            return false;
        }
    }

    private static bool JsValueToBoolean(JsValue value)
    {
        try
        {
            return Jint.Runtime.TypeConverter.ToBoolean(value);
        }
        catch
        {
            return false;
        }
    }
}

