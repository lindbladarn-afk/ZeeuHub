// Verifies that follow-up result context stays isolated to the active AI conversation.
using Microsoft.Extensions.Caching.Memory;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiConversationMemoryTests
{
    [Fact]
    public void LastResultContext_ReturnsCopy_AndIsRemovedWithConversation()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var memory = new AiConversationMemory(cache);
        const string conversationKey = "ai:db:user:c-none:tenant";
        memory.SetLastResultContext(conversationKey, new AiConversationResultContext
        {
            Intent = "ranking",
            Metric = "net_revenue",
            Period = "current_year",
            Columns = ["ProductID", "Revenue"],
            Rows = [["101001", "100100"]],
            TotalRowCount = 1
        });

        var firstRead = Assert.IsType<AiConversationResultContext>(memory.GetLastResultContext(conversationKey));
        firstRead.Columns[0] = "Changed";
        firstRead.Rows[0][0] = "Changed";

        var secondRead = Assert.IsType<AiConversationResultContext>(memory.GetLastResultContext(conversationKey));
        Assert.Equal("ProductID", secondRead.Columns[0]);
        Assert.Equal("101001", secondRead.Rows[0][0]);
        Assert.Equal("current_year", secondRead.Period);

        memory.Clear(conversationKey);

        Assert.Null(memory.GetLastResultContext(conversationKey));
    }
}
