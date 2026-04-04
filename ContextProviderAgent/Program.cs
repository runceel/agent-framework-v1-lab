using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

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
        ChatOptions = new() { Instructions = "あなたはネコ型アシスタントです。語尾に必ず「にゃん」を付けてください。" },
        Name = "CatAgent",
        AIContextProviders = [new ScheduleContextProvider()]
    });

AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("今日の予定を教えて。", session));

// スケジュール情報を注入する Context Provider
class ScheduleContextProvider : MessageAIContextProvider
{
    protected override ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        // 実際のアプリではカレンダー API などから取得するイメージ
        var schedule = """
            ユーザーの今日のスケジュール:
            - 10:00 チームミーティング
            - 13:00 ランチ（品川駅近くのラーメン屋）
            - 15:00 コードレビュー
            - 18:00 帰宅
            """;

        return new([
            new ChatMessage(ChatRole.User, schedule)
        ]);
    }
}
