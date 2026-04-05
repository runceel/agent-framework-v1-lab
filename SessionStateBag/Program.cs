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

// セッションを作成
AgentSession session = await agent.CreateSessionAsync();

// StateBag にユーザー設定を保存
session.StateBag.SetValue("userPreferences", new UserPreferences
{
    NickName = "かずき",
    FavoriteColor = "青"
});

// StateBag からユーザー設定を取得
if (session.StateBag.TryGetValue<UserPreferences>("userPreferences", out var prefs))
{
    Console.WriteLine($"[StateBag] ニックネーム: {prefs!.NickName}, 好きな色: {prefs.FavoriteColor}");
}

// 1回目の実行: StateBag に保存した情報をエージェントに伝える
Console.WriteLine("\n=== 1回目の実行 ===");
var savedPrefs = session.StateBag.GetValue<UserPreferences>("userPreferences");
Console.WriteLine(await agent.RunAsync(
    $"私のニックネームは{savedPrefs!.NickName}で、好きな色は{savedPrefs.FavoriteColor}です。覚えてね。",
    session));

// 2回目の実行: StateBag の値は同じセッション内で保持されている
Console.WriteLine("\n=== 2回目の実行 ===");
savedPrefs = session.StateBag.GetValue<UserPreferences>("userPreferences");
Console.WriteLine($"[StateBag] ニックネーム: {savedPrefs!.NickName} (セッション内で保持)");
Console.WriteLine(await agent.RunAsync("私のニックネームは何だった？", session));

// StateBag の項目を削除
session.StateBag.TryRemoveValue("userPreferences");
Console.WriteLine($"\n[StateBag] 削除後の項目数(userPreferences): {(session.StateBag.TryGetValue<UserPreferences>("userPreferences", out _) ? "残っている" : "削除済み")}");

class UserPreferences
{
    public string? NickName { get; set; }
    public string? FavoriteColor { get; set; }
}
