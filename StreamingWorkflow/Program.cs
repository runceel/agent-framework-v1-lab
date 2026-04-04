using Microsoft.Agents.AI.Workflows;

Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
var uppercase = uppercaseFunc.BindAsExecutor("UppercaseExecutor");
ReverseTextExecutor reverse = new();

WorkflowBuilder builder = new(uppercase);
builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
var workflow = builder.Build();

// ストリーミングで実行
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: "Hello, World!");
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is ExecutorCompletedEvent executorCompleted)
    {
        Console.WriteLine($"{executorCompleted.ExecutorId}: {executorCompleted.Data}");
    }
}

class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(
        string message, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(string.Concat(message.Reverse()));
    }
}
