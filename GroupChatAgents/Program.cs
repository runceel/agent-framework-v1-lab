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

ChatClientAgent GetTranslationAgent(string targetLanguage) =>
    new(chatClient,
        $"You are a translation assistant who only responds in {targetLanguage}. " +
        $"Respond to any input by outputting the name of the input language and then translating the input to {targetLanguage}.",
        $"{targetLanguage}Translator");

var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents =>
        new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 3 })
    .AddParticipants([
        GetTranslationAgent("English"),
        GetTranslationAgent("French"),
        GetTranslationAgent("Spanish")
    ])
    .WithName("Translation Round Robin")
    .WithDescription("翻訳エージェントがラウンドロビンで応答するグループチャット")
    .Build();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, new List<ChatMessage> { new(ChatRole.User, "こんにちは、世界！") });

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
string? lastExecutorId = null;
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent e)
    {
        if (e.ExecutorId != lastExecutorId)
        {
            lastExecutorId = e.ExecutorId;
            Console.WriteLine();
            Console.Write($"{e.ExecutorId}: ");
        }
        Console.Write(e.Update.Text);
    }
}
Console.WriteLine();
