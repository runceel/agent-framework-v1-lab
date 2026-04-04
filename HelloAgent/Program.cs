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

// 非ストリーミングでの呼び出し
Console.WriteLine(await agent.RunAsync("自己紹介をしてください。"));

Console.WriteLine();

// ストリーミングでの呼び出し
await foreach (var update in agent.RunStreamingAsync("今日の関東の天気を適当に予想して。"))
{
    Console.Write(update);
}
Console.WriteLine();

