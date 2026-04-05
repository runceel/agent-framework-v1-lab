using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = config["Endpoint"] ?? throw new InvalidOperationException("Endpoint is not set.");
var deploymentName = config["DeploymentName"] ?? throw new InvalidOperationException("DeploymentName is not set.");

AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new()
        {
            Instructions = "あなたはネコ型アシスタントです。語尾に必ず「にゃん」を付けてください。"
        },
        Name = "CatAgent",
        AIContextProviders = [new VisitCounterProvider()]
    });

AgentSession session = await agent.CreateSessionAsync();

// 3回連続で呼び出して、訪問回数が自動的にカウントされることを確認
Console.WriteLine("=== 1回目 ===");
Console.WriteLine(await agent.RunAsync("今何回目の会話？", session));

Console.WriteLine("\n=== 2回目 ===");
Console.WriteLine(await agent.RunAsync("今は何回目？", session));

Console.WriteLine("\n=== 3回目 ===");
Console.WriteLine(await agent.RunAsync("今は？", session));

// ProviderSessionState の中身は StateBag に保存されている
var provider = agent.GetService<VisitCounterProvider>()!;
var state = provider.GetState(session);
Console.WriteLine($"\n[ProviderSessionState] 訪問回数: {state.Count}");
Console.WriteLine($"[StateBag] キー '{nameof(VisitCounterProvider)}' に保存済み");

// 訪問回数を追跡して指示に注入する Context Provider
class VisitCounterProvider : AIContextProvider
{
    private readonly ProviderSessionState<VisitState> _sessionState = new(
        _ => new VisitState(),
        nameof(VisitCounterProvider));

    private IReadOnlyList<string>? _stateKeys;

    public override IReadOnlyList<string> StateKeys => _stateKeys ??= [_sessionState.StateKey];

    public VisitState GetState(AgentSession session)
        => _sessionState.GetOrInitializeState(session);

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        var state = _sessionState.GetOrInitializeState(context.Session);
        state.Count++;
        _sessionState.SaveState(context.Session, state);

        return new(new AIContext
        {
            Instructions = $"これはユーザーとの {state.Count} 回目の会話です。回数を聞かれたら教えてください。"
        });
    }
}

class VisitState
{
    public int Count { get; set; }
}
