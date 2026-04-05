using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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

AgentSession session = await agent.CreateSessionAsync();

// 会話を 2 ターン行う
Console.WriteLine("=== 会話 ===");
Console.WriteLine(await agent.RunAsync("好きな動物は何？", session));
Console.WriteLine(await agent.RunAsync("その動物の豆知識を1つ教えて。", session));

// セッションからチャット履歴を取得
Console.WriteLine("\n=== チャット履歴の取得 ===");
if (session.TryGetInMemoryChatHistory(out var messages))
{
    Console.WriteLine($"メッセージ数: {messages.Count}");
    foreach (var msg in messages)
    {
        var text = msg.Text ?? "(non-text)";
        Console.WriteLine($"  [{msg.Role}] {text[..Math.Min(50, text.Length)]}{(text.Length > 50 ? "..." : "")}");
    }
}

// チャット履歴を差し替えて別の文脈で会話を継続
Console.WriteLine("\n=== チャット履歴を差し替え ===");
session.SetInMemoryChatHistory([
    new ChatMessage(ChatRole.User, "私の名前はかずきです。"),
    new ChatMessage(ChatRole.Assistant, "こんにちは、かずきさんにゃん！"),
]);
Console.WriteLine(await agent.RunAsync("私の名前を覚えてる？", session));

// 差し替え後の履歴を確認
if (session.TryGetInMemoryChatHistory(out var updatedMessages))
{
    Console.WriteLine($"\n差し替え後のメッセージ数: {updatedMessages.Count}");
    foreach (var msg in updatedMessages)
    {
        var text = msg.Text ?? "(non-text)";
        Console.WriteLine($"  [{msg.Role}] {text[..Math.Min(50, text.Length)]}{(text.Length > 50 ? "..." : "")}");
    }
}
