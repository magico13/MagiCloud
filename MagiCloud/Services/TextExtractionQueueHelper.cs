using MagiCloud.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MagiCloud.Services;

public class TextExtractionQueueHelper
{
    public int DefaultMaxAttempts { get; set; } = 5;
    public TimeSpan PollingPeriod { get; set; } = TimeSpan.FromSeconds(30);

    private ILogger<TextExtractionQueueHelper> Logger { get; }
    private IMessageQueueService<string> TextExtractionQueue { get; }


    public TextExtractionQueueHelper(ILogger<TextExtractionQueueHelper> logger,
        IMessageQueueService<string> queueService)
    {
        TextExtractionQueue = queueService;
        Logger = logger;
    }

    public void AddFileToQueue(string fileId) => TextExtractionQueue.AddMessage(fileId);

    public async Task ProcessQueueAsync()
    {
        while (true)
        {
            // while there are messages, process them as fast as possible
            var message = TextExtractionQueue.PopMessage();
            while (message != null)
            {
                await ProcessMessage(message);
                message = TextExtractionQueue.PopMessage();
            }
            
            // Sleep between polls
            await Task.Delay(PollingPeriod);
        }
    }

    public async Task ProcessMessage(Message<string> message)
    {
        if (message == null)
        {
            return;
        }

        var fileId = message.Content;

        try
        {
            // Process the message by getting the required info from the ElasticManager
            // And then calling the ExtractionHelper to extract the text
            // Then update the file in Elastic
            Logger.LogInformation("Text Processing for file: {FileId}", fileId);

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
