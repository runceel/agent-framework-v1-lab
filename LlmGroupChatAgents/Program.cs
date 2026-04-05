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

ChatClientAgent sightseeingGuide = new(chatClient,
    "あなたは広島の観光ガイドです。広島の観光スポットについて1〜2文で簡潔に紹介してください。",
    "SightseeingGuide");

ChatClientAgent foodGuide = new(chatClient,
    "あなたは広島のグルメガイドです。広島のご当地グルメについて1〜2文で簡潔に紹介してください。",
    "FoodGuide");

ChatClientAgent summarizer = new(chatClient,
    "あなたはまとめ役です。これまでの会話を踏まえて、旅行プランを簡潔にまとめてください。",
    "Summarizer");

var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents =>
        new LlmGroupChatManager(chatClient, agents) { MaximumIterationCount = 3 })
    .AddParticipants([sightseeingGuide, foodGuide, summarizer])
    .WithName("Hiroshima Travel Planning")
    .WithDescription("広島旅行の計画をエージェントが議論するグループチャット")
    .Build();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, new List<ChatMessage> { new(ChatRole.User, "広島に日帰り旅行に行きたいです。おすすめを教えてください。") });

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
string? lastExecutorId = null;
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent e)
    {
        if (e.ExecutorId != lastExecutorId)
        {
            lastExecutorId = e.ExecutorId;
            Console.WriteLine();
            Console.Write($"{e.ExecutorId}: ");
        }
        Console.Write(e.Update.Text);
    }
}
Console.WriteLine();

// LLM ベースのグループチャットマネージャー
class LlmGroupChatManager(IChatClient chatClient, IReadOnlyList<AIAgent> agents) : GroupChatManager
{
    protected override async ValueTask<AIAgent> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        var agentNames = string.Join(", ", agents.Select(a => a.Name));
        var prompt = $"""
            あなたはグループチャットの司会者です。
            参加者: {agentNames}

            会話の流れを見て、次に発言すべき参加者の名前だけを返してください。
            名前のみを返し、それ以外は何も出力しないでください。
            """;

        List<ChatMessage> messages = [.. history, new(ChatRole.User, prompt)];
        var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var selectedName = response.Text.Trim();

        return agents.FirstOrDefault(a => selectedName.Contains(a.Name!)) ?? agents[0];
    }
}
