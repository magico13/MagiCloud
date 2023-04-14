using MagiCommon.Models;

namespace MagiCloud.Services
{
    public interface IMessageQueueService<T>
    {
        void AddMessage(T message);
        void AddMessage(Message<T> message);
        Message<T> PopMessage();
        Message<T> PeekMessage();
    }
}