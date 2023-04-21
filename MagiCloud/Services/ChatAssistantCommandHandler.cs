using MagiCommon.Extensions;
using MagiCommon.Models.AssistantChat;
using Microsoft.Extensions.Logging;
using System;
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
        // [sys:search search terms]
        // [sys:text docId]
        // [sys:process docId]

        var response = new StringBuilder();

        foreach (var line in message.Split('\n'))
        {
            if (line.Contains("[sys:"))
            {
                // Find where the command starts, then go to the end of the line
                var startIndex = line.IndexOf("[sys:");
                var endIndex = line.IndexOf(']', startIndex);

                if (startIndex >= 0 && endIndex >= 0)
                {
                    var commandText = line[(startIndex+5)..endIndex];

                    var args = commandText.Split(' ', 2);

                    var cmdResult = string.Empty;

                    switch (args[0])
                    {
                        case "search":
                            cmdResult = args.Length == 2 ? await HandleSearchCommand(userId, args[1]) : null;
                            break;
                        case "text":
                            cmdResult = args.Length == 2 ? await HandleTextCommand(userId, args[1]) : null;
                            break;
                        case "process":
                            cmdResult = args.Length == 2 ? await HandleProcessCommand(userId, args[1]) : null;
                            break;
                        case "time":
                            cmdResult = $"The current time is {DateTimeOffset.Now:MM/dd/yyyy h:mm tt z}";
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
        if (searchResults?.Where(d => !d.IsDeleted)?.Any() != true)
        {
            // No results, return a message stating no results found for search query
            return $"No results found for query '{searchTerms}'";
        }
        // We have results, repeat the query and give the top 5
        var filteredResults = searchResults.Where(d => !d.IsDeleted).Take(5);
        var resultBuilder = new StringBuilder($"Top search results for '{searchTerms}':\n");
        foreach (var doc in filteredResults)
        {
            resultBuilder.AppendLine($"ID={doc.Id},N={doc.GetFullPath()},P={(doc.IsPublic ? 1 : 0)},U={doc.LastUpdated.ToUnixTimeSeconds()},S={doc.Size}");
            if (doc.Highlights?.Any() == true)
            {
                resultBuilder.AppendLine($"Result text '{doc.Highlights.First()}'");
            }
        }

        return resultBuilder.ToString();
    }

    private async Task<string> HandleTextCommand(string userId, string docId)
    {
        if (string.IsNullOrWhiteSpace(docId) || string.IsNullOrWhiteSpace(userId))
        {
            // Not enough info to work off of
            return null;
        }

        docId = TryToGetDocIdFromArg(docId);
        if (docId is null)
        {
            return null;
        }
        var (access, doc) = await ElasticManager.GetDocumentAsync(userId, docId, true);

        if (access is not FileAccessResult.FullAccess or FileAccessResult.ReadOnly)
        {
            return $"Doc id {docId} not found or user does not have permission";
        }
        // Limit text to N characters
        var charLimit = 4000;
        var docText = doc.Text?[..Math.Min(doc.Text.Length, charLimit)];

        return $"First {docText?.Length ?? 0} chars of text of the document: {docText}";
    }

    private async Task<string> HandleProcessCommand(string userId, string docId)
    {
        if (string.IsNullOrWhiteSpace(docId) || string.IsNullOrWhiteSpace(userId))
        {
            // Not enough info to work off of
            return null;
        }

        docId = TryToGetDocIdFromArg(docId);
        if (docId is null)
        {
            return null;
        }
        var (access, doc) = await ElasticManager.GetDocumentAsync(userId, docId, false);

        if (access != FileAccessResult.FullAccess)
        {
            return $"Doc id {docId} not found or user does not have permission";
        }

        ExtractionQueue.AddFileToQueue(doc.Id);
        return $"Doc id {docId} scheduled for reprocessing. Could take several minutes. No feedback will be provided.";
    }

    private string TryToGetDocIdFromArg(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        { 
            return null;
        }

        // The doc id should not have spaces or backticks
        var docId = arg.Split(' ', '`').First().Trim();
        if (arg.Contains("{ID}", StringComparison.OrdinalIgnoreCase))
        {
            // It's telling the user how to run the command (against orders) just ignore it
            return null;
        }

        return docId;
    }
}
