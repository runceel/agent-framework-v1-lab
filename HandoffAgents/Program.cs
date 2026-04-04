using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = config["Endpoint"] ?? throw new InvalidOperationException("Endpoint is not set.");
var deploymentName = config["DeploymentName"] ?? throw new InvalidOperationException("DeploymentName is not set.");

var chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();

ChatClientAgent historyTutor = new(chatClient,
    "あなたは歴史の専門家です。歴史に関する質問にわかりやすく答えてください。歴史以外の質問には答えないでください。",
    "HistoryTutor",
    "歴史に関する質問に答える専門エージェント");

ChatClientAgent mathTutor = new(chatClient,
    "あなたは数学の専門家です。数学の問題をステップバイステップで解説してください。数学以外の質問には答えないでください。",
    "MathTutor",
    "数学に関する質問に答える専門エージェント");

ChatClientAgent triageAgent = new(chatClient,
    "あなたはユーザーの質問を適切な専門エージェントに振り分けるトリアージエージェントです。必ず他のエージェントに handoff してください。",
    "TriageAgent",
    "質問を適切な専門エージェントに振り分けるエージェント");

var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor])
    .WithHandoffs([mathTutor, historyTutor], triageAgent)
    .Build();

List<ChatMessage> messages = [];
while (true)
{
    Console.Write("Q: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) break;

    messages.Add(new(ChatRole.User, input));

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        if (evt is AgentResponseUpdateEvent e)
        {
            Console.Write(e.Update.Text);
        }
        else if (evt is WorkflowOutputEvent output)
        {
            messages = output.As<List<ChatMessage>>()!;
        }
    }
    Console.WriteLine();
}
