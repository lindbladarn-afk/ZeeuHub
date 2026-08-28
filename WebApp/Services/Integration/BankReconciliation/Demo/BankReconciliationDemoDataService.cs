using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Loads reusable bank reconciliation demo data and composes focused workflow scenarios.
public sealed class BankReconciliationDemoDataService : IBankReconciliationDemoDataService
{
    private static readonly IReadOnlyList<BankReconciliationDemoScenarioOption> ScenarioOptions =
    [
        new()
        {
            Key = "overview",
            Title = "Auto-match",
            Description = "Visar tydliga OCR- och beloppsmatchningar som ska gå direkt att auto-matcha."
        },
        new()
        {
            Key = "manual-review",
            Title = "Manuell granskning",
            Description = "Visar avsiktligt osäkra träffar där användaren ska granska och bekräfta manuellt."
        },
        new()
        {
            Key = "partial-payments",
            Title = "Delbetalningar",
            Description = "Visar två- och tredelade betalningsgrupper, avrundningstolerans och en samlingsbetalning."
        },
        new()
        {
            Key = "ai-camt-lab",
            Title = "AI-test",
            Description = "Kontrollerat CAMT-underlag med OCR-träffar, betalningsgrupper, osäkra kandidater och en rad utan legitim matchning."
        }
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IWebHostEnvironment _environment;

    public BankReconciliationDemoDataService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<BankReconciliationDemoData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(_environment.ContentRootPath, "Data", "Integration", "BankReconciliation", "demo");
        var transactionsPath = Path.Combine(root, "transactions.json");
        var invoicesPath = Path.Combine(root, "invoices.json");

        var result = new BankReconciliationDemoData
        {
            Transactions = await ReadAsync<List<BankReconciliationDemoTransaction>>(transactionsPath, cancellationToken) ?? new(),
            Invoices = await ReadAsync<List<BankReconciliationDemoInvoice>>(invoicesPath, cancellationToken) ?? new()
        };

        return result;
    }

    public async Task<BankReconciliationDemoScenario> LoadScenarioAsync(string? scenarioKey, CancellationToken cancellationToken = default)
    {
        var baseData = await LoadAsync(cancellationToken);
        var normalizedScenario = string.IsNullOrWhiteSpace(scenarioKey)
            ? "overview"
            : scenarioKey.Trim().ToLowerInvariant();

        return normalizedScenario switch
        {
            "manual-review" => BuildManualReviewScenario(baseData),
            "partial-payments" => BuildPartialPaymentsScenario(baseData),
            "ai-camt-lab" => BuildAiCamtLabScenario(),
            _ => BuildOverviewScenario(baseData)
        };
    }

    public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios() => ScenarioOptions;

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return default;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static BankReconciliationDemoScenario BuildOverviewScenario(BankReconciliationDemoData data)
    {
        var scenario = Clone(data);
        AddManualReviewRecommendations(scenario);

        return new BankReconciliationDemoScenario
        {
            Key = "overview",
            Title = "Auto-match",
            Description = "Visar tydliga OCR- och beloppsmatchningar som ska gå direkt att auto-matcha.",
            Data = scenario
        };
    }

    private static BankReconciliationDemoScenario BuildManualReviewScenario(BankReconciliationDemoData data)
    {
        var scenario = Clone(data);
        AddManualReviewRecommendations(scenario);
        ReplaceTransaction(scenario, "TX-001", transaction =>
        {
            transaction.Reference = "46216659";
            transaction.Remittance = "Kundbetalning december Birgitta Andersson";
            transaction.Amount = 9264.50m;
        });

        ReplaceTransaction(scenario, "TX-002", transaction =>
        {
            transaction.Reference = "46216259";
            transaction.Remittance = "Delvis OCR 46216259";
            transaction.Amount = 61124.50m;
        });

        ReplaceTransaction(scenario, "TX-009", transaction =>
        {
            transaction.Reference = string.Empty;
            transaction.Remittance = "Betalning december";
        });

        scenario.Transactions.Insert(0, new BankReconciliationDemoTransaction
        {
            Id = "TX-M001",
            Date = "2025-12-16",
            Amount = 1199m,
            Currency = "SEK",
            Reference = string.Empty,
            EndToEndId = "25121602000133940",
            DebtorName = "Markenberg Barbro",
            Remittance = "Decemberbetalning utan OCR"
        });

        return new BankReconciliationDemoScenario
        {
            Key = "manual-review",
            Title = "Manuell granskning",
            Description = "Visar avsiktligt osäkra träffar där användaren ska granska och bekräfta manuellt.",
            Data = scenario
        };
    }

    private static BankReconciliationDemoScenario BuildPartialPaymentsScenario(BankReconciliationDemoData data)
    {
        var scenario = Clone(data);
        scenario.Transactions.RemoveAll(x => string.Equals(x.Id, "TX-001", StringComparison.OrdinalIgnoreCase));
        scenario.Transactions.Insert(0, new BankReconciliationDemoTransaction
        {
            Id = "TX-P001",
            Date = "2025-12-15",
            Amount = 5000m,
            Currency = "SEK",
            Reference = "462166596",
            EndToEndId = "25121502000133901",
            DebtorName = "Birgitta Andersson",
            Remittance = "Delbetalning 1 OCR 462166596"
        });
        scenario.Transactions.Insert(1, new BankReconciliationDemoTransaction
        {
            Id = "TX-P002",
            Date = "2025-12-18",
            Amount = 4265m,
            Currency = "SEK",
            Reference = "462166596",
            EndToEndId = "25121802000133902",
            DebtorName = "Birgitta Andersson",
            Remittance = "Delbetalning 2 OCR 462166596"
        });
        scenario.Transactions.Add(new BankReconciliationDemoTransaction
        {
            Id = "TX-P003",
            Date = "2025-12-19",
            Amount = 1521m,
            Currency = "SEK",
            Reference = string.Empty,
            EndToEndId = "25121902000133903",
            DebtorName = "Samlingsinbetalning AB",
            Remittance = "Samlad betalning 1003 + 1005 + 1007"
        });
        AddPaymentBundleTransactions(scenario);

        return new BankReconciliationDemoScenario
        {
            Key = "partial-payments",
            Title = "Delbetalningar",
            Description = "Visar två- och tredelade betalningsgrupper, avrundningstolerans och en samlingsbetalning.",
            Data = scenario,
            SeedMatches =
            [
                new()
                {
                    AllocationId = "seed-partial-1",
                    TransactionId = "TX-P001",
                    InvoiceId = "INV-1001",
                    MatchType = "manual",
                    MatchRule = "seed-demo-partial",
                    MatchedAmount = 5000m,
                    Currency = "SEK",
                    CreatedByName = "ZeeU Demo"
                },
                new()
                {
                    AllocationId = "seed-bundle-1",
                    TransactionId = "TX-P003",
                    InvoiceId = "INV-1003",
                    MatchType = "manual",
                    MatchRule = "seed-demo-bundle",
                    MatchedAmount = 322m,
                    Currency = "SEK",
                    CreatedByName = "ZeeU Demo"
                }
            ]
        };
    }

    private static BankReconciliationDemoScenario BuildAiCamtLabScenario()
    {
        var scenario = new BankReconciliationDemoData
        {
            Transactions =
            [
                new()
                {
                    Id = "TX-AI001",
                    Date = "2026-04-27",
                    Amount = 11396.00m,
                    Currency = "SEK",
                    Reference = "873550016",
                    EndToEndId = "AI-20260427-001",
                    DebtorName = "Pelles Butik AB",
                    Remittance = "OCR 873550016"
                },
                new()
                {
                    Id = "TX-AI002",
                    Date = "2026-04-27",
                    Amount = 2465.43m,
                    Currency = "SEK",
                    Reference = string.Empty,
                    EndToEndId = "AI-20260427-002",
                    DebtorName = "Dagab Inköp & Logistik AB",
                    Remittance = "Betalning faktura 81002"
                },
                new()
                {
                    Id = "TX-AI003",
                    Date = "2026-04-27",
                    Amount = 986.58m,
                    Currency = "SEK",
                    Reference = "91",
                    EndToEndId = "AI-20260427-003",
                    DebtorName = "Dagab Inköp & Logistik AB",
                    Remittance = "Kort referens 91 avser faktura 81003"
                },
                new()
                {
                    Id = "TX-AI004",
                    Date = "2026-04-27",
                    Amount = 353.65m,
                    Currency = "SEK",
                    Reference = "614967628700",
                    EndToEndId = "AI-20260427-004",
                    DebtorName = "Dagab Inköp & Logistik AB",
                    Remittance = "OCR 614967628700"
                },
                new()
                {
                    Id = "TX-AI005",
                    Date = "2026-04-27",
                    Amount = 965.07m,
                    Currency = "SEK",
                    Reference = "83",
                    EndToEndId = "AI-20260427-005",
                    DebtorName = "Dagab Inköp & Logistik AB",
                    Remittance = "Referens 83, betalning för faktura 81005"
                },
                new()
                {
                    Id = "TX-AI006",
                    Date = "2026-04-27",
                    Amount = 2216.15m,
                    Currency = "SEK",
                    Reference = string.Empty,
                    EndToEndId = "AI-20260427-006",
                    DebtorName = "Nordic Servicebolaget AB",
                    Remittance = "Serviceavtal april"
                },
                new()
                {
                    Id = "TX-AI007",
                    Date = "2026-04-27",
                    Amount = 999.99m,
                    Currency = "SEK",
                    Reference = "NO-MATCH-001",
                    EndToEndId = "AI-20260427-007",
                    DebtorName = "Okänd Avsändare AB",
                    Remittance = "Testbetalning utan fakturaträff"
                },
                new()
                {
                    Id = "TX-AI008",
                    Date = "2026-04-28",
                    Amount = 3500.00m,
                    Currency = "SEK",
                    Reference = "992000110",
                    EndToEndId = "AI-20260428-008",
                    DebtorName = "Fjällstad Kontor AB",
                    Remittance = "Delbetalning 1 OCR 992000110"
                },
                new()
                {
                    Id = "TX-AI009",
                    Date = "2026-04-29",
                    Amount = 6500.00m,
                    Currency = "SEK",
                    Reference = "992000110",
                    EndToEndId = "AI-20260429-009",
                    DebtorName = "Fjällstad Kontor AB",
                    Remittance = "Delbetalning 2 OCR 992000110"
                },
                new()
                {
                    Id = "TX-AI010",
                    Date = "2026-04-28",
                    Amount = 1000.00m,
                    Currency = "SEK",
                    Reference = "992000129",
                    EndToEndId = "AI-20260428-010",
                    DebtorName = "Sundhamn Fastighet AB",
                    Remittance = "Delbetalning 1 av 3 OCR 992000129"
                },
                new()
                {
                    Id = "TX-AI011",
                    Date = "2026-04-29",
                    Amount = 2250.00m,
                    Currency = "SEK",
                    Reference = "992000129",
                    EndToEndId = "AI-20260429-011",
                    DebtorName = "Sundhamn Fastighet AB",
                    Remittance = "Delbetalning 2 av 3 OCR 992000129"
                },
                new()
                {
                    Id = "TX-AI012",
                    Date = "2026-04-30",
                    Amount = 4000.00m,
                    Currency = "SEK",
                    Reference = "992000129",
                    EndToEndId = "AI-20260430-012",
                    DebtorName = "Sundhamn Fastighet AB",
                    Remittance = "Delbetalning 3 av 3 OCR 992000129"
                },
                new()
                {
                    Id = "TX-AI013",
                    Date = "2026-04-29",
                    Amount = 2000.00m,
                    Currency = "SEK",
                    Reference = "992000137",
                    EndToEndId = "AI-20260429-013",
                    DebtorName = "Nordverk Service AB",
                    Remittance = "Delbetalning 1 OCR 992000137"
                },
                new()
                {
                    Id = "TX-AI014",
                    Date = "2026-04-30",
                    Amount = 2999.50m,
                    Currency = "SEK",
                    Reference = "992000137",
                    EndToEndId = "AI-20260430-014",
                    DebtorName = "Nordverk Service AB",
                    Remittance = "Slutbetalning OCR 992000137, avrundningsdifferens 0,50"
                }
            ],
            Invoices =
            [
                new()
                {
                    Id = "AI-INV-81001",
                    InvoiceNo = "81001",
                    Ocr = "873550016",
                    CustomerName = "Pelles Butik AB",
                    Amount = 11396.00m,
                    Currency = "SEK",
                    DueDate = "2026-05-07"
                },
                new()
                {
                    Id = "AI-INV-81002",
                    InvoiceNo = "81002",
                    Ocr = "87100024543",
                    CustomerName = "Dagab Inköp & Logistik AB",
                    Amount = 2465.43m,
                    Currency = "SEK",
                    DueDate = "2026-05-08"
                },
                new()
                {
                    Id = "AI-INV-81003",
                    InvoiceNo = "81003",
                    Ocr = "9181003",
                    CustomerName = "Dagab Inköp & Logistik AB",
                    Amount = 986.58m,
                    Currency = "SEK",
                    DueDate = "2026-05-09"
                },
                new()
                {
                    Id = "AI-INV-81004",
                    InvoiceNo = "81004",
                    Ocr = "614967628700",
                    CustomerName = "Dagab Inköp & Logistik AB",
                    Amount = 353.65m,
                    Currency = "SEK",
                    DueDate = "2026-05-10"
                },
                new()
                {
                    Id = "AI-INV-81005",
                    InvoiceNo = "81005",
                    Ocr = "8381005",
                    CustomerName = "Dagab Inköp & Logistik AB",
                    Amount = 965.07m,
                    Currency = "SEK",
                    DueDate = "2026-05-11"
                },
                new()
                {
                    Id = "AI-INV-81006",
                    InvoiceNo = "81006",
                    Ocr = string.Empty,
                    CustomerName = "Nordic Servicebolaget AB",
                    Amount = 2216.15m,
                    Currency = "SEK",
                    DueDate = "2026-05-12"
                },
                new()
                {
                    Id = "AI-INV-81007",
                    InvoiceNo = "81007",
                    Ocr = "614947472000",
                    CustomerName = "Dagab Inköp & Logistik AB",
                    Amount = 2.93m,
                    Currency = "SEK",
                    DueDate = "2026-05-13"
                },
                new()
                {
                    Id = "AI-INV-81008",
                    InvoiceNo = "81008",
                    Ocr = "614963250500",
                    CustomerName = "Dagab Inköp & Logistik AB",
                    Amount = 2184.62m,
                    Currency = "SEK",
                    DueDate = "2026-05-14"
                },
                new()
                {
                    Id = "AI-INV-82001",
                    InvoiceNo = "82001",
                    Ocr = "992000110",
                    CustomerName = "Fjällstad Kontor AB",
                    Amount = 10000.00m,
                    Currency = "SEK",
                    DueDate = "2026-05-15"
                },
                new()
                {
                    Id = "AI-INV-82002",
                    InvoiceNo = "82002",
                    Ocr = "992000129",
                    CustomerName = "Sundhamn Fastighet AB",
                    Amount = 7250.00m,
                    Currency = "SEK",
                    DueDate = "2026-05-16"
                },
                new()
                {
                    Id = "AI-INV-82003",
                    InvoiceNo = "82003",
                    Ocr = "992000137",
                    CustomerName = "Nordverk Service AB",
                    Amount = 5000.00m,
                    Currency = "SEK",
                    DueDate = "2026-05-17"
                }
            ]
        };

        return new BankReconciliationDemoScenario
        {
            Key = "ai-camt-lab",
            Title = "AI-test",
            Description = "Kontrollerat CAMT-underlag med OCR-träffar, betalningsgrupper, osäkra kandidater och en rad utan legitim matchning.",
            Data = scenario
        };
    }

    private static void ReplaceTransaction(BankReconciliationDemoData data, string id, Action<BankReconciliationDemoTransaction> update)
    {
        var tx = data.Transactions.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (tx is not null)
        {
            update(tx);
        }
    }

    private static void AddPaymentBundleTransactions(BankReconciliationDemoData data)
    {
        data.Transactions.AddRange(
        [
            PaymentBundleTransaction("TX-P1011-A", "2025-12-17", 3500m, "462200011", "Fjällstad Kontor AB", "Delbetalning 1 av 2"),
            PaymentBundleTransaction("TX-P1011-B", "2025-12-20", 6500m, "462200011", "Fjällstad Kontor AB", "Delbetalning 2 av 2"),
            PaymentBundleTransaction("TX-P1012-A", "2025-12-18", 1000m, "462200029", "Sundhamn Fastighet AB", "Delbetalning 1 av 3"),
            PaymentBundleTransaction("TX-P1012-B", "2025-12-21", 2250m, "462200029", "Sundhamn Fastighet AB", "Delbetalning 2 av 3"),
            PaymentBundleTransaction("TX-P1012-C", "2025-12-27", 4000m, "462200029", "Sundhamn Fastighet AB", "Delbetalning 3 av 3"),
            PaymentBundleTransaction("TX-P1013-A", "2025-12-19", 2000m, "462200037", "Nordverk Service AB", "Delbetalning 1 av 2"),
            PaymentBundleTransaction("TX-P1013-B", "2025-12-29", 2999.50m, "462200037", "Nordverk Service AB", "Slutbetalning med avrundningsdifferens 0,50")
        ]);
    }

    private static BankReconciliationDemoTransaction PaymentBundleTransaction(
        string id,
        string date,
        decimal amount,
        string ocr,
        string debtorName,
        string label)
        => new()
        {
            Id = id,
            Date = date,
            Amount = amount,
            Currency = "SEK",
            Reference = ocr,
            EndToEndId = $"DEMO-{id}",
            DebtorName = debtorName,
            Remittance = $"{label} OCR {ocr}"
        };

    private static void AddManualReviewRecommendations(BankReconciliationDemoData data)
    {
        ReplaceTransaction(data, "TX-010", transaction =>
        {
            transaction.Reference = string.Empty;
            transaction.Remittance = "Inbetalning december Susanne Gustafsson";
        });

        ReplaceTransaction(data, "TX-011", transaction =>
        {
            transaction.Reference = string.Empty;
            transaction.Remittance = "Fakturabetalning Kajsa Walter";
        });

        UpsertInvoice(data, new BankReconciliationDemoInvoice
        {
            Id = "1010-M",
            InvoiceNo = "1010-M",
            Ocr = string.Empty,
            CustomerName = "Susanne Gustafsson",
            Amount = 794.00m,
            Currency = "SEK",
            DueDate = "2025-12-29"
        });

        UpsertInvoice(data, new BankReconciliationDemoInvoice
        {
            Id = "1011-M",
            InvoiceNo = "1011-M",
            Ocr = string.Empty,
            CustomerName = "Kajsa Walter",
            Amount = 153633.00m,
            Currency = "SEK",
            DueDate = "2025-12-30"
        });
    }

    private static void UpsertInvoice(BankReconciliationDemoData data, BankReconciliationDemoInvoice invoice)
    {
        var existing = data.Invoices.FindIndex(x => string.Equals(x.Id, invoice.Id, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            data.Invoices[existing] = invoice;
            return;
        }

        data.Invoices.Add(invoice);
    }

    private static BankReconciliationDemoData Clone(BankReconciliationDemoData data)
    {
        return new BankReconciliationDemoData
        {
            Transactions = data.Transactions.Select(x => new BankReconciliationDemoTransaction
            {
                Id = x.Id,
                Date = x.Date,
                Amount = x.Amount,
                Currency = x.Currency,
                Reference = x.Reference,
                EndToEndId = x.EndToEndId,
                DebtorName = x.DebtorName,
                Remittance = x.Remittance
            }).ToList(),
            Invoices = data.Invoices.Select(x => new BankReconciliationDemoInvoice
            {
                Id = x.Id,
                InvoiceNo = x.InvoiceNo,
                Ocr = x.Ocr,
                CustomerName = x.CustomerName,
                Amount = x.Amount,
                Currency = x.Currency,
                DueDate = x.DueDate
            }).ToList()
        };
    }
}
