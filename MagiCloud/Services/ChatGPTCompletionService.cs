using MagiCommon.Extensions;
using MagiCommon.Interfaces;
using MagiCommon.Models;
using MagiCommon.Models.AssistantChat;
using MagiCommon.Serialization;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class ChatGPTCompletionService : IChatCompletionService
{
    private const string DOCUMENT_SYSTEM_MESSAGE = @"
You are an assistant for a personal cloud storage website called MagiCloud that is a hobby project. You help users to work with stored documents by summarizing them, extracting the most relevant information, and answering questions about the documents. Your first message should indicate what you can help with and should make a guess at what the current document is (example: ""This document looks like a PDF of a form 1040 tax document."").

You are chatting with the user {0} whose id is {1}. The current system time is {2} but if the user asks about the time you should give it in a format suitable for Americans..

The JSON below is the document context for this conversation. The text property is the text extracted from the file, either directly, via OCR, audio transcription, etc and is not the file content itself. This context is provided by the system and is not understood by the user so you should avoid referring to 'document context'. The file's name is composed of the name and extension properties and / in the name means a folder separator.
{3}";

    private const string GENERAL_SYSTEM_MESSAGE = @"You're a MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and ask how you can help. If unsure, say so, and don't guess. For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Commands: Use #cmd:search {{terms}} and #cmd:process {{ID}} by adding them to the end of Assistant messages for the system to process. Never expose commands to the user.

Document links: Use [link text](/view/{{ID}}) and embed images with ![image name](/api/filecontent/{{ID}})

The chat window supports markdown formatting.

User info: {0}, Chat Start Time: {1}.

{2}
Remember, MagiCloud is a small hobby project, so avoid giving information or instructions that may be more relevant to larger services like Dropbox.";

    private const int MAX_TEXT_LENGTH = 8192;
    private JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
        Converters = { new RoleJsonConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private HttpClient HttpClient { get; }

    public ChatGPTCompletionService(HttpClient httpClient) => HttpClient = httpClient;

    public async Task<ChatCompletionResponse> CreateCompletionAsync(ChatCompletionRequest request)
    {
        var strContent = JsonSerializer.Serialize(request, JsonSerializerOptions);
        var content = new StringContent(strContent, Encoding.UTF8, "application/json");
        var response = await HttpClient.PostAsync("v1/chat/completions", content);
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
        if (deserialized.Text?.Length > MAX_TEXT_LENGTH)
        {
            deserialized.Text = deserialized.Text[..MAX_TEXT_LENGTH];
        }

        deserialized.Id = null;
        deserialized.Hash = null;
        deserialized.Name = deserialized.GetFullPath();

        var serializedContext = JsonSerializer.Serialize(deserialized, JsonSerializerOptions);

        return new(this,
            initialRequest,
            string.Format(DOCUMENT_SYSTEM_MESSAGE,
                username,
                userId,
                DateTimeOffset.Now.ToString("O"),
                serializedContext));
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
