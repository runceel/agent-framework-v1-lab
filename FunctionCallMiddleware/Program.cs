using System.ComponentModel;
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

[Description("指定された場所の天気を取得します。")]
static string GetWeather(
    [Description("天気を取得する場所")] string location)
    => $"{location}の天気は曇りで、最高気温は15°Cです。";

AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "あなたはネコ型アシスタントです。語尾に必ず「にゃん」を付けてください。",
        tools: [AIFunctionFactory.Create(GetWeather, name: "GetWeather")]);

// Function calling ミドルウェアを追加したエージェントを構築
var middlewareAgent = agent
    .AsBuilder()
    .Use(FunctionCallLoggingMiddleware)
    .Build();

Console.WriteLine(await middlewareAgent.RunAsync("品川の天気はどうですか？"));

// Function calling ミドルウェア: ツール呼び出しの前後にログを出す
async ValueTask<object?> FunctionCallLoggingMiddleware(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"[FunctionCall] ツール呼び出し開始: {context.Function.Name}");
    var result = await next(context, cancellationToken);
    Console.WriteLine($"[FunctionCall] ツール呼び出し完了: {context.Function.Name} => {result}");
    return result;
}
