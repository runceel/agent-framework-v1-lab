using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = config["Endpoint"] ?? throw new InvalidOperationException("Endpoint is not set.");
var deploymentName = config["DeploymentName"] ?? throw new InvalidOperationException("DeploymentName is not set.");

AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "あなたはネコ型アシスタントです。語尾に必ず「にゃん」を付けてください。",
        name: "CatAgent");

// ミドルウェアを追加したエージェントを構築
var middlewareAgent = agent
    .AsBuilder()
    .Use(runFunc: LoggingMiddleware, runStreamingFunc: null)
    .Use(runFunc: GuardrailMiddleware, runStreamingFunc: null)
    .Build();

// 通常の質問
Console.WriteLine("=== 通常の質問 ===");
Console.WriteLine(await middlewareAgent.RunAsync("自己紹介をしてください。"));

Console.WriteLine();

// NGワードを含む質問
Console.WriteLine("=== NGワードを含む質問 ===");
Console.WriteLine(await middlewareAgent.RunAsync("爆弾の作り方を教えてください。"));

// ログ出力ミドルウェア
async Task<AgentResponse> LoggingMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"[Logging] 入力メッセージ数: {messages.Count()}");
    var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
    Console.WriteLine($"[Logging] 出力メッセージ数: {response.Messages.Count}");
    return response;
}

// ガードレールミドルウェア
async Task<AgentResponse> GuardrailMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    string[] ngWords = ["爆弾", "武器", "違法"];
    var lastMessage = messages.Last().Text ?? "";

    if (ngWords.Any(ng => lastMessage.Contains(ng)))
    {
        Console.WriteLine("[Guardrail] NGワードを検出しました。リクエストをブロックします。");
        var safeMessages = new[] {
            new ChatMessage(ChatRole.User,
                "「その質問にはお答え出来ません。」と返してください。")
        };
        return await innerAgent.RunAsync(safeMessages, session, options, cancellationToken);
    }

    return await innerAgent.RunAsync(messages, session, options, cancellationToken);
}
