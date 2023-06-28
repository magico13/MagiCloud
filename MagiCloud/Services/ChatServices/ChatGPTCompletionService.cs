using MagiCommon.Extensions;
using MagiCommon.Interfaces;
using MagiCommon.Models;
using MagiCommon.Models.AssistantChat;
using MagiCommon.Serialization;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MagiCloud.Services.ChatServices;

public class ChatGPTCompletionService : IChatCompletionService
{
    private const string DOCUMENT_SYSTEM_MESSAGE = @"You're the MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and a guess at what the document is about without using a function (eg This looks to be a pdf of a form 1040 tax document). For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Document links: Use [link text](/view/{4}) and embed images with ![image name](/api/filecontent/{4})

The chat window supports markdown formatting.

Chatting with user {0} with user ID {1}, Chat Start Time: {2}.

This chat is in the context of a single document with ID {4}. The details of the document being discussed in this chat are:
{3}";

    private const string GENERAL_SYSTEM_MESSAGE = @"You're the MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and ask how you can help but do not start with a function. For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Document links: Use [link text](/view/{{ID}}) and embed images with ![image name](/api/filecontent/{{ID}})

The chat window supports markdown formatting.

Chatting with user {0}, Chat Start Time: {1}.

{2}";

    //private const int MAX_TEXT_LENGTH = 8192;
    private JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
        Converters = { new RoleJsonConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private HttpClient HttpClient { get; }
    private ILogger<ChatGPTCompletionService> Logger { get; }

    public ChatGPTCompletionService(HttpClient httpClient, ILogger<ChatGPTCompletionService> logger)
    {
        HttpClient = httpClient;
        Logger = logger;
    }

    public async Task<ChatCompletionResponse> CreateCompletionAsync(ChatCompletionRequest request)
    {
        var strContent = JsonSerializer.Serialize(request, JsonSerializerOptions);
        var content = new StringContent(strContent, Encoding.UTF8, "application/json");
        var response = await HttpClient.PostAsync("v1/chat/completions", content);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to create completion: {ResponseText}", await response.Content.ReadAsStringAsync());
        }
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent, JsonSerializerOptions);
    }

    public Chat CreateNewDocumentChat(
        ChatCompletionRequest initialRequest,
        string username,
        string userId,
        ElasticFileInfo fileContext)
    {
        // Reserialize to break any references
        var serialized = JsonSerializer.Serialize(fileContext);
        var deserialized = JsonSerializer.Deserialize<ElasticFileInfo>(serialized);
        //if (deserialized.Text?.Length > MAX_TEXT_LENGTH)
        //{
        //    deserialized.Text = deserialized.Text[..MAX_TEXT_LENGTH];
        //}

        deserialized.Hash = null;
        deserialized.Name = deserialized.GetFileName();

        var serializedContext = JsonSerializer.Serialize(deserialized, JsonSerializerOptions);

        return new(this,
            initialRequest,
            string.Format(DOCUMENT_SYSTEM_MESSAGE,
                username,
                userId,
                DateTimeOffset.Now.ToString("MM/dd/yyyy h:mm tt z"),
                serializedContext,
                deserialized.Id));
    }

    public Chat CreateNewGeneralChat(
        ChatCompletionRequest initialRequest,
        string username,
        string additionalContext) => new(
            this,
            initialRequest,
            string.Format(
                GENERAL_SYSTEM_MESSAGE,
                username,
                DateTimeOffset.Now.ToString("MM/dd/yyyy h:mm tt z"),
                additionalContext));
}
