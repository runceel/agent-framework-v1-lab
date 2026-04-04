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

var chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName);

AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    ChatOptions = new()
    {
        Instructions = """
            あなたはネコ型アシスタントです。語尾に必ず「にゃん」を付けてください。
            ユーザーの名前を知っている場合は名前で呼びかけてください。
            """
    },
    Name = "CatAgent",
    AIContextProviders = [new UserNameMemory(chatClient.AsIChatClient())]
});

// セッション1: 名前を伝える
AgentSession session1 = await agent.CreateSessionAsync();
Console.WriteLine("=== セッション1: 名前を伝える ===");
Console.WriteLine(await agent.RunAsync("こんにちは！私の名前はかずきです。", session1));

// メモリから記憶した名前を取得
var memory = agent.GetService<UserNameMemory>()!;
var userInfo = memory.GetUserInfo(session1);
Console.WriteLine($"\n[Memory] 記憶した名前: {userInfo.Name}");

// セッション2: 新しいセッション（会話履歴なし）にメモリだけ引き継ぐ
Console.WriteLine("\n=== セッション2: 新しいセッション（メモリだけ引き継ぎ） ===");
AgentSession session2 = await agent.CreateSessionAsync();
memory.SetUserInfo(session2, userInfo);
Console.WriteLine(await agent.RunAsync("私の名前を覚えてる？", session2));

// ユーザーの名前を記憶する Context Provider
class UserNameMemory(IChatClient chatClient) : AIContextProvider
{
    private readonly ProviderSessionState<UserInfo> _sessionState = new(
        _ => new UserInfo(),
        nameof(UserNameMemory));

    private IReadOnlyList<string>? _stateKeys;

    public override IReadOnlyList<string> StateKeys => _stateKeys ??= [_sessionState.StateKey];

    public UserInfo GetUserInfo(AgentSession session)
        => _sessionState.GetOrInitializeState(session);

    public void SetUserInfo(AgentSession session, UserInfo userInfo)
        => _sessionState.SaveState(session, userInfo);

    // エージェント実行前: 記憶している名前を指示として注入
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        var userInfo = _sessionState.GetOrInitializeState(context.Session);

        var instructions = userInfo.Name is null
            ? "ユーザーの名前はまだわかりません。"
            : $"ユーザーの名前は「{userInfo.Name}」です。";

        return new(new AIContext
        {
            Instructions = instructions
        });
    }

    // エージェント実行後: 会話からユーザーの名前を抽出して記憶
    protected override async ValueTask StoreAIContextAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        var userInfo = _sessionState.GetOrInitializeState(context.Session);

        if (userInfo.Name is null &&
            context.RequestMessages.Any(x => x.Role == ChatRole.User))
        {
            var result = await chatClient.GetResponseAsync<UserInfo>(
                context.RequestMessages,
                new ChatOptions
                {
                    Instructions = "会話からユーザーの名前を抽出してください。見つからない場合は null を返してください。"
                },
                cancellationToken: cancellationToken);

            userInfo.Name ??= result.Result.Name;
        }

        _sessionState.SaveState(context.Session, userInfo);
    }
}

class UserInfo
{
    public string? Name { get; set; }
}
