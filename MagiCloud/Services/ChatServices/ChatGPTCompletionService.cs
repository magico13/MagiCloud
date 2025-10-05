using MagiCloud.Configuration;
using MagiCommon.Extensions;
using MagiCommon.Interfaces;
using MagiCommon.Models;
using MagiCommon.Models.AssistantChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace MagiCloud.Services.ChatServices;

public class ChatGPTCompletionService(
    OpenAIResponseClient responseClient, 
    ILogger<ChatGPTCompletionService> logger, 
    IOptions<AssistantSettings> assistantSettings) : IChatCompletionService
{
    private const string GENERAL_SYSTEM_MESSAGE = @"You're the MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and ask how you can help but do not start with a function. For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Document links: Use [link text](/view/{{ID}}) to link to a file and embed images into the chat with ![image name](/api/filecontent/{{ID}})

The chat window supports markdown formatting.

Chatting with user {0} (id={1}), Chat Start Time: {2}.

{3}";

    public async Task<OpenAIResponse> CreateCompletionAsync(ChatCompletionRequest request)
    {
        var responseItems = ConvertMessagesToResponseItems(request.Messages);
        var options = ConvertToResponseCreationOptions(request);

        try
        {
            var response = await responseClient.CreateResponseAsync(responseItems, options);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create completion");
            throw;
        }
    }

    private static List<ResponseItem> ConvertMessagesToResponseItems(List<Message> messages)
    {
        var items = new List<ResponseItem>();
        
        foreach (var message in messages)
        {
            switch (message.Role)
            {
                case Role.System:
                    items.Add(ResponseItem.CreateSystemMessageItem([
                        ResponseContentPart.CreateInputTextPart(message.Content ?? string.Empty)
                    ]));
                    break;
                    
                case Role.User:
                    items.Add(ResponseItem.CreateUserMessageItem([
                        ResponseContentPart.CreateInputTextPart(message.Content ?? string.Empty)
                    ]));
                    break;
                    
                case Role.Assistant:
                    if (message.FunctionCall != null)
                    {
                        // This shouldn't happen in request - function calls come from responses
                        // But if we need to represent it, create a function call item
                        items.Add(new FunctionCallResponseItem(
                            callId: message.FunctionCall.Id ?? Guid.NewGuid().ToString(),
                            functionName: message.FunctionCall.Name,
                            functionArguments: BinaryData.FromString(message.FunctionCall.Arguments ?? "{}")
                        ));
                    }
                    else
                    {
                        items.Add(ResponseItem.CreateAssistantMessageItem([
                            ResponseContentPart.CreateOutputTextPart(message.Content ?? string.Empty, annotations: null)
                        ]));
                    }
                    break;
                    
                case Role.Function:
                    // Function result - use the stored call ID
                    var callId = message.FunctionCall?.Id ?? message.Name ?? Guid.NewGuid().ToString();
                    items.Add(ResponseItem.CreateFunctionCallOutputItem(
                        callId: callId,
                        functionOutput: message.Content ?? string.Empty
                    ));
                    break;
            }
        }
        
        return items;
    }

    private ResponseCreationOptions ConvertToResponseCreationOptions(ChatCompletionRequest request)
    {
        var options = new ResponseCreationOptions();

        if (request.Temperature.HasValue)
            options.Temperature = (float)request.Temperature.Value;
            
        if (request.TopP.HasValue)
            options.TopP = (float)request.TopP.Value;
            
        if (request.MaxTokens.HasValue)
            options.MaxOutputTokenCount = request.MaxTokens.Value;

        // Pass previous response ID for better context management
        if (!string.IsNullOrEmpty(request.PreviousResponseId))
        {
            options.PreviousResponseId = request.PreviousResponseId;
        }

        // Configure reasoning options
        var reasoningEffort = assistantSettings.Value.ReasoningEffort?.ToLowerInvariant() switch
        {
            "low" => ResponseReasoningEffortLevel.Low,
            "medium" => ResponseReasoningEffortLevel.Medium,
            "high" => ResponseReasoningEffortLevel.High,
            _ => ResponseReasoningEffortLevel.Medium
        };

        options.ReasoningOptions = new ResponseReasoningOptions
        {
            ReasoningEffortLevel = reasoningEffort
        };

        // Only set summary verbosity if summaries are enabled
        // If not set (null), the API won't generate summaries
        if (assistantSettings.Value.IncludeReasoningSummaries)
        {
            options.ReasoningOptions.ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Auto;
        }

        // Enable response storage for better context management
        options.StoredOutputEnabled = assistantSettings.Value.StoreResponses;

        // Add tools/functions
        if (request.Functions != null)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            foreach (var function in request.Functions)
            {
                var tool = ResponseTool.CreateFunctionTool(
                    functionName: function.Name,
                    functionDescription: function.Description,
                    functionParameters: BinaryData.FromObjectAsJson(function.Parameters, jsonOptions),
                    strictModeEnabled: false
                );
                options.Tools.Add(tool);
            }
        }

        return options;
    }

    public Chat CreateNewGeneralChat(
        ChatCompletionRequest initialRequest,
        string username,
        string userId,
        string additionalContext,
        ElasticFileInfo fileContext = null)
    {
        if (string.IsNullOrEmpty(initialRequest.Model))
        {
            initialRequest.Model = assistantSettings.Value.Model;
        }
        additionalContext ??= string.Empty;
        // Reserialize to break any references
        if (fileContext is not null)
        {
            var serialized = JsonSerializer.Serialize(fileContext);
            var deserialized = JsonSerializer.Deserialize<ElasticFileInfo>(serialized);

            deserialized.Hash = null;
            deserialized.Name = deserialized.GetFileName();

            var serializedContext = JsonSerializer.Serialize(deserialized);
            additionalContext += "The file context is: " + serializedContext ?? "null";
        }

        var finalSystemMessage = string.Format(
            GENERAL_SYSTEM_MESSAGE,
            username,
            userId,
            DateTimeOffset.Now.ToString("MM/dd/yyyy h:mm tt z"),
            additionalContext);

        return new(
            this,
            initialRequest,
            finalSystemMessage);
    }
}
