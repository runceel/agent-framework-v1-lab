using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage; // OpenAI.Chat.ChatMessage との名前衝突を回避

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var app = builder.Build();

var endpoint = app.Configuration["Endpoint"] ?? throw new InvalidOperationException("Endpoint is not set.");
var deploymentName = app.Configuration["DeploymentName"] ?? throw new InvalidOperationException("DeploymentName is not set.");

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
        tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(GetWeather, name: "GetWeather"))]);

// セッションと承認リクエストをシリアライズして保存（実際は CosmosDB 等に置き換え可能）
ConcurrentDictionary<string, string> sessionStore = new();
ConcurrentDictionary<string, string> approvalStore = new();

// チャットエンドポイント
app.MapPost("/chat", async (ChatRequest request) =>
{
    var sessionId = request.SessionId ?? Guid.NewGuid().ToString("N");

    // セッションをデシリアライズして復元、なければ新規作成
    AgentSession session;
    if (sessionStore.TryGetValue(sessionId, out var sessionJson))
    {
        using var doc = JsonDocument.Parse(sessionJson);
        session = await agent.DeserializeSessionAsync(doc.RootElement);
    }
    else
    {
        session = await agent.CreateSessionAsync();
    }

    AgentResponse response = await agent.RunAsync(request.Message, session);

    // セッションをシリアライズして保存
    sessionStore[sessionId] = JsonSerializer.Serialize(
        await agent.SerializeSessionAsync(session));

    var approvalRequests = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<ToolApprovalRequestContent>()
        .ToList();

    if (approvalRequests.Count > 0)
    {
        // 承認リクエストをシリアライズして保存
        approvalStore[sessionId] = JsonSerializer.Serialize(
            approvalRequests, AIJsonUtilities.DefaultOptions);
        return Results.Ok(new ChatResponse(
            SessionId: sessionId,
            Message: null,
            PendingApprovals: approvalRequests.Select(r =>
            {
                var fc = (FunctionCallContent)r.ToolCall;
                return new PendingApproval(
                    r.RequestId,
                    fc.Name,
                    fc.Arguments?.ToDictionary(a => a.Key, a => a.Value?.ToString() ?? "") ?? []);
            }).ToList()));
    }

    return Results.Ok(new ChatResponse(
        SessionId: sessionId,
        Message: response.Text,
        PendingApprovals: []));
});

// 承認エンドポイント
app.MapPost("/approve", async (ApproveRequest request) =>
{
    if (!sessionStore.TryGetValue(request.SessionId, out var sessionJson))
        return Results.NotFound("セッションが見つかりません。");

    if (!approvalStore.TryRemove(request.SessionId, out var approvalJson))
        return Results.NotFound("承認待ちのリクエストが見つかりません。");

    // デシリアライズして復元
    AgentSession session;
    {
        using var doc = JsonDocument.Parse(sessionJson);
        session = await agent.DeserializeSessionAsync(doc.RootElement);
    }
    var pending = JsonSerializer.Deserialize<List<ToolApprovalRequestContent>>(
        approvalJson, AIJsonUtilities.DefaultOptions)!;

    // 承認結果から ChatMessage を組み立てる
    var userResponses = pending.Select(approvalRequest =>
    {
        var decision = request.Decisions.FirstOrDefault(d => d.RequestId == approvalRequest.RequestId);
        var approved = decision?.Approved ?? false;
        return new ChatMessage(ChatRole.User, [approvalRequest.CreateResponse(approved)]);
    }).ToList();

    AgentResponse response = await agent.RunAsync(userResponses, session);

    // セッションをシリアライズして保存
    sessionStore[request.SessionId] = JsonSerializer.Serialize(
        await agent.SerializeSessionAsync(session));

    var newApprovals = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<ToolApprovalRequestContent>()
        .ToList();

    if (newApprovals.Count > 0)
    {
        approvalStore[request.SessionId] = JsonSerializer.Serialize(
            newApprovals, AIJsonUtilities.DefaultOptions);
        return Results.Ok(new ChatResponse(
            SessionId: request.SessionId,
            Message: null,
            PendingApprovals: newApprovals.Select(r =>
            {
                var fc = (FunctionCallContent)r.ToolCall;
                return new PendingApproval(
                    r.RequestId,
                    fc.Name,
                    fc.Arguments?.ToDictionary(a => a.Key, a => a.Value?.ToString() ?? "") ?? []);
            }).ToList()));
    }

    return Results.Ok(new ChatResponse(
        SessionId: request.SessionId,
        Message: response.Text,
        PendingApprovals: []));
});

app.Run();

record ChatRequest(string Message, string? SessionId = null);
record ChatResponse(string SessionId, string? Message, List<PendingApproval> PendingApprovals);
record PendingApproval(string RequestId, string FunctionName, Dictionary<string, string> Arguments);
record ApproveRequest(string SessionId, List<ApprovalDecision> Decisions);
record ApprovalDecision(string RequestId, bool Approved);
