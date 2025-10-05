using MagiCommon.Interfaces;
using MagiCommon.Serialization;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MagiCommon.Models.AssistantChat
{
    public class Chat
    {
        public List<Message> Messages { get; } = new List<Message>();
        private string LastResponseId { get; set; }

        private IChatCompletionService ChatService { get; }
        private ChatCompletionRequest InitialRequest { get; }
        private string SystemMessage { get; }

        public Chat(
            IChatCompletionService chatService,
            ChatCompletionRequest initialRequest,
            string systemMessage)
        {
            ChatService = chatService;
            InitialRequest = initialRequest;
            SystemMessage = systemMessage;
        }

        public Task<OpenAIResponse> StartChatAsync()
            => SendMessage(new Message { Content = SystemMessage, Role = Role.System });

        public Task<OpenAIResponse> SendUserMessage(string nextMessageContent)
            => SendMessage(new Message
            {
                Role = Role.User,
                Content = nextMessageContent,
                Name = InitialRequest.User
            });

        public async Task<OpenAIResponse> SendMessage(Message message)
        {
            Messages.Add(message);
            var request = new ChatCompletionRequest
            {
                Model = InitialRequest.Model,
                Messages = Messages,
                Functions = InitialRequest.Functions,
                Temperature = InitialRequest.Temperature,
                TopP = InitialRequest.TopP,
                N = InitialRequest.N,
                Stream = InitialRequest.Stream,
                Stop = InitialRequest.Stop,
                MaxTokens = InitialRequest.MaxTokens,
                PresencePenalty = InitialRequest.PresencePenalty,
                FrequencyPenalty = InitialRequest.FrequencyPenalty,
                LogitBias = InitialRequest.LogitBias,
                User = InitialRequest.User,
                PreviousResponseId = LastResponseId // Pass previous response ID for better context
            };

            var response = await ChatService.CreateCompletionAsync(request).ConfigureAwait(false);
            
            // Store response ID for next request
            LastResponseId = response.Id;
            
            // Convert all response items to Messages for conversation history
            var assistantMessages = ConvertResponseToMessages(response);
            foreach (var assistantMessage in assistantMessages)
            {
                assistantMessage.Content ??= string.Empty;
                Messages.Add(assistantMessage);
            }
            
            return response;
        }

        private List<Message> ConvertResponseToMessages(OpenAIResponse response)
        {
            var messages = new List<Message>();
            
            // Process all output items - there may be multiple (reasoning, messages, function calls)
            foreach (var item in response.OutputItems)
            {
                if (item is MessageResponseItem messageItem)
                {
                    // Combine all text content parts
                    var textContent = string.Join("\n", 
                        messageItem.Content
                            .Where(c => !string.IsNullOrEmpty(c.Text))
                            .Select(c => c.Text));
                    
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        messages.Add(new Message
                        {
                            Role = Role.Assistant,
                            Content = textContent
                        });
                    }
                }
                else if (item is FunctionCallResponseItem functionCallItem)
                {
                    messages.Add(new Message
                    {
                        Role = Role.Assistant,
                        FunctionCall = new FunctionCall
                        {
                            Name = functionCallItem.FunctionName,
                            Arguments = functionCallItem.FunctionArguments?.ToString() ?? string.Empty,
                            Id = functionCallItem.CallId
                        }
                    });
                }
                else if (item is ReasoningResponseItem reasoningItem)
                {
                    // Extract reasoning summary if available
                    var summaryText = reasoningItem.GetSummaryText();
                    
                    if (!string.IsNullOrEmpty(summaryText))
                    {
                        messages.Add(new Message
                        {
                            Role = Role.Reasoning,
                            Content = summaryText
                        });
                    }
                }
            }
            
            return messages;
        }
    }

    public class ChatCompletionRequest
    {
        public string Model { get; set; }
        public List<Message> Messages { get; set; } = new List<Message>();
        public List<Function> Functions { get; set; }
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public int? N { get; set; }
        public bool? Stream { get; set; }
        public string[] Stop { get; set; }
        public int? MaxTokens { get; set; }
        public double? PresencePenalty { get; set; }
        public double? FrequencyPenalty { get; set; }
        public Dictionary<string, double> LogitBias { get; set; }
        public string User { get; set; }
        public string PreviousResponseId { get; set; }
    }

    public class Message
    {
        [JsonConverter(typeof(RoleJsonConverter))]
        public Role Role { get; set; }
        public string Content { get; set; }
        public string Name { get; set; }
        public FunctionCall FunctionCall { get; set; }
    }

    public class FunctionCall
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string Id { get; set; }
    }

    public class Function
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public FunctionParameters Parameters { get; set; } 
            = new FunctionParameters() { Properties = new Dictionary<string, FunctionProperty>() };
    }

    public class FunctionParameters
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";
        
        [JsonPropertyName("properties")]
        public Dictionary<string, FunctionProperty> Properties { get; set; } = new Dictionary<string, FunctionProperty>();

        [JsonPropertyName("required")]
        public string[] Required { get; set; } = Array.Empty<string>();
    }

    public class FunctionProperty
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        [JsonPropertyName("enum")]
        public string[] Enum { get; set; }
    }

    public enum Role
    {
        System,
        User,
        Assistant,
        Function,
        Reasoning
    }
}