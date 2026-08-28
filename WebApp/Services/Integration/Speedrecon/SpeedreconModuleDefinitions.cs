using WebApp.Models.Integration.Speedrecon;

namespace WebApp.Services.Integration.Speedrecon;

// Keeps the original Speedrecon procedure order and result descriptions in one place.
public static class SpeedreconModuleDefinitions
{
    public static SpeedreconModuleDefinition Kundreskontra { get; } = new()
    {
        Key = "kundresk",
        DisplayName = "Kundreskontra",
        IsEnabled = plan => plan.Kundreskontra,
        ResultDescriptions = ["KUNDRESK"]
    };

    public static SpeedreconModuleDefinition Leverantorsreskontra { get; } = new()
    {
        Key = "levresk",
        DisplayName = "Leverantorsreskontra",
        IsEnabled = plan => plan.Leverantorsreskontra,
        ResultDescriptions = ["LEVRESK"]
    };

    public static SpeedreconModuleDefinition Anlaggning { get; } = new()
    {
        Key = "anlaggning",
        DisplayName = "Anlaggning",
        IsEnabled = plan => plan.Anlaggning,
        ResultDescriptions = ["ANLAG", "AVSKR"]
    };

    public static SpeedreconModuleDefinition InlevereratEjFakturerat { get; } = new()
    {
        Key = "inlevejfakt",
        DisplayName = "Inlevererat ej fakturerat",
        IsEnabled = plan => plan.InlevereratEjFakturerat,
        ResultDescriptions = ["INLEVEJFAKT"]
    };

    public static SpeedreconModuleDefinition Lego { get; } = new()
    {
        Key = "lego",
        DisplayName = "Lego",
        IsEnabled = plan => plan.InlevereratEjFakturerat,
        ResultDescriptions = ["LEGO"]
    };

    public static SpeedreconModuleDefinition InternLeverantorsreskontra { get; } = new()
    {
        Key = "intlevresk",
        DisplayName = "Intern leverantorsreskontra",
        IsEnabled = plan => plan.InternLeverantorsreskontra,
        ResultDescriptions = ["INTLEVRESK"]
    };

    public static SpeedreconModuleDefinition Lagervarde { get; } = new()
    {
        Key = "lagervarde",
        DisplayName = "Lagervarde",
        IsEnabled = plan => plan.Lagervarde,
        ResultDescriptions = ["LAGVARDE"]
    };

    public static SpeedreconModuleDefinition Lagerflytt { get; } = new()
    {
        Key = "lagerflytt",
        DisplayName = "Lagerflytt",
        IsEnabled = plan => plan.Lagerflytt,
        ResultDescriptions = ["LAGFLYTT"]
    };

    public static SpeedreconModuleDefinition Orderunik { get; } = new()
    {
        Key = "orderunik",
        DisplayName = "Orderunik",
        IsEnabled = plan => plan.Orderunik,
        ResultDescriptions = ["ORDERUNIK"]
    };

    public static SpeedreconModuleDefinition Periodisering { get; } = new()
    {
        Key = "periodisering",
        DisplayName = "Periodisering",
        IsEnabled = plan => plan.Periodisering,
        ResultDescriptions = ["PERIOD"]
    };

    public static SpeedreconModuleDefinition Pia { get; } = new()
    {
        Key = "pia",
        DisplayName = "PIA",
        IsEnabled = plan => plan.Pia,
        ResultDescriptions = ["PIA"]
    };

    public static SpeedreconModuleDefinition UtlevereratEjFakturerat { get; } = new()
    {
        Key = "utlevejfakt",
        DisplayName = "Utlevererat ej fakturerat",
        IsEnabled = plan => plan.UtlevereratEjFakturerat,
        ResultDescriptions = ["UTLEVEJFAKT"]
    };

    public static IReadOnlyList<SpeedreconModuleDefinition> All { get; } =
    [
        Kundreskontra,
        Leverantorsreskontra,
        Anlaggning,
        InlevereratEjFakturerat,
        Lego,
        InternLeverantorsreskontra,
        Lagervarde,
        Lagerflytt,
        Orderunik,
        Periodisering,
        Pia,
        UtlevereratEjFakturerat
    ];
}
