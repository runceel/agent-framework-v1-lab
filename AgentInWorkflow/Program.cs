using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = config["Endpoint"] ?? throw new InvalidOperationException("Endpoint is not set.");
var deploymentName = config["DeploymentName"] ?? throw new InvalidOperationException("DeploymentName is not set.");

var chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();

// 翻訳エージェントを作成
AIAgent englishAgent = new ChatClientAgent(chatClient,
    "あなたは翻訳アシスタントです。入力されたテキストを英語に翻訳してください。翻訳結果のみを出力してください。");
AIAgent frenchAgent = new ChatClientAgent(chatClient,
    "あなたは翻訳アシスタントです。入力されたテキストをフランス語に翻訳してください。翻訳結果のみを出力してください。");

// ワークフローを構築: 英語に翻訳 → フランス語に翻訳
var workflow = new WorkflowBuilder(englishAgent)
    .AddEdge(englishAgent, frenchAgent)
    .Build();

// 実行
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, new ChatMessage(ChatRole.User, "こんにちは、世界！"));

// TurnToken を送ってエージェントの処理を開始
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent update)
    {
        Console.WriteLine($"{update.ExecutorId}: {update.Data}");
    }
}
