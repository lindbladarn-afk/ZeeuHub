using System;
using System.IO;
using Xunit;

namespace WebApp.Tests;

public class JeevesInvoicesKnowledgeTests
{
    [Fact]
    public void JeevesInvoicesKnowledge_DefinesOpenInvoicesAsUnpaidInvoices()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WebApp", "AI", "Knowledge", "db", "jeeves", "jeeves-invoices.md"));

        var text = File.ReadAllText(path);

        Assert.Contains("Open invoice", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unpaid invoice", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AttBetalaBelopp > 0", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AttBetalaBelopp <= 0", text, StringComparison.OrdinalIgnoreCase);
    }
}
