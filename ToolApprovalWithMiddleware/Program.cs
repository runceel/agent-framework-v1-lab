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

// ApprovalRequiredAIFunction でツールをラップ
AIAgent baseAgent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "あなたはネコ型アシスタントです。語尾に必ず「にゃん」を付けてください。",
        tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(GetWeather, name: "GetWeather"))]);

// Agent Run ミドルウェアで承認ループを自動化
AIAgent agent = baseAgent
    .AsBuilder()
    .Use(ConsoleApprovalMiddleware, null)
    .Build();

Console.WriteLine(await agent.RunAsync("品川の天気はどうですか？"));

// 承認ループを処理するミドルウェア
async Task<AgentResponse> ConsoleApprovalMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    var approvalRequests = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<ToolApprovalRequestContent>()
        .ToList();

    while (approvalRequests.Count > 0)
    {
        var userResponses = approvalRequests.ConvertAll(request =>
        {
            var functionCall = (FunctionCallContent)request.ToolCall;
            Console.WriteLine($"[承認リクエスト] ツール: {functionCall.Name}, 引数: {string.Join(", ", functionCall.Arguments?.Select(a => $"{a.Key}={a.Value}") ?? [])}");
            Console.Write("承認しますか？ (Y/N): ");
            var approved = Console.ReadLine()?.Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false;
            Console.WriteLine($"→ {(approved ? "承認" : "拒否")}しました。\n");
            return new ChatMessage(ChatRole.User, [request.CreateResponse(approved)]);
        });

        response = await innerAgent.RunAsync(userResponses, session, options, cancellationToken);
        approvalRequests = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<ToolApprovalRequestContent>()
            .ToList();
    }

    return response;
}
