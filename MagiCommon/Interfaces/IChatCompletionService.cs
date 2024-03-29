﻿using MagiCommon.Models;
using MagiCommon.Models.AssistantChat;
using System.Threading.Tasks;

namespace MagiCommon.Interfaces
{
    public interface IChatCompletionService
    {
        Task<ChatCompletionResponse> CreateCompletionAsync(ChatCompletionRequest request);
        Chat CreateNewGeneralChat(ChatCompletionRequest initialRequest, string username, string userId, string additionalContext, ElasticFileInfo fileContext = null);
    }
}