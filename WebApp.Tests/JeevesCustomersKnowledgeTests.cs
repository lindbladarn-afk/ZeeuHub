// Verifies that credit-limit prompts use the authoritative Jeeves customer-credit field.
using System;
using System.IO;

namespace WebApp.Tests;

public sealed class JeevesCustomersKnowledgeTests
{
    [Fact]
    public void CreditLimitKnowledge_UsesKundKredLimAndRejectsAktieKap()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WebApp", "AI", "Knowledge", "db", "jeeves", "jeeves-customers.md"));

        var text = File.ReadAllText(path);

        Assert.Contains("dbo.kus.kundkredlim", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not `dbo.fr.aktiekap`", text, StringComparison.OrdinalIgnoreCase);
    }
}
