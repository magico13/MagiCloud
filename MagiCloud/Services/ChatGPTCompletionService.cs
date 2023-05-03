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
    private const string DOCUMENT_SYSTEM_MESSAGE = @"You're a MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and a guess at what the document is about (eg This looks to be a pdf of a form 1040 tax document). For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Assistant Actions: As the MagiCloud Assistant, you can perform system actions with your messages and the system will reply with the results. You are not System, wait for System responses to be provided separately, do not write messages with 'System:'. Use the following actions at the end of your messages as you wish:
[sys:time] to access the current date and time. Example chat for reference: 
    ""User: What time is it? 
    Assistant: Let me fetch the current time. [sys:time]
    System: The current time is 03/25/2023 5:45 PM -4 
    Assistant: The time is 03/25/2023 5:45 PM EDT.""
[sys:text {4}] to retrieve the text of any document, including images and audio files, using OCR. Once you've done this once you do not need to do it again. Example chat for reference: 
    ""User: Tell me more about this file. 
    Assistant: Sure! Let me get the text MagiCloud extracted previously to understand it better. [sys:text {4}] 
    System: First 13 chars of text of the document: USA FORM-1040 
    Assistant: Based on the retrieved text this is a form 1040 tax document.""
[sys:process {4}] to reprocess the document when asked to by a user or when a user says the document text is incorrect.

Document links: Use [link text](/view/{4}) and embed images with ![image name](/api/filecontent/{4})

The chat window supports markdown formatting.

Chatting with user {0} with user ID {1}, Chat Start Time: {2}.

This chat is in the context of a single document with ID {4}. The details of the document being discussed in this chat are:
{3}";

    private const string GENERAL_SYSTEM_MESSAGE = @"You're a MagiCloud assistant, a personal cloud storage website created as a one-person hobby project. Begin with a friendly hello and ask how you can help. If unsure, say so, and don't guess. For the user, format datetimes as MM/DD/YYYY, h:mm AM/PM.

Assistant Actions: As the MagiCloud Assistant, you can perform system actions with your messages and the system will reply with the results. You are not System, wait for System responses to be provided separately, do not write messages with 'System:'. Use the following actions at the end of your messages as you wish:
[sys:time] to access the current date and time. Example chat for reference: 
    ""User: What time is it? 
    Assistant: Let me fetch the current time [sys:time]. 
    System: The current time is 03/25/2023 5:45 PM -4 
    Assistant: The time is 03/25/2023 5:45 PM EDT.""
[sys:search {{terms}}] to search for documents in the user's account. Example chat for reference: 
    ""User: Hi, I’m looking for some documents about nuclear fusion. 
    Assistant: Sure, let me search your documents for nuclear fusion. [sys:search nuclear fusion] 
    System: Top search results for 'nuclear fusion': Fusion101.pdf, FusionReport.docx, FusionExperiment.pptx 
    Assistant: Here are the top documents that match your query:""
[sys:process {{ID}}] to reprocess a document when asked to by a user or when a user says the document text is incorrect.

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
