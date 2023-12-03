﻿using MagiCommon.Extensions;
using MagiCommon.Models;
using MagiCommon.Models.AssistantChat;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MagiCloud.Services.ChatServices;

public class ChatAssistantCommandHandler(
    ILogger<ChatAssistantCommandHandler> logger,
    ElasticManager elasticManager,
    TextExtractionQueueWrapper extractionQueue)
{
    public static Dictionary<string, Function> AvailableFunctionDefinitions { get; } = new()
    {
        ["get_time"] = new()
        {
            Name = "get_time",
            Description = "Gets the current time."
        },
        ["get_text"] = new()
        {
            Name = "get_text",
            Description = "Gets the text of a document.",
            Parameters = new()
            {
                Properties = new()
                {
                    ["doc_id"] = new()
                    {
                        Description = "The ID of the document to get the text of.",
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
            Description = "Runs text extraction on a document. Requires user confirmation and is only needed when failing to get text.",
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
            Description = "Searches for documents for the given keywords.",
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
    };

    public async Task<ChatCompletionResponse> HandleCommandsAsync(Chat chat, string userId, Message functionMessage)
    {
        var functionCall = functionMessage.FunctionCall;
        //functionMessage.Content ??= $"{functionCall.Name}: {functionCall.Arguments}";

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
            Name = functionCall.Name
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
        // We have results, grab the top 5
        var filteredResults = searchResults.Where(d => !d.IsDeleted).Take(5);
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

        var docId = TryToGetDocIdFromArg(arguments);
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
        var charLimit = 4000;
        var originalLength = doc.Text?.Length ?? 0;
        var docText = doc.Text?[..Math.Min(doc.Text.Length, charLimit)] ?? string.Empty;

        return new()
        {
            ["doc_id"] = doc.Id,
            ["text"] = docText,
            ["excerpt_length"] = docText.Length.ToString(),
            ["original_length"] = originalLength.ToString()
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
        if (string.IsNullOrWhiteSpace(arg))
        {
            return null;
        }

        // In theory the arguments should be json like {"doc_id": "1234" }
        try
        {
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(arg);
            if (deserialized?.TryGetValue("doc_id", out var docId) == true)
            {
                return docId;
            }
        }
        catch (Exception)
        {
            // Couldn't deserialize so we could try regex or something but for now just log it
            logger.LogError("Could not deserialize arguments '{Arguments}'", arg);
        }
        return null;
    }
}
