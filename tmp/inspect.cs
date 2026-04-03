using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Reflection;

var type = typeof(AgentResponseItem<StreamingChatMessageContent>);
Console.WriteLine($"Properties for {type.Name}:");
foreach (var prop in type.GetProperties())
{
    Console.WriteLine($" - {prop.Name} ({prop.PropertyType.Name})");
}
