using System.Collections.Concurrent;
using System;
using MagiCommon.Models;

namespace MagiCloud.Services;

public class InMemoryMessageQueueService<T> : IMessageQueueService<T>
{
    private readonly ConcurrentQueue<Message<T>> _queue = new();

    public void AddMessage(T messageContent) => AddMessage(new Message<T>(messageContent));

    public void AddMessage(Message<T> message) => _queue.Enqueue(message);

    public Message<T> PopMessage()
    {
        while (_queue.TryPeek(out var message))
        {
            if (message.VisibilityTimeout.HasValue 
                && message.VisibilityTimeout.Value > DateTimeOffset.Now)
            {
                continue;
            }

            if (_queue.TryDequeue(out var dequeuedMessage))
            {
                dequeuedMessage.RetryCount++;
                return dequeuedMessage;
            }
        }

        return null;
    }

    public Message<T> PeekMessage()
    {
        while (_queue.TryPeek(out var message))
        {
            if (message.VisibilityTimeout.HasValue 
                && message.VisibilityTimeout.Value > DateTimeOffset.Now)
            {
                continue;
            }

            return message;
        }

        return null;
    }
}
