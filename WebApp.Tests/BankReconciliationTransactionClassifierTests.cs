using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

// Classifier tests keep the new bank reconciliation types stable before the UI starts using them.
public sealed class BankReconciliationTransactionClassifierTests
{
    [Fact]
    public void Classify_CustomerInpayment_ReturnsBankinbetalningar()
    {
        var classification = BankReconciliationTransactionClassifier.Classify(
            "PMNT",
            "RCDT",
            "NTAV",
            "CRDT",
            "SCOR",
            "*INSÄTTNING 0260507015667",
            "Willab Garden Kund");

        Assert.Equal("bankinbetalningar", classification.TypeKey);
        Assert.Equal("Bankinbetalningar", classification.TypeLabel);
        Assert.Equal("Kundinbetalningar", classification.LegacyGroup);
        Assert.Equal("1510", classification.SuggestedAccount);
        Assert.Null(classification.SuggestedCostCenter);
        Assert.False(classification.IsDefault);
    }

    [Fact]
    public void Classify_InternalTransfer_ReturnsOverforingKonto()
    {
        var classification = BankReconciliationTransactionClassifier.Classify(
            "TRAD",
            "NTAV",
            "NTAV",
            "DBIT",
            null,
            "0260209006385 260209006385",
            null);

        Assert.Equal("overforing-konto", classification.TypeKey);
        Assert.Equal("Överföring konto", classification.TypeLabel);
        Assert.Equal("Ovrigt", classification.LegacyGroup);
        Assert.Equal("trad+ntav", classification.LegacyRule);
        Assert.Equal("1930", classification.SuggestedAccount);
    }

    [Fact]
    public void Classify_RantaText_ReturnsRantekonto()
    {
        var classification = BankReconciliationTransactionClassifier.Classify(
            "PMNT",
            "ICDT",
            "DMCT",
            "DBIT",
            null,
            "Ränta på konto",
            null);

        Assert.Equal("rantekonto", classification.TypeKey);
        Assert.Equal("Räntekonto", classification.TypeLabel);
        Assert.Equal("Ovrigt", classification.LegacyGroup);
        Assert.Equal("8310", classification.SuggestedAccount);
    }

    [Fact]
    public void Classify_Fallback_ReturnsDef()
    {
        var classification = BankReconciliationTransactionClassifier.Classify(
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Equal("def", classification.TypeKey);
        Assert.Equal("DEF", classification.TypeLabel);
        Assert.True(classification.IsDefault);
        Assert.Equal("Ovrigt", classification.LegacyGroup);
        Assert.Null(classification.SuggestedAccount);
    }
}
