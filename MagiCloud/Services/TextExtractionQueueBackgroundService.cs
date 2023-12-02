using MagiCommon.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MagiCloud.Services;

/// <summary>
/// The service that actually processes the text extraction queue.
/// </summary>
public class TextExtractionQueueBackgroundService(
    ILogger<TextExtractionQueueBackgroundService> logger,
    TextExtractionQueueWrapper extractionQueueWrapper,
    ExtractionHelper extractionHelper,
    IElasticFileRepo elasticManager) : BackgroundService
{
    public int DefaultMaxAttempts { get; set; } = 5;
    public TimeSpan PollingPeriod { get; set; } = TimeSpan.FromSeconds(30);

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
            logger.LogInformation("Text Processing for file: {FileId}", fileId);
            var (_, text) = await extractionHelper.ExtractTextAsync(fileId, true);
            // Update the document with the new text
            if (!string.IsNullOrEmpty(text))
            {
                var fileInfo = await elasticManager.GetDocumentByIdAsync(fileId, false);
                fileInfo.Text = text;
                await elasticManager.UpdateFileAttributesAsync(fileInfo);
            }
            // If we got no text we don't bother updating the file
        }
        catch (Exception ex)
        {
            if (message.RetryCount >= DefaultMaxAttempts)
            {
                //throw new Exception($"Failed to process message after {DefaultMaxAttempts} attempts", ex);
                // Rather than throwing an exception, we just let the message go away
                logger.LogError("Messaged reach max attempts of {MaxAttempts}: {FileId}", DefaultMaxAttempts, fileId);
            }

            // Log error and retry
            logger.LogError(ex, "Error processing file {FileId}: {Message}", fileId, ex.Message);

            extractionQueueWrapper.AddMessage(message);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // while there are messages, process them as fast as possible
                Message<string> message;
                while ((message = extractionQueueWrapper.PopMessage()) != null
                    && !stoppingToken.IsCancellationRequested)
                {
                    await ProcessMessage(message);
                    // TODO: If cancellation is requested while ProcessMessage is running,
                    // it may not stop within the allowed time. Extract Text should allow cancellation.
                }

                // Sleep between polls
                await Task.Delay(PollingPeriod, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Text Extraction Queue processing encountered an exception. Ignoring and continuing.");
            }
        }

        logger.LogInformation("Shutting down Text Extraction Queue processing");
    }
}
