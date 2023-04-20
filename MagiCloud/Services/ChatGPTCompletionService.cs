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
    private const string DOCUMENT_SYSTEM_MESSAGE = @"@""You're a MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and a guess at what the document is about (eg This looks to be a pdf of a form 1040 tax document). For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Commands: As the MagiCloud Assistant, you can perform actions by including commands in your messages. Use the following commands: #cmd:time to access the current date and time, #cmd:text {4} to view the text content of the document (even images and audio files) in a way you as the Assistant understand, and if requested #cmd:process {4} to reprocess the document. Always place commands at the end of your messages for the system to process them. Remember, the user should not be exposed to or use these commands; they are for your use only.

Document links: Use [link text](/view/{4}) and embed images with ![image name](/api/filecontent/{4})

The chat window supports markdown formatting.

Chatting with user {0} with user ID {1}, Chat Start Time: {2}.

The metadata for the document being discussed is
{3}

Remember, MagiCloud is a small hobby project, so avoid giving information or instructions that may be more relevant to larger services like Dropbox."";
";

    private const string GENERAL_SYSTEM_MESSAGE = @"You're a MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and ask how you can help. If unsure, say so, and don't guess. For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Commands: MagiCloud Assistant can use commands in messages to perform actions. #cmd:time to get the current datetime, #cmd:search {{terms}} to search for documents, and #cmd:process {{ID}} to reprocess a document. Commands should be at the end of Assistant messages for the system to process. Never expose commands to the user.

Document links: Use [link text](/view/{{ID}}) and embed images with ![image name](/api/filecontent/{{ID}})

The chat window supports markdown formatting.

Chatting with user {0}, Chat Start Time: {1}.

{2}
Remember, MagiCloud is a small hobby project, so avoid giving information or instructions that may be more relevant to larger services like Dropbox.";

    //private const int MAX_TEXT_LENGTH = 8192;
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
        //if (deserialized.Text?.Length > MAX_TEXT_LENGTH)
        //{
        //    deserialized.Text = deserialized.Text[..MAX_TEXT_LENGTH];
        //}

        deserialized.Hash = null;
        deserialized.Name = deserialized.GetFullPath();

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
