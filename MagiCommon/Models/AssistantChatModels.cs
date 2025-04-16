using MagiCommon.Interfaces;
using MagiCommon.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MagiCommon.Models.AssistantChat
{
    public class Chat
    {
        public List<Message> Messages { get; } = new List<Message>();

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

        public Task<ChatCompletionResponse> StartChatAsync()
            => SendMessage(new Message { Content = SystemMessage, Role = Role.System });

        public Task<ChatCompletionResponse> SendUserMessage(string nextMessageContent)
            => SendMessage(new Message
            {
                Role = Role.User,
                Content = nextMessageContent,
                Name = InitialRequest.User
            });

        public async Task<ChatCompletionResponse> SendMessage(Message message)
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
                User = InitialRequest.User
            };

            var completed = await ChatService.CreateCompletionAsync(request).ConfigureAwait(false);
            var firstChoice = completed.Choices.First().Message;
            firstChoice.Content ??= string.Empty;
            Messages.Add(firstChoice);
            return completed;
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
    }

    public class ChatCompletionResponse
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public int Created { get; set; }
        public List<Choice> Choices { get; set; }
        public Usage Usage { get; set; }
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
        public string Type { get; set; } = "object";
        public Dictionary<string, FunctionProperty> Properties { get; set; }
        public string[] Required { get; set; }
    }

    public class FunctionProperty
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public string[] Enum { get; set; }
    }

    public class Choice
    {
        public int Index { get; set; }
        public Message Message { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FinishReason? FinishReason { get; set; }
    }

    public class Usage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public enum Role
    {
        System,
        User,
        Assistant,
        Function
    }

    public enum FinishReason
    {
        Stop,
        Length,
        Content_Filter,
        Function_Call
    }
}