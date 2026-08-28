using System.Collections.Generic;
using WebApp.Services.Application;

namespace WebApp.Services.Application.AI;

public interface IAiConversationMemory
{
    List<OpenAiChatMessage> GetHistory(string key);
    void AppendTurn(string key, string userMessage, string assistantMessage);
    AiConversationResultContext? GetLastResultContext(string key);
    void SetLastResultContext(string key, AiConversationResultContext resultContext);
    void Clear(string key);
}
