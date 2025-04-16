using MagiCloud.Configuration;
using MagiCommon.Extensions;
using MagiCommon.Interfaces;
using MagiCommon.Models;
using MagiCommon.Models.AssistantChat;
using MagiCommon.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MagiCloud.Services.ChatServices;

public class ChatGPTCompletionService(HttpClient httpClient, ILogger<ChatGPTCompletionService> logger, IOptions<AssistantSettings> assistantSettings) : IChatCompletionService
{
    private const string GENERAL_SYSTEM_MESSAGE = @"You're the MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and ask how you can help but do not start with a function. For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Document links: Use [link text](/view/{{ID}}) to link to a file and embed images into the chat with ![image name](/api/filecontent/{{ID}})

The chat window supports markdown formatting.

Chatting with user {0} (id={1}), Chat Start Time: {2}.

{3}";

    //private const int MAX_TEXT_LENGTH = 8192;
    private JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
        Converters = { new RoleJsonConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ChatCompletionResponse> CreateCompletionAsync(ChatCompletionRequest request)
    {
        var strContent = JsonSerializer.Serialize(request, JsonSerializerOptions);
        var content = new StringContent(strContent, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("v1/chat/completions", content);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to create completion: {ResponseText}", await response.Content.ReadAsStringAsync());
        }
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent, JsonSerializerOptions);
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

            var serializedContext = JsonSerializer.Serialize(deserialized, JsonSerializerOptions);
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
