using MagiCommon.Interfaces;
using OpenAI.Responses;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MagiCommon.Models.AssistantChat;

public class Chat
{
    // Store ALL messages for display purposes
    public List<ResponseItem> ConversationHistory { get; } = [];
    private string? LastResponseId { get; set; }

    private IChatCompletionService ChatService { get; }
    private ChatCompletionRequest InitialRequest { get; }

    public Chat(
        IChatCompletionService chatService,
        ChatCompletionRequest initialRequest,
        string systemMessage)
    {
        ChatService = chatService;
        InitialRequest = initialRequest;
        
        // Add system message as a ResponseItem
        ConversationHistory.Add(ResponseItem.CreateSystemMessageItem([
            ResponseContentPart.CreateInputTextPart(systemMessage ?? string.Empty)
        ]));
    }

    public async Task<OpenAIResponse> StartChatAsync()
    {
        var response = await SendUserMessage(string.Empty);
        return response;
    }

    public async Task<OpenAIResponse> SendUserMessage(string messageContent)
    {
        // Build request with only new user message
        var requestHistory = new List<ResponseItem>();
        
        // Add new user message to both request and conversation history
        if (!string.IsNullOrEmpty(messageContent))
        {
            var userMessage = ResponseItem.CreateUserMessageItem([
                ResponseContentPart.CreateInputTextPart(messageContent)
            ]);
            requestHistory.Add(userMessage);
            ConversationHistory.Add(userMessage); // Keep for display
        }
        else
        {
            // Add the system message to the request history if this is the start of the chat
            var systemMessage = ConversationHistory[0];
            requestHistory.Add(systemMessage);
        }

        var request = new ChatCompletionRequest
            {
                Model = InitialRequest.Model,
                ConversationHistory = requestHistory, //new message
                Functions = InitialRequest.Functions,
                Temperature = InitialRequest.Temperature,
                TopP = InitialRequest.TopP,
                MaxTokens = InitialRequest.MaxTokens,
                User = InitialRequest.User,
                PreviousResponseId = LastResponseId // API pulls previous context via this
            };

        var response = await ChatService.CreateCompletionAsync(request).ConfigureAwait(false);
        
        // Store response ID for next request
        LastResponseId = response.Id;
        
        // Add all output items to conversation history for display purposes
        foreach (var outputItem in response.OutputItems)
        {
            ConversationHistory.Add(outputItem);
        }
        
        return response;
    }

    public async Task<OpenAIResponse> SendFunctionResult(string callId, string functionName, string result)
    {
        // Build request with only new function result
        var requestHistory = new List<ResponseItem>();
        
        // Add function result to both request and conversation history
        var functionOutput = ResponseItem.CreateFunctionCallOutputItem(
            callId: callId,
            functionOutput: result
        );
        requestHistory.Add(functionOutput);
        ConversationHistory.Add(functionOutput); // Keep for display

        var request = new ChatCompletionRequest
        {
            Model = InitialRequest.Model,
            ConversationHistory = requestHistory, // Only system + function result
            Functions = InitialRequest.Functions,
            Temperature = InitialRequest.Temperature,
            TopP = InitialRequest.TopP,
            MaxTokens = InitialRequest.MaxTokens,
            User = InitialRequest.User,
            PreviousResponseId = LastResponseId // API pulls previous context via this
        };

        var response = await ChatService.CreateCompletionAsync(request).ConfigureAwait(false);
        
        // Store response ID
        LastResponseId = response.Id;
        
        // Add all output items to conversation history for display purposes
        foreach (var outputItem in response.OutputItems)
        {
            ConversationHistory.Add(outputItem);
        }
        
        return response;
    }
}

public class ChatCompletionRequest
{
    public string? Model { get; set; }
    public List<ResponseItem> ConversationHistory { get; set; } = new List<ResponseItem>();
    public List<Function>? Functions { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? MaxTokens { get; set; }
    public string? User { get; set; }
    public string? PreviousResponseId { get; set; }
}

// Function definition types for tool registration
public class Function
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FunctionParameters Parameters { get; set; } 
        = new FunctionParameters();
}

public class FunctionParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";
    
    [JsonPropertyName("properties")]
    public Dictionary<string, FunctionProperty> Properties { get; set; } = new Dictionary<string, FunctionProperty>();

    [JsonPropertyName("required")]
    public string[] Required { get; set; } = [];
}

public class FunctionProperty
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("enum")]
    public string[]? Enum { get; set; }
}