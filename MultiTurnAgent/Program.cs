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

// セッションを作成して会話のコンテキストを保持
AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine(await agent.RunAsync("好きな食べ物は何？", session));
Console.WriteLine();
Console.WriteLine(await agent.RunAsync("それを使ったレシピを1つ教えて。", session));

