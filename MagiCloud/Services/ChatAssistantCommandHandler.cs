using MagiCommon.Extensions;
using MagiCommon.Models.AssistantChat;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class ChatAssistantCommandHandler
{

    // This lets the chat service use commands to run searches, get info about a file, queue files for rextraction, etc
    private ILogger<ChatAssistantCommandHandler> Logger { get; }
    private IElasticManager ElasticManager { get; }
    private TextExtractionQueueHelper ExtractionQueue { get; }

    public ChatAssistantCommandHandler(
        ILogger<ChatAssistantCommandHandler> logger,
        IElasticManager elasticManager,
        TextExtractionQueueHelper extractionQueue)
    {
        Logger = logger;
        ElasticManager = elasticManager;
        ExtractionQueue = extractionQueue;
    }

    public async Task<ChatCompletionResponse> HandleCommandsAsync(Chat chat, string userId, ChatCompletionResponse recentResponse)
    {
        var message = recentResponse?.Choices.FirstOrDefault()?.Message.Content;
        if (string.IsNullOrWhiteSpace(message)) { return recentResponse; }
        // Commands look like this:
        // #cmd:search search terms
        // #cmd:info docId
        // #cmd:process docId

        StringBuilder response = new StringBuilder();

        foreach (var line in message.Split('\n'))
        {
            if (line.Contains("#cmd:"))
            {
                // Find where the command starts, then go to the end of the line
                var startOfCmd = line.Substring(line.IndexOf("#cmd:"));

                var args = startOfCmd.Split(' ', 2);
                if (args.Length == 2)
                {
                    var cmdResult = string.Empty;

                    switch (args[0])
                    {
                        case "#cmd:search": 
                            cmdResult = await HandleSearchCommand(userId, args[1]);
                            break;
                        case "#cmd:info":
                            cmdResult = "Handling for #cmd:info is not implemented yet"; 
                            break;
                        case "#cmd:process": 
                            cmdResult = await HandleProcessCommand(userId, args[1]);
                            break;
                        default:
                            Logger.LogWarning("Unknown command '{Command}'", line); 
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(cmdResult))
                    {
                        response.AppendLine(cmdResult);
                    }
                }
            }
        }
        if (response.Length > 0)
        {
            var completion = await chat.SendMessage(new Message
            {
                Role = Role.System,
                Content = response.ToString()
            });
            return completion;
        }
        return recentResponse;

    }

    private async Task<string> HandleSearchCommand(string userId, string searchTerms)
    {
        if (string.IsNullOrWhiteSpace(searchTerms) || string.IsNullOrWhiteSpace(userId))
        {
            // Not enough info to search with
            return null;
        }

        // If the search terms has a backtick then GPT is being dumb and put it inline, end the query there
        searchTerms = searchTerms.Split('`').First().Trim();

        Logger.LogInformation("Assistant is searching for query '{Query}' for user '{UserId}'", searchTerms, userId);
        var searchResults = await ElasticManager.SearchAsync(userId, searchTerms);
        // Convert the searchResults into a message
        if (searchResults?.Any() != true)
        {
            // No results, return a message stating no results found for search query
            return $"No results found for query '{searchTerms}'";
        }
        // We have results, repeat the query and give the top 5
        var filteredResults = searchResults.Take(5);
        var resultBuilder = new StringBuilder($"Top search results for '{searchTerms}':\n");
        foreach (var doc in filteredResults)
        {
            resultBuilder.AppendLine($"ID={doc.Id},N={doc.GetFullPath()},P={(doc.IsPublic ? 1 : 0)},U={doc.LastUpdated.ToUnixTimeSeconds()},S={doc.Size}");
            if (doc.Highlights?.Any() == true)
            {
                resultBuilder.AppendLine($"Highlight '{doc.Highlights.First()}'");
            }
        }

        return resultBuilder.ToString();
    }

    private async Task<string> HandleProcessCommand(string userId, string docId)
    {
        if (string.IsNullOrWhiteSpace(docId) || string.IsNullOrWhiteSpace(userId))
        {
            // Not enough info to work off of
            return null;
        }

        // The doc id should not have spaces or backticks
        docId = docId.Split(' ', '`').First().Trim();

        var (access, doc) = await ElasticManager.GetDocumentAsync(userId, docId, false);
        
        if (access != FileAccessResult.FullAccess)
        {
            return $"Doc id {docId} not found or user does not have permission";
        }

        ExtractionQueue.AddFileToQueue(doc.Id);
        return $"Doc id {docId} scheduled for reprocessing. Could take several minutes and no feedback provided.";
    }
}
