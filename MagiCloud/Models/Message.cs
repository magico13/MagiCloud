using System;

namespace MagiCloud.Models;

public class Message<T>
{
    public Message() { }
    public Message(T content) => Content = content;

    public T Content { get; set; }
    public DateTimeOffset QueuedTime { get; set; } = DateTimeOffset.Now;
    public int RetryCount { get; set; }
    public DateTimeOffset? ExpirationTime { get; set; }
    public DateTimeOffset? VisibilityTimeout { get; set; }
}