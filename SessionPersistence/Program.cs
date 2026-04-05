using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
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
    .AsAIAgent(
        instructions: "あなたはネコ型アシスタントです。語尾に必ず「にゃん」を付けてください。",
        name: "CatAgent");

// === セッション1: 会話して StateBag にも情報を保存 ===
Console.WriteLine("=== セッション1: 会話 ===");
AgentSession session = await agent.CreateSessionAsync();
session.StateBag.SetValue("userName", new UserName { Name = "かずき" });

Console.WriteLine(await agent.RunAsync("こんにちは！私の名前はかずきです。好きな食べ物はラーメンです。", session));
Console.WriteLine(await agent.RunAsync("私の好きな食べ物を覚えてる？", session));

// セッションをシリアライズ（JSON に変換）
JsonElement serialized = await agent.SerializeSessionAsync(session);
string json = JsonSerializer.Serialize(serialized, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine($"\n[シリアライズ] JSON の長さ: {json.Length} 文字");
Console.WriteLine($"[シリアライズ] JSON の先頭 200 文字:\n{json[..Math.Min(200, json.Length)]}...");

// === セッション2: シリアライズした JSON からセッションを復元 ===
Console.WriteLine("\n=== セッション2: 復元した会話 ===");
JsonElement deserialized = JsonSerializer.Deserialize<JsonElement>(json);
AgentSession restoredSession = await agent.DeserializeSessionAsync(deserialized);

// StateBag も復元されている
if (restoredSession.StateBag.TryGetValue<UserName>("userName", out var userName))
{
    Console.WriteLine($"[StateBag] 復元された名前: {userName!.Name}");
}

// 会話のコンテキストが保持されているか確認
Console.WriteLine(await agent.RunAsync("私の好きな食べ物と名前をもう一度教えて。", restoredSession));

class UserName
{
    public string? Name { get; set; }
}
