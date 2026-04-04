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
        $"You are a translation assistant that translates the provided text to {targetLanguage}.",
        $"{targetLanguage}Translator");

// Sequential ワークフローを構築
var workflow = AgentWorkflowBuilder.BuildSequential([
    GetTranslationAgent("English"),
    GetTranslationAgent("French"),
    GetTranslationAgent("Spanish")
]);

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, new ChatMessage(ChatRole.User, "こんにちは、世界！"));

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent e)
    {
        Console.WriteLine($"{e.ExecutorId}: {e.Data}");
    }
}
