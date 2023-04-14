using MagiCommon.Models;
using MagiCommon.Models.AssistantChat;
using System.Threading.Tasks;

namespace MagiCommon.Interfaces
{
    public interface IChatCompletionService
    {
        Task<ChatCompletionResponse> CreateCompletionAsync(ChatCompletionRequest request);
        Chat CreateNewChat(ChatCompletionRequest initialRequest, string username, string userId, ElasticFileInfo fileContext);
    }
}