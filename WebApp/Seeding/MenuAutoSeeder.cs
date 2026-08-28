// Seeds the stable portal module catalog and keeps company permission rows in sync.
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.Services.Application;

namespace WebApp.Seeding;

/// <summary>
/// Seeds modules and submodules if they are missing and normalizes legacy menu rows when older seed data is still present.
/// </summary>
public static class MenuAutoSeeder
{
    // Fixed IDs so we don't create duplicates across runs
    // Legacy module IDs kept for reference (not used)
    private static readonly Guid InvoicesModuleId = Guid.Parse("d661cf3e-5c8f-4e7e-af98-8f023db1d8b4");
    private static readonly Guid OrdersModuleId = Guid.Parse("7f3c9b55-2d78-4cb9-9c2c-3e8f4b6d9131");
    private static readonly Guid ExcelModuleId = Guid.Parse("e6b4d0d9-3af9-4a61-9c78-1b0b45f2f0ea");
    private static readonly Guid ExcelSubModuleId = Guid.Parse("a2dfeb49-7a52-4e0b-9c8c-0b7a3a3fa41f");
    private static readonly Guid IntelligenceModuleId = Guid.Parse("4e91c0c2-bc0c-4f5b-bc2b-a728e1c621fd");
    private static readonly Guid IntelligenceSubModuleId = Guid.Parse("c1a47e0e-8f4f-4db0-8d53-72b0fb4d8a6c");
    private static readonly Guid ActionCenterModuleId = Guid.Parse("f7f9c6a1-2d7f-4d9f-a6e4-4b5b5c9f5f6a");
    private static readonly Guid ActionCenterSubModuleId = Guid.Parse("e8a5b2c3-d4e5-4f6a-8b9c-0d1e2f3a4b5c");
    private static readonly Guid WebApprovalAttestChainsSubModuleId = Guid.Parse("6e68a2f0-5b77-4b7a-8f93-4bc2d9df80a1");
    private static readonly string[] LegacyApprovalSubModuleKeys = new[]
    {
        "SubModule_WebApproval_OrderApproval",
        "SubModule_WebApproval_PurchaseApproval",
        "SubModule_WebApproval_PriceListApproval"
    };
    private static readonly Guid DocumentSigningModuleId = Guid.Parse("9c0cc0d5-7eb4-4bc3-a643-8ad6505f7f71");
    private static readonly Guid IntegrationModuleId = Guid.Parse("9a7ef2b7-0f2b-4f1f-8dbb-61b8a53d4f3e");
    private static readonly Guid SpeedreconModuleId = Guid.Parse("8d50a65a-07bf-4a7b-8790-b4ff2345d0a2");
    private static readonly Guid SpeedreconSubModuleId = Guid.Parse("adbc1e55-6f13-4f6b-968d-5b2f7d73b441");
    private static readonly Guid CustomerSyncSubModuleId = Guid.Parse("0f5c9db5-5b7b-4a2f-9d51-3e2c9a1b8a44");
    private static readonly Guid OrderMatchingSubModuleId = Guid.Parse("e6f41a9a-3b0a-4f7b-9d1a-1c6e2f3c9b7a");
    private static readonly Guid AkeneoExportSubModuleId = Guid.Parse("5f75d5c0-8e1e-4b6a-a24f-0b1a7f5b7c2d");
    private static readonly Guid OngoingSubModuleId = Guid.Parse("7b5f1d3f-7f1a-4f93-9f4f-8d0f4a3c2b10");
    private static readonly Guid FlowEngineSubModuleId = Guid.Parse("d9ad45b1-5cf3-4c12-a260-33f7733918d4");
    private static readonly Guid FlowEngineJeevesSubModuleId = Guid.Parse("ab848301-87fd-4200-a48f-2ad2f4d826fb");
    private static readonly Guid FlowEngineShopifySubModuleId = Guid.Parse("3395aa22-0ac8-4a30-bcea-4314e32f56ff");
    private static readonly Guid FlowEngineJobsSubModuleId = Guid.Parse("1bc60290-97f2-40bb-a1df-3c8722698aea");
    private static readonly Guid FlowEngineConfigSubModuleId = Guid.Parse("1e7fef0d-402f-4d6f-aa73-e342fa0dd09f");

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Make sure the database exists and has the portal schema before seeding.
        try
        {
            await PortalDatabaseInitializer.InitializeAsync(context);
        }
        catch (SqlException ex) when (ex.Number == 1801 || ex.Number == 2714) // DB exists or object exists
        {
            // Schema already present; continue with seeding
        }

        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("BankReconciliationLegacyDataMigrator");
        await BankReconciliationLegacyDataMigrator.MigrateAsync(context, environment, logger);

        await EnsureInvoicesModuleAsync(context);
        await EnsureInvoicesSubModuleAsync(context);
        await EnsureOrdersModuleAsync(context);
        await EnsureOrdersSubModuleAsync(context);
        await EnsureExcelModuleAsync(context);
        await EnsureExcelSubModuleAsync(context);
        await EnsureIntelligenceModuleAsync(context);
        await EnsureIntelligenceSubModuleAsync(context);
        await EnsureActionCenterModuleAsync(context);
        await EnsureActionCenterSubModuleAsync(context);
        var approvalModuleId = await EnsureApprovalModuleAsync(context);
        await EnsureApprovalAttestChainsSubModuleAsync(context, approvalModuleId);
        await EnsureDocumentSigningModuleAsync(context);
        await EnsureBankReconciliationModuleAsync(context);
        await EnsureSpeedreconModuleAsync(context);
        await EnsureSpeedreconSubModuleAsync(context);
        await EnsureIntegrationModuleAsync(context);
        await EnsureOrderMatchingSubModuleAsync(context);
        await EnsureAkeneoExportSubModuleAsync(context);
        await EnsureOngoingSubModuleAsync(context);
        await HideBankReconciliationIntegrationSubModuleAsync(context);
        await EnsureCustomerSyncSubModuleAsync(context);
        await HideCustomerSyncModuleAsync(context);
        await EnsureDocumentSigningSubModuleAsync(context);
        await EnsureFlowEngineSubModuleAsync(context);
        await EnsureFlowEngineJeevesSubModuleAsync(context);
        await EnsureFlowEngineShopifySubModuleAsync(context);
        await EnsureFlowEngineJobsSubModuleAsync(context);
        await EnsureFlowEngineConfigSubModuleAsync(context);
        await EnsureNotifyMeEditorRouteAsync(context);
        await EnsureCompanyPermissionsAsync(context, IntegrationModuleId, CustomerSyncSubModuleId);
        await EnsureCompanyPermissionsAsync(context, PortalModuleIds.BankReconciliationModule);
        await EnsureCompanyPermissionsAsync(context, SpeedreconModuleId, SpeedreconSubModuleId);
        await EnsureCompanyPermissionsAsync(context, DocumentSigningModuleId, PortalModuleIds.DocumentSigningSubModule);
        await EnsureCompanyPermissionsAsync(context, approvalModuleId);
        await EnsureCompanyPermissionsAsync(context, approvalModuleId, WebApprovalAttestChainsSubModuleId);
        await EnsureFlowEngineSectionPermissionsFromDashboardAsync(context, OrderMatchingSubModuleId);
        await EnsureFlowEngineSectionPermissionsFromDashboardAsync(context, AkeneoExportSubModuleId);
        await EnsureFlowEngineSectionPermissionsFromDashboardAsync(context, FlowEngineJeevesSubModuleId);
        await EnsureFlowEngineSectionPermissionsFromDashboardAsync(context, FlowEngineShopifySubModuleId);
        await EnsureFlowEngineSectionPermissionsFromDashboardAsync(context, FlowEngineJobsSubModuleId);
        await EnsureFlowEngineSectionPermissionsFromDashboardAsync(context, FlowEngineConfigSubModuleId);

        await context.SaveChangesAsync();
    }

    private static async Task EnsureNotifyMeEditorRouteAsync(ApplicationDbContext context)
    {
        var notifyMeSubModules = await context.SubModules!
            .Where(sm => sm.MenuItemController == "NotifyMe"
                         && (sm.MenuItemAction == "CreateNew"
                             || sm.MenuItemText == "SubModule_NotifyMe_CreateNew"))
            .ToListAsync();

        foreach (var subModule in notifyMeSubModules)
        {
            var needsUpdate = false;

            if (!string.Equals(subModule.MenuItemAction, "Editor", StringComparison.OrdinalIgnoreCase))
            {
                subModule.MenuItemAction = "Editor";
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                context.SubModules!.Update(subModule);
            }
        }
    }

    private static async Task EnsureInvoicesModuleAsync(ApplicationDbContext context)
    {
        var exists = await context.Modules!.AnyAsync(m => m.Id == InvoicesModuleId || m.Name == "Invoices New");
        if (exists) return;

        var module = new ApplicationModule
        {
            Id = InvoicesModuleId,
            Name = "Invoices New",
            Description = "Se och hantera fakturor (nya komponenten)",
            MenuSectionController = "Invoices",
            MenuSectionAction = "Index",
            MenuSectionIcon = "fas fa-file-invoice",
            MenuSectionText = "Fakturor New",
            // Hidden from sidebar; used only for permissions mapping
            MenuSectionEnabled = false,
            MenuSectionSortOrder = 51
        };

        await context.Modules!.AddAsync(module);
    }

    private static async Task EnsureInvoicesSubModuleAsync(ApplicationDbContext context)
    {
        var exists = await context.SubModules!.AnyAsync(sm => sm.Id == PortalModuleIds.InvoicesSubModule || sm.Name == "Fakturor New");
        if (exists) return;

        var sub = new ApplicationSubModule
        {
            Id = PortalModuleIds.InvoicesSubModule,
            ModuleId = InvoicesModuleId,
            Name = "Fakturor New",
            Description = "Lista/hantera fakturor (nya komponenten)",
            MenuItemController = "Invoices",
            MenuItemAction = "Index",
            MenuItemText = "SubModule_Invoices_New",
            MenuItemEnabled = true,
            MenuItemSortOrder = 1
        };

        await context.SubModules!.AddAsync(sub);
    }

    private static async Task EnsureOrdersModuleAsync(ApplicationDbContext context)
    {
        var exists = await context.Modules!.AnyAsync(m => m.Id == OrdersModuleId || m.Name == "Orders New");
        if (exists) return;

        var module = new ApplicationModule
        {
            Id = OrdersModuleId,
            Name = "Orders New",
            Description = "Se och hantera ordrar (nya komponenten)",
            MenuSectionController = "Orders",
            MenuSectionAction = "Index",
            MenuSectionIcon = "fa fa-list-alt",
            MenuSectionText = "Orders New",
            MenuSectionEnabled = false,
            MenuSectionSortOrder = 52
        };

        await context.Modules!.AddAsync(module);
    }

    private static async Task EnsureOrdersSubModuleAsync(ApplicationDbContext context)
    {
        var exists = await context.SubModules!.AnyAsync(sm => sm.Id == PortalModuleIds.OrdersSubModule || sm.Name == "Orders New");
        if (exists) return;

        var sub = new ApplicationSubModule
        {
            Id = PortalModuleIds.OrdersSubModule,
            ModuleId = OrdersModuleId,
            Name = "Orders New",
            Description = "Lista/hantera ordrar (nya komponenten)",
            MenuItemController = "Orders",
            MenuItemAction = "Index",
            MenuItemText = "SubModule_Orders_New",
            MenuItemEnabled = true,
            MenuItemSortOrder = 1
        };

        await context.SubModules!.AddAsync(sub);
    }

    private static async Task EnsureExcelModuleAsync(ApplicationDbContext context)
    {
        var exists = await context.Modules!.AnyAsync(m => m.Id == ExcelModuleId || m.Name == "Excel Import New");
        if (exists) return;

        var module = new ApplicationModule
        {
            Id = ExcelModuleId,
            Name = "Excel Import New",
            Description = "Importera data via Excel (nya komponenten)",
            MenuSectionController = "ExcelImport",
            MenuSectionAction = "Index",
            MenuSectionIcon = "fa fa-file-excel",
            MenuSectionText = "Excel Import New",
            MenuSectionEnabled = false,
            MenuSectionSortOrder = 53
        };

        await context.Modules!.AddAsync(module);
    }

    private static async Task EnsureExcelSubModuleAsync(ApplicationDbContext context)
    {
        var exists = await context.SubModules!.AnyAsync(sm => sm.Id == ExcelSubModuleId || sm.Name == "Excel Import New");
        if (exists) return;

        var sub = new ApplicationSubModule
        {
            Id = ExcelSubModuleId,
            ModuleId = ExcelModuleId,
            Name = "Excel Import New",
            Description = "Importera via Excel (nya komponenten)",
            MenuItemController = "ExcelImport",
            MenuItemAction = "Index",
            MenuItemText = "SubModule_ExcelImport_New",
            MenuItemEnabled = true,
            MenuItemSortOrder = 1
        };

        await context.SubModules!.AddAsync(sub);
    }

    private static async Task EnsureIntelligenceModuleAsync(ApplicationDbContext context)
    {
        var module = await context.Modules!
            .FirstOrDefaultAsync(m => m.Id == IntelligenceModuleId || m.Name == "ZeeU Intelligence New");

        if (module is null)
        {
            module = new ApplicationModule
            {
                Id = IntelligenceModuleId,
                Name = "ZeeU Intelligence New",
                Description = "ZeeU Intelligence (nya komponenten)",
                MenuSectionController = "AI",
                MenuSectionAction = "Intelligence",
                MenuSectionIcon = "fa fa-magic",
                MenuSectionText = "ZeeU Intelligence New",
                MenuSectionEnabled = false,
                MenuSectionSortOrder = 54
            };

            await context.Modules!.AddAsync(module);
            return;
        }

        // Normalize existing row if it was created with old routes/icons
        bool needsUpdate = false;
        if (!string.Equals(module.MenuSectionController, "AI", StringComparison.OrdinalIgnoreCase))
        {
            module.MenuSectionController = "AI";
            needsUpdate = true;
        }
        if (!string.Equals(module.MenuSectionAction, "Intelligence", StringComparison.OrdinalIgnoreCase))
        {
            module.MenuSectionAction = "Intelligence";
            needsUpdate = true;
        }
        if (!string.Equals(module.MenuSectionIcon, "fa fa-magic", StringComparison.OrdinalIgnoreCase))
        {
            module.MenuSectionIcon = "fa fa-magic";
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            context.Modules!.Update(module);
        }
    }

    private static async Task EnsureIntelligenceSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!
            .FirstOrDefaultAsync(sm => sm.Id == IntelligenceSubModuleId || sm.Name == "ZeeU Intelligence New");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = IntelligenceSubModuleId,
                ModuleId = IntelligenceModuleId,
                Name = "ZeeU Intelligence New",
                Description = "ZeeU Intelligence (nya komponenten)",
                MenuItemController = "AI",
                MenuItemAction = "Intelligence",
                MenuItemText = "SubModule_ZeeUIntelligence_New",
                MenuItemEnabled = true,
                MenuItemSortOrder = 1
            };

            await context.SubModules!.AddAsync(sub);
            return;
        }

        bool needsUpdate = false;
        if (!string.Equals(sub.MenuItemController, "AI", StringComparison.OrdinalIgnoreCase))
        {
            sub.MenuItemController = "AI";
            needsUpdate = true;
        }
        if (!string.Equals(sub.MenuItemAction, "Intelligence", StringComparison.OrdinalIgnoreCase))
        {
            sub.MenuItemAction = "Intelligence";
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            context.SubModules!.Update(sub);
        }
    }

    private static async Task EnsureActionCenterModuleAsync(ApplicationDbContext context)
    {
        var module = await context.Modules!
            .FirstOrDefaultAsync(m => m.Id == ActionCenterModuleId || m.Name == "ZeeU Action Center");

        if (module is null)
        {
            module = new ApplicationModule
            {
                Id = ActionCenterModuleId,
                Name = "ZeeU Action Center",
                Description = "Aviseringar och uppgifter i ZeeU Action Center",
                MenuSectionController = "ActionCenter",
                MenuSectionAction = "Index",
                MenuSectionIcon = "fa fa-bolt",
                MenuSectionText = "ZeeU Action Center",
                MenuSectionEnabled = true,
                MenuSectionSortOrder = 55
            };

            await context.Modules!.AddAsync(module);
            return;
        }

        bool needsUpdate = false;
        if (!string.Equals(module.MenuSectionController, "ActionCenter", StringComparison.OrdinalIgnoreCase))
        {
            module.MenuSectionController = "ActionCenter";
            needsUpdate = true;
        }
        if (!string.Equals(module.MenuSectionAction, "Index", StringComparison.OrdinalIgnoreCase))
        {
            module.MenuSectionAction = "Index";
            needsUpdate = true;
        }
        if (!string.Equals(module.MenuSectionIcon, "fa fa-bolt", StringComparison.OrdinalIgnoreCase))
        {
            module.MenuSectionIcon = "fa fa-bolt";
            needsUpdate = true;
        }
        if (!string.Equals(module.MenuSectionText, "ZeeU Action Center", StringComparison.Ordinal))
        {
            module.MenuSectionText = "ZeeU Action Center";
            needsUpdate = true;
        }
        if (!module.MenuSectionEnabled)
        {
            module.MenuSectionEnabled = true;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            context.Modules!.Update(module);
        }
    }

    private static async Task EnsureActionCenterSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!
            .FirstOrDefaultAsync(sm => sm.Id == ActionCenterSubModuleId || sm.Name == "ZeeU Action Center");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = ActionCenterSubModuleId,
                ModuleId = ActionCenterModuleId,
                Name = "ZeeU Action Center",
                Description = "Aviseringar och uppgifter i ZeeU Action Center",
                MenuItemController = "ActionCenter",
                MenuItemAction = "Index",
                MenuItemText = "SubModule_ZeeU_ActionCenter",
                MenuItemEnabled = true,
                MenuItemSortOrder = 1
            };

            await context.SubModules!.AddAsync(sub);
            return;
        }

        bool needsUpdate = false;
        if (!string.Equals(sub.MenuItemController, "ActionCenter", StringComparison.OrdinalIgnoreCase))
        {
            sub.MenuItemController = "ActionCenter";
            needsUpdate = true;
        }
        if (!string.Equals(sub.MenuItemAction, "Index", StringComparison.OrdinalIgnoreCase))
        {
            sub.MenuItemAction = "Index";
            needsUpdate = true;
        }
        if (!string.Equals(sub.MenuItemText, "SubModule_ZeeU_ActionCenter", StringComparison.Ordinal))
        {
            sub.MenuItemText = "SubModule_ZeeU_ActionCenter";
            needsUpdate = true;
        }
        if (sub.MenuItemEnabled != true)
        {
            sub.MenuItemEnabled = true;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            context.SubModules!.Update(sub);
        }
    }

    private static async Task<Guid> EnsureApprovalModuleAsync(ApplicationDbContext context)
    {
        var approvalModuleCandidates = await context.Modules!
            .Include(m => m.SubModules)
            .Where(m => m.MenuSectionController == "WebApproval")
            .ToListAsync();

        var module = approvalModuleCandidates
            .OrderByDescending(m => m.MenuSectionSortOrder == 6)
            .ThenByDescending(m => string.Equals(m.MenuSectionIcon, "fa fa-thumbs-up", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.SubModules?.Count(sm => LegacyApprovalSubModuleKeys.Contains(sm.MenuItemText ?? string.Empty)) ?? 0)
            .ThenByDescending(m => m.SubModules?.Count ?? 0)
            .FirstOrDefault(m =>
                string.Equals(m.MenuSectionIcon, "fa fa-thumbs-up", StringComparison.OrdinalIgnoreCase) ||
                (m.SubModules?.Any(sm => LegacyApprovalSubModuleKeys.Contains(sm.MenuItemText ?? string.Empty)) ?? false))
            ?? approvalModuleCandidates.FirstOrDefault(m => (m.SubModules?.Count ?? 0) > 0);

        if (module is null)
        {
            module = new ApplicationModule
            {
                Id = Guid.Parse("c4f4a9c0-6c2f-4b7d-9c2b-33e7d1c6a9f2")
            };

            await context.Modules!.AddAsync(module);
        }
        else
        {
            foreach (var duplicate in approvalModuleCandidates.Where(candidate => candidate.Id != module.Id))
            {
                if (duplicate.MenuSectionEnabled)
                {
                    duplicate.MenuSectionEnabled = false;
                    context.Modules!.Update(duplicate);
                }
            }
        }

        module.Name = "Approval";
        module.Description = "Godkännanden och attestflöden i portalen";
        module.MenuSectionController = "WebApproval";
        module.MenuSectionAction = "WebApproval";
        module.MenuSectionIcon = "fa fa-thumbs-up";
        module.MenuSectionText = "Module_WebApproval";
        module.MenuSectionEnabled = true;
        module.MenuSectionSortOrder = 6;

        return module.Id;
    }

    private static async Task EnsureApprovalAttestChainsSubModuleAsync(ApplicationDbContext context, Guid moduleId)
    {
        var sub = await context.SubModules!
            .FirstOrDefaultAsync(sm => sm.Id == WebApprovalAttestChainsSubModuleId || sm.Name == "Attestkedja");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = WebApprovalAttestChainsSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = moduleId;
        sub.Name = "Attestkedja";
        sub.Description = "Simulera och förhandsgranska attestkedjor";
        sub.MenuItemController = "WebApproval";
        sub.MenuItemAction = "AttestChains";
        sub.MenuItemText = "SubModule_WebApproval_AttestChains";
        sub.MenuItemEnabled = true;
        sub.MenuItemSortOrder = 6;
    }

    private static async Task EnsureIntegrationModuleAsync(ApplicationDbContext context)
    {
        var module = await context.Modules!
            .FirstOrDefaultAsync(m => m.Id == IntegrationModuleId || m.Name == "Integration" || m.Name == "Integrationer" || m.Name == "FlowEngine");

        if (module is null)
        {
            module = new ApplicationModule
            {
                Id = IntegrationModuleId
            };

            await context.Modules!.AddAsync(module);
        }

        module.Name = "Integrationer";
        module.Description = "Integrationer och bolagsspecifika API-flöden";
        module.MenuSectionController = "Integration";
        module.MenuSectionAction = "OrderMatching";
        module.MenuSectionIcon = "fa fa-plug";
        module.MenuSectionText = "Module_Integration";
        module.MenuSectionEnabled = true;
        module.MenuSectionSortOrder = 60;
    }

    private static async Task EnsureDocumentSigningModuleAsync(ApplicationDbContext context)
    {
        var module = await context.Modules!
            .FirstOrDefaultAsync(m => m.Id == DocumentSigningModuleId || m.Name == "Dokumentsignering");

        if (module is null)
        {
            module = new ApplicationModule
            {
                Id = DocumentSigningModuleId
            };

            await context.Modules!.AddAsync(module);
        }

        module.Name = "Dokumentsignering";
        module.Description = "Elektronisk signering av offerter och bilagor";
        module.MenuSectionController = "Integration";
        module.MenuSectionAction = "DocumentSigning";
        module.MenuSectionIcon = "fa fa-pencil-square-o";
        module.MenuSectionText = "Module_DocumentSigning";
        module.MenuSectionEnabled = true;
        module.MenuSectionSortOrder = 59;
    }

    private static async Task EnsureBankReconciliationModuleAsync(ApplicationDbContext context)
    {
        var module = await context.Modules!
            .FirstOrDefaultAsync(m => m.Id == PortalModuleIds.BankReconciliationModule || m.Name == "Bankavstämning");

        if (module is null)
        {
            module = new ApplicationModule
            {
                Id = PortalModuleIds.BankReconciliationModule
            };

            await context.Modules!.AddAsync(module);
        }

        module.Name = "Bankavstämning";
        module.Description = "Import av bankfiler och avstämning mot fakturor";
        module.MenuSectionController = "Integration";
        module.MenuSectionAction = "BankReconciliation";
        module.MenuSectionIcon = "fa fa-university";
        module.MenuSectionText = "Module_BankReconciliation";
        module.MenuSectionEnabled = true;
        module.MenuSectionSortOrder = 58;
    }

    private static async Task EnsureSpeedreconModuleAsync(ApplicationDbContext context)
    {
        var module = await context.Modules!
            .FirstOrDefaultAsync(m => m.Id == SpeedreconModuleId || m.Name == "Speedrecon");

        if (module is null)
        {
            module = new ApplicationModule
            {
                Id = SpeedreconModuleId
            };

            await context.Modules!.AddAsync(module);
        }

        module.Name = "Speedrecon";
        module.Description = "Jeeves-baserad avstamning for reskontra, lager och periodiseringar";
        module.MenuSectionController = "Speedrecon";
        module.MenuSectionAction = "Speedrecon";
        module.MenuSectionIcon = "fa fa-balance-scale";
        module.MenuSectionText = "Module_Speedrecon";
        module.MenuSectionEnabled = true;
        module.MenuSectionSortOrder = 57;
    }

    private static async Task EnsureSpeedreconSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!
            .FirstOrDefaultAsync(sm => sm.Id == SpeedreconSubModuleId || sm.Name == "Speedrecon");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = SpeedreconSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = SpeedreconModuleId;
        sub.Name = "Speedrecon";
        sub.Description = "Status, schema och korning av Speedrecon i Jeeves";
        sub.MenuItemController = "Speedrecon";
        sub.MenuItemAction = "Speedrecon";
        sub.MenuItemText = "SubModule_Speedrecon";
        sub.MenuItemEnabled = true;
        sub.MenuItemSortOrder = 1;
    }

    private static async Task EnsureCustomerSyncSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!
            .FirstOrDefaultAsync(sm => sm.Id == CustomerSyncSubModuleId || sm.Name == "CustomerSync" || sm.Name == "Kundsynk");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = CustomerSyncSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "Kundsynk";
        sub.Description = "Synk av kunddata mellan Jeeves och HubSpot";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "CustomerSync";
        sub.MenuItemText = "SubModule_CustomerSync";
        sub.MenuItemEnabled = true;
        sub.MenuItemSortOrder = 10;
    }

    private static async Task EnsureOrderMatchingSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!
            .FirstOrDefaultAsync(sm => sm.Id == OrderMatchingSubModuleId || sm.Name == "Ordermatchning" || sm.Name == "Centra");

        if (sub == null)
        {
            sub = new ApplicationSubModule
            {
                Id = OrderMatchingSubModuleId,
                ModuleId = IntegrationModuleId,
                Name = "Centra",
                Description = "Centra-flöden i FlowEngine",
                MenuItemController = "Integration",
                MenuItemAction = "FlowEngineCentra",
                MenuItemText = "SubModule_FlowEngine_Centra",
                MenuItemEnabled = true,
                MenuItemSortOrder = 2
            };

            await context.SubModules!.AddAsync(sub);
            return;
        }

        var needsUpdate = false;
        if (!string.Equals(sub.Name, "Centra", StringComparison.Ordinal))
        {
            sub.Name = "Centra";
            needsUpdate = true;
        }
        if (!string.Equals(sub.Description, "Centra-flöden i FlowEngine", StringComparison.Ordinal))
        {
            sub.Description = "Centra-flöden i FlowEngine";
            needsUpdate = true;
        }
        if (!string.Equals(sub.MenuItemText, "SubModule_FlowEngine_Centra", StringComparison.Ordinal))
        {
            sub.MenuItemText = "SubModule_FlowEngine_Centra";
            needsUpdate = true;
        }
        if (!string.Equals(sub.MenuItemController, "Integration", StringComparison.OrdinalIgnoreCase))
        {
            sub.MenuItemController = "Integration";
            needsUpdate = true;
        }
        if (!string.Equals(sub.MenuItemAction, "FlowEngineCentra", StringComparison.OrdinalIgnoreCase))
        {
            sub.MenuItemAction = "FlowEngineCentra";
            needsUpdate = true;
        }
        if (sub.MenuItemSortOrder != 2)
        {
            sub.MenuItemSortOrder = 2;
            needsUpdate = true;
        }
        if (sub.MenuItemEnabled != true)
        {
            sub.MenuItemEnabled = true;
            needsUpdate = true;
        }

        if (needsUpdate)
            context.SubModules!.Update(sub);
    }

    private static async Task EnsureAkeneoExportSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm => sm.Id == AkeneoExportSubModuleId || sm.Name == "Akeneo export" || sm.Name == "FlowEngine Akeneo" || sm.Name == "Akeneo");
        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = AkeneoExportSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "Akeneo";
        sub.Description = "Akeneo-flöden och export i FlowEngine";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "FlowEngineAkeneo";
        sub.MenuItemText = "SubModule_FlowEngine_Akeneo";
        sub.MenuItemEnabled = true;
        sub.MenuItemSortOrder = 4;
    }

    private static async Task EnsureOngoingSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm => sm.Id == OngoingSubModuleId || sm.Name == "Ongoing");
        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = OngoingSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "Ongoing";
        sub.Description = "Kontroll mot Ongoing (order matchning)";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "OrderMatching";
        sub.MenuItemText = "SubModule_Ongoing";
        sub.MenuItemEnabled = false;
        sub.MenuItemSortOrder = 8;
    }

    private static async Task HideBankReconciliationIntegrationSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm => sm.Id == PortalModuleIds.BankReconciliationSubModule || sm.Name == "Bankavstämning");
        if (sub is null)
            return;

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "Bankavstämning";
        sub.Description = "Matcha banktransaktioner (camt.053) mot fakturor";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "BankReconciliation";
        sub.MenuItemText = "SubModule_BankReconciliation";
        sub.MenuItemEnabled = false;
        sub.MenuItemSortOrder = 9;
    }

    internal static async Task HideCustomerSyncModuleAsync(ApplicationDbContext context)
    {
        var customerSyncNames = new[] { "customersync", "kundsynk" };

        var customerSyncModules = await context.Modules!
            .Where(module =>
                module.MenuSectionEnabled
                && (
                    (module.Name != null && customerSyncNames.Contains(module.Name.ToLower()))
                    || module.MenuSectionText == "Module_CustomerSync"
                    || module.MenuSectionController == "CustomerSync"
                    || module.MenuSectionAction == "CustomerSync"))
            .ToListAsync();

        if (customerSyncModules.Count == 0)
            return;

        foreach (var module in customerSyncModules)
        {
            module.MenuSectionEnabled = false;
            context.Modules!.Update(module);
        }
    }

    private static async Task EnsureDocumentSigningSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm =>
            sm.Id == PortalModuleIds.DocumentSigningSubModule ||
            sm.Name == "Dokumentsignering");

        if (sub == null)
        {
            sub = new ApplicationSubModule
            {
                Id = PortalModuleIds.DocumentSigningSubModule
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = DocumentSigningModuleId;
        sub.Name = "Dokumentsignering";
        sub.Description = "Elektronisk signering av offerter och bilagor";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "DocumentSigning";
        sub.MenuItemText = "SubModule_DocumentSigning";
        sub.MenuItemEnabled = true;
        sub.MenuItemSortOrder = 10;
    }

    private static async Task EnsureFlowEngineSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm =>
            sm.Id == FlowEngineSubModuleId ||
            sm.Name == "FlowEngine");

        if (sub == null)
        {
            sub = new ApplicationSubModule
            {
                Id = FlowEngineSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "FlowEngine";
        sub.Description = "Dashboard för FlowEngine och operativa integrationsflöden";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "FlowEngine";
        sub.MenuItemText = "SubModule_FlowEngine";
        sub.MenuItemEnabled = true;
        sub.MenuItemSortOrder = 1;
    }

    private static async Task EnsureFlowEngineJeevesSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm =>
            sm.Id == FlowEngineJeevesSubModuleId ||
            sm.Name == "FlowEngine Jeeves" ||
            sm.Name == "Jeeves");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = FlowEngineJeevesSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "Jeeves";
        sub.Description = "Jeeves-lasningar och skrivflöden i FlowEngine";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "FlowEngineJeeves";
        sub.MenuItemText = "SubModule_FlowEngine_Jeeves";
        sub.MenuItemEnabled = true;
        sub.MenuItemSortOrder = 3;
    }

    private static async Task EnsureFlowEngineShopifySubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm =>
            sm.Id == FlowEngineShopifySubModuleId ||
            sm.Name == "FlowEngine Shopify" ||
            sm.Name == "Shopify");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = FlowEngineShopifySubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "Shopify";
        sub.Description = "Shopify fetch, validate, send och fulfillment i FlowEngine";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "FlowEngineShopify";
        sub.MenuItemText = "SubModule_FlowEngine_Shopify";
        sub.MenuItemEnabled = true;
        sub.MenuItemSortOrder = 5;
    }

    private static async Task EnsureFlowEngineJobsSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm =>
            sm.Id == FlowEngineJobsSubModuleId ||
            sm.Name == "FlowEngine Jobs" ||
            sm.Name == "Jobs");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = FlowEngineJobsSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "Jobs";
        sub.Description = "Jobbhistorik, output och felsökning för FlowEngine";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "FlowEngineJobs";
        sub.MenuItemText = "SubModule_FlowEngine_Jobs";
        sub.MenuItemEnabled = false;
        sub.MenuItemSortOrder = 6;
    }

    private static async Task EnsureFlowEngineConfigSubModuleAsync(ApplicationDbContext context)
    {
        var sub = await context.SubModules!.FirstOrDefaultAsync(sm =>
            sm.Id == FlowEngineConfigSubModuleId ||
            sm.Name == "FlowEngine Config" ||
            sm.Name == "Config");

        if (sub is null)
        {
            sub = new ApplicationSubModule
            {
                Id = FlowEngineConfigSubModuleId
            };

            await context.SubModules!.AddAsync(sub);
        }

        sub.ModuleId = IntegrationModuleId;
        sub.Name = "Config";
        sub.Description = "Konfiguration och runtime-validering för FlowEngine";
        sub.MenuItemController = "Integration";
        sub.MenuItemAction = "FlowEngineConfig";
        sub.MenuItemText = "SubModule_FlowEngine_Config";
        sub.MenuItemEnabled = false;
        sub.MenuItemSortOrder = 7;
    }

    private static async Task EnsureFlowEngineSectionPermissionsFromDashboardAsync(ApplicationDbContext context, Guid subModuleId)
    {
        var existingDashboardCompanyIds = await context.CompanyPermissions!
            .Where(cp => cp.SubModuleId == FlowEngineSubModuleId)
            .Select(cp => cp.CompanyId)
            .ToListAsync();

        if (existingDashboardCompanyIds.Count == 0)
            return;

        var existingTargetCompanyIds = await context.CompanyPermissions!
            .Where(cp => cp.SubModuleId == subModuleId)
            .Select(cp => cp.CompanyId)
            .ToListAsync();

        var targetSet = new HashSet<Guid?>(existingTargetCompanyIds);
        foreach (var companyId in existingDashboardCompanyIds)
        {
            if (targetSet.Contains(companyId))
                continue;

            await context.CompanyPermissions!.AddAsync(new ApplicationCompanyPermission
            {
                CompanyId = companyId,
                ModuleId = IntegrationModuleId,
                SubModuleId = subModuleId
            });
        }
    }


    /// <summary>
    /// Grants company permissions for the given submodule to all companies that lack it.
    /// Safe to run repeatedly; only inserts missing rows.
    /// </summary>
    private static async Task EnsureCompanyPermissionsAsync(ApplicationDbContext context, Guid moduleId, Guid? subModuleId = null)
    {
        var companyIds = await context.Companies!
            .Select(c => c.Id)
            .ToListAsync();

        var existing = await context.CompanyPermissions!
            .Where(cp => cp.SubModuleId == subModuleId)
            .Select(cp => cp.CompanyId)
            .ToListAsync();

        var existingSet = new HashSet<Guid?>(existing);

        foreach (var companyId in companyIds)
        {
            if (existingSet.Contains(companyId)) continue;

            await context.CompanyPermissions!.AddAsync(new ApplicationCompanyPermission
            {
                CompanyId = companyId,
                ModuleId = moduleId,
                SubModuleId = subModuleId
            });
        }
    }
}
