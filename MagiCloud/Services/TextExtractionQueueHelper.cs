using MagiCommon.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class TextExtractionQueueHelper
{
    public int DefaultMaxAttempts { get; set; } = 5;
    public TimeSpan PollingPeriod { get; set; } = TimeSpan.FromSeconds(30);

    private ILogger<TextExtractionQueueHelper> Logger { get; }
    private IMessageQueueService<string> TextExtractionQueue { get; }
    private ExtractionHelper ExtractionHelper { get; }
    private IElasticFileRepo ElasticManager { get; }

    public TextExtractionQueueHelper(ILogger<TextExtractionQueueHelper> logger,
        IMessageQueueService<string> queueService,
        ExtractionHelper extractionHelper,
        IElasticFileRepo elasticManager)
    {
        TextExtractionQueue = queueService;
        ExtractionHelper = extractionHelper;
        Logger = logger;
        ElasticManager = elasticManager;
    }

    public void AddFileToQueue(string fileId) => TextExtractionQueue.AddMessage(fileId);

    public async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // while there are messages, process them as fast as possible
                Message<string> message;
                while ((message = TextExtractionQueue.PopMessage()) != null)
                {
                    await ProcessMessage(message);
                }

                // Sleep between polls
                await Task.Delay(PollingPeriod, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Text Extraction Queue processing encountered an exception. Ignoring and continuing.");
            }
        }

        Logger.LogInformation("Shutting down Text Extraction Queue processing");
    }

    private async Task ProcessMessage(Message<string> message)
    {
        var fileId = message?.Content;
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return;
        }

        try
        {
            // Process the message by getting the required info from the ElasticManager
            // And then calling the ExtractionHelper to extract the text
            // Then update the file in Elastic
            Logger.LogInformation("Text Processing for file: {FileId}", fileId);
            var (_, text) = await ExtractionHelper.ExtractTextAsync(fileId, true);
            // Update the document with the new text
            if (!string.IsNullOrEmpty(text)) 
            {
                var fileInfo = await ElasticManager.GetDocumentByIdAsync(fileId, false);
                fileInfo.Text = text;
                await ElasticManager.UpdateFileAttributesAsync(fileInfo);
            }
            // If we got no text we don't bother updating the file
        }
        catch (Exception ex)
        {
            if (message.RetryCount >= DefaultMaxAttempts)
            {
                //throw new Exception($"Failed to process message after {DefaultMaxAttempts} attempts", ex);
                // Rather than throwing an exception, we just let the message go away
                Logger.LogError("Messaged reach max attempts of {MaxAttempts}: {FileId}", DefaultMaxAttempts, fileId);
            }

            // Log error and retry
            Logger.LogError(ex, "Error processing file {FileId}: {Message}", fileId, ex.Message);
           
            TextExtractionQueue.AddMessage(message);
        }
    }

}
