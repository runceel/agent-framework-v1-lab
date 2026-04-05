using System.Net.Http.Json;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

Console.WriteLine($"API サーバー: {baseUrl}");
Console.Write("メッセージを入力してください: ");
var message = Console.ReadLine() ?? "";

// /chat にメッセージを送信
var chatResponse = await http.PostAsJsonAsync("/chat", new { Message = message });
chatResponse.EnsureSuccessStatusCode();
var result = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();

while (result?.PendingApprovals.Count > 0)
{
    Console.WriteLine("\n--- 承認待ちのツール呼び出しがあります ---");
    var decisions = new List<ApprovalDecision>();

    foreach (var approval in result.PendingApprovals)
    {
        Console.WriteLine($"[承認リクエスト] ツール: {approval.FunctionName}, 引数: {string.Join(", ", approval.Arguments.Select(a => $"{a.Key}={a.Value}"))}");
        Console.Write("承認しますか？ (Y/N): ");
        var approved = Console.ReadLine()?.Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false;
        Console.WriteLine($"→ {(approved ? "承認" : "拒否")}しました。");
        decisions.Add(new ApprovalDecision(approval.RequestId, approved));
    }

    // /approve に承認結果を送信
    var approveResponse = await http.PostAsJsonAsync("/approve",
        new { SessionId = result.SessionId, Decisions = decisions });
    approveResponse.EnsureSuccessStatusCode();
    result = await approveResponse.Content.ReadFromJsonAsync<ChatResponse>();
}

Console.WriteLine($"\nエージェント: {result?.Message}");

record ChatResponse(string SessionId, string? Message, List<PendingApproval> PendingApprovals);
record PendingApproval(string RequestId, string FunctionName, Dictionary<string, string> Arguments);
record ApprovalDecision(string RequestId, bool Approved);
