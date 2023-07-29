using MagiCommon.Models;

namespace MagiCloud.Services;

/// <summary>
/// Provides a Singleton wrapper for the text extraction queue.
/// </summary>
public class TextExtractionQueueWrapper
{
    private IMessageQueueService<string> TextExtractionQueue { get; }

    public TextExtractionQueueWrapper(IMessageQueueService<string> queueService)
    {
        TextExtractionQueue = queueService;
    }

    public void AddFileToQueue(string fileId) => TextExtractionQueue.AddMessage(fileId);
    public Message<string> PopMessage() => TextExtractionQueue.PopMessage();
    internal void AddMessage(Message<string> message) => TextExtractionQueue.AddMessage(message);
}
