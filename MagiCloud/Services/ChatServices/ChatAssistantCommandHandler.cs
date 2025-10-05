using MagiCloud.Configuration;
using MagiCommon.Extensions;
using MagiCommon.Models;
using MagiCommon.Models.AssistantChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MagiCloud.Services.ChatServices;

public class ChatAssistantCommandHandler(
    ILogger<ChatAssistantCommandHandler> logger,
    ElasticManager elasticManager,
    TextExtractionQueueWrapper extractionQueue,
    IOptions<AssistantSettings> assistantSettings)
{
    public static Dictionary<string, Function> AvailableFunctionDefinitions { get; } = new()
    {
        ["get_time"] = new()
        {
            Name = "get_time",
            Description = "Gets the current time.",
            Parameters = new()
            {
                Type = "object",
                Properties = new()
            }
        },
        ["get_text"] = new()
        {
            Name = "get_text",
            Description = "Gets a segment of the text rendition of a document. Provide a segment number to request a specific segment.",
            Parameters = new()
            {
                Properties = new()
                {
                    ["doc_id"] = new()
                    {
                        Description = "The ID of the document to get the text of.",
                        Type = "string"
                    },
                    ["segment"] = new()
                    {
                        Description = "The segment number of document to get. Defaults to the first segment (0).",
                        Type = "string"
                    }
                },
                Required = ["doc_id"]
            }
        },
        ["get_metadata"] = new()
        {
            Name = "get_metadata",
            Description = "Gets the metadata of a document, such as owner, last modified, etc.",
            Parameters = new()
            {
                Properties = new()
                {
                    ["doc_id"] = new()
                    {
                        Description = "The ID of the document to get the metadata of.",
                        Type = "string"
                    }
                },
                Required = ["doc_id"]
            }
        },
        ["process"] = new()
        {
            Name = "process",
            Description = "Reruns automatic processing on a document. This is a long process and requires user confirmation.",
            Parameters = new()
            {
                Properties = new()
                {
                    ["doc_id"] = new()
                    {
                        Description = "The ID of the document to process.",
                        Type = "string"
                    }
                },
                Required = ["doc_id"]
            }
        },
        ["search"] = new()
        {
            Name = "search",
            Description = "Searches through the user's documents for the given keywords.",
            Parameters = new()
            {
                Properties = new()
                {
                    ["keywords"] = new()
                    {
                        Description = "The keywords to search for.",
                        Type = "string"
                    }
                },
                Required = ["keywords"]
            }
        }
        // TODO: Add search_within function
        // It has to also take the highlights and return which segment it's in
    };

    public async Task<OpenAIResponse> HandleCommandsAsync(Chat chat, string userId, Message functionMessage)
    {
        var functionCall = functionMessage.FunctionCall;

        // switch on the function name, pass the arguments as-is
        object response = functionCall.Name switch
        {
            "get_time" => new Dictionary<string, object> { ["time"] = DateTimeOffset.Now },
            "get_text" => await HandleTextCommand(userId, functionCall.Arguments),
            "get_metadata" => await HandleMetadataCommand(userId, functionCall.Arguments),
            "process" => await HandleProcessCommand(userId, functionCall.Arguments),
            "search" => await HandleSearchCommand(userId, functionCall.Arguments),
            _ => new Dictionary<string, object> { ["message"] = "Unknown command" }
        };

        if (response is null)
        {
            return null;
        }
        
        var completion = await chat.SendMessage(new()
        {
            Role = Role.Function,
            Content = JsonSerializer.Serialize(response),
            Name = functionCall.Name,
            FunctionCall = new FunctionCall { Id = functionCall.Id } // Pass through the ID
        });
        return completion;
    }

    private async Task<Dictionary<string, object>> HandleSearchCommand(string userId, string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments) || string.IsNullOrWhiteSpace(userId))
        {
            // Not enough info to search with
            return null;
        }

        // try to extract the search terms from the arguments
        string searchTerms = null;
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, string>>(arguments);
            args?.TryGetValue("keywords", out searchTerms);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize arguments '{Arguments}' for user '{UserId}'", arguments, userId);
        }
        if (string.IsNullOrWhiteSpace(searchTerms))
        {
            return new()
            {
                ["Message"] = "Failed to parse arguments"
            };
        }

        logger.LogInformation("Assistant is searching for query '{Query}' for user '{UserId}'", searchTerms, userId);

        var response = new Dictionary<string, object>
        {
            ["search_terms"] = searchTerms,
            ["result_count"] = 0,
            ["results"] = null
        };

        var searchResults = await elasticManager.FileRepo.SearchAsync(userId, searchTerms);
        // Convert the searchResults into a message
        if (searchResults?.Where(d => !d.IsDeleted)?.Any() != true)
        {
            // No results, return an empty response
            return response;
        }
        // We have results, grab the top N
        var filteredResults = searchResults.Where(d => !d.IsDeleted).Take(assistantSettings.Value.MaxSearchResults);
        response["result_count"] = filteredResults.Count();
        var jsonResultList = new List<Dictionary<string, string>>();
        response["results"] = jsonResultList;
        foreach (var doc in filteredResults)
        {
            jsonResultList.Add(new()
            {
                ["doc_id"] = doc.Id,
                ["filename"] = doc.GetFileName(),
                ["highlight"] = doc.Highlights?.FirstOrDefault()
            });
        }

        return response;
    }

    private async Task<Dictionary<string, string>> HandleTextCommand(string userId, string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments) || string.IsNullOrWhiteSpace(userId))
        {
            // Not enough info to work off of
            return null;
        }

        var extractedArgs = ExtractArguments(arguments);
        var docId = extractedArgs?.GetValueOrDefault("doc_id");
        if (string.IsNullOrWhiteSpace(docId))
        {
            return null;
        }
        var (access, doc) = await elasticManager.FileRepo.GetDocumentAsync(userId, docId, true);

        if (access is not (FileAccessResult.FullAccess or FileAccessResult.ReadOnly))
        {
            return new()
            {
                ["doc_id"] = docId,
                ["message"] = "Document not found or user does not have permission."
            };
        }
        // Limit text to N characters
        var segment = 0;
        var segmentStr = extractedArgs?.GetValueOrDefault("segment");
        if (!string.IsNullOrWhiteSpace(segmentStr) && int.TryParse(segmentStr, out var segmentInt))
        {
            segment = segmentInt;
        }

        var charLimit = assistantSettings.Value.TextSegmentLength;
        var originalLength = doc.Text?.Length ?? 0;
        var startIndex = segment * charLimit;
        var endIndex = startIndex + charLimit;
        if (startIndex >= originalLength)
        {
            return new()
            {
                ["doc_id"] = doc.Id,
                ["segment"] = segment.ToString(),
                ["message"] = "Segment out of range."
            };
        }
        if (endIndex > originalLength)
        {
            endIndex = originalLength;
        }
        var docText = doc.Text?[startIndex..endIndex] ?? string.Empty;

        return new()
        {
            ["doc_id"] = doc.Id,
            ["text"] = docText,
            ["segment"] = segment.ToString(),
            ["segment_length"] = docText.Length.ToString(),
            ["total_length"] = originalLength.ToString()
        };
    }

    private async Task<Dictionary<string, string>> HandleMetadataCommand(string userId, string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments) || string.IsNullOrWhiteSpace(userId))
        {
            // Not enough info to work off of
            return null;
        }

        var docId = TryToGetDocIdFromArg(arguments);
        if (string.IsNullOrWhiteSpace(docId))
        {
            return null;
        }
        var (access, doc) = await elasticManager.FileRepo.GetDocumentAsync(userId, docId, false);

        if (access is not (FileAccessResult.FullAccess or FileAccessResult.ReadOnly))
        {
            return new()
            {
                ["doc_id"] = docId,
                ["message"] = "Document not found or user does not have permission."
            };
        }


        var serialized = JsonSerializer.Serialize(doc);
        var deserialized = JsonSerializer.Deserialize<ElasticFileInfo>(serialized);

        deserialized.Hash = null;
        deserialized.Name = deserialized.GetFileName();

        var reserializedDoc = JsonSerializer.Serialize(deserialized);
        return new()
        {
            ["doc_id"] = doc.Id,
            ["metadata"] = reserializedDoc
        };
    }

    private async Task<Dictionary<string, string>> HandleProcessCommand(string userId, string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments) || string.IsNullOrWhiteSpace(userId))
        {
            // Not enough info to work off of
            return null;
        }

        var docId = TryToGetDocIdFromArg(arguments);
        if (docId is null)
        {
            return null;
        }
        var (access, doc) = await elasticManager.FileRepo.GetDocumentAsync(userId, docId, false);

        if (access != FileAccessResult.FullAccess)
        {
            return new()
            {
                ["doc_id"] = doc.Id,
                ["message"] = $"Document not found or user does not have permission."
            };
        }

        extractionQueue.AddFileToQueue(doc.Id);
        return new()
        {
            ["doc_id"] = doc.Id,
            ["message"] = "Document scheduled for reprocessing. Could take several minutes. No feedback will be provided."
        };
    }

    private string TryToGetDocIdFromArg(string arg)
    {
        var args = ExtractArguments(arg);

        if (args?.TryGetValue("doc_id", out var obj) == true 
            && obj is string docId)
        {
            return docId;
        }
        
        return null;
    }

    private Dictionary<string, string> ExtractArguments(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return null;
        }

        // In theory the arguments should be json like {"doc_id": "1234" }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(arg);
        }
        catch (Exception)
        {
            // Couldn't deserialize so we could try regex or something but for now just log it
            logger.LogError("Could not deserialize arguments '{Arguments}'", arg);
        }
        return null;
    }
}
