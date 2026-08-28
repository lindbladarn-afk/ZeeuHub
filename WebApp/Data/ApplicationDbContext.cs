// Configures the portal identity and application persistence model.
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApp.Models.AI;
using WebApp.Models.Identity;
using WebApp.Models.ActionCenter;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.DocumentSigning;
using WebApp.Models.Dashboard;
using WebApp.Models.Integration;
using WebApp.Models.Integration.BankReconciliation;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Models.Application;
using WebApp.Models.Admin.ApprovalChains;

namespace WebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema("Identity");
            builder.Entity<ApplicationUser>(entity => { entity.ToTable(name: "Users"); });
            builder.Entity<IdentityRole>(entity => { entity.ToTable(name: "Roles"); });
            builder.Entity<IdentityUserRole<string>>(entity => { entity.ToTable("UserRoles"); });
            builder.Entity<IdentityUserClaim<string>>(entity => { entity.ToTable("UserClaims"); });
            builder.Entity<IdentityUserLogin<string>>(entity => { entity.ToTable("UserLogins"); });
            builder.Entity<IdentityRoleClaim<string>>(entity => { entity.ToTable("RoleClaims"); });
            builder.Entity<IdentityUserToken<string>>(entity => { entity.ToTable("UserTokens"); });

            builder.Entity<ApplicationCompanyLicense>()
                .HasOne(e => e.Company)
                .WithMany(e => e.Licenses)
                .OnDelete(DeleteBehavior.NoAction);
            builder.Entity<ApplicationCompanyLicense>()
                .HasOne(e => e.ZeeuProduct)
                .WithMany(e => e.Licenses)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ApplicationCompany>()
                .HasMany(e => e.Permissions)
                .WithOne(e => e.Company)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ApplicationCompany>()
                .HasMany(e => e.ConnectionStrings)
                .WithOne(e => e.Company)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ApplicationCompany>()
                .HasMany(e => e.JeevesCompanies)
                .WithOne(e => e.Company)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApplicationCompanyJeevesCompany>(entity =>
            {
                entity.ToTable("CompanyJeevesCompanies");
                entity.HasIndex(e => new { e.CompanyId, e.CompanyCode }).IsUnique();
            });

            builder.Entity<ApplicationModule>()
                .HasMany(e => e.SubModules)
                .WithOne(e => e.Module)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ApplicationConnectionStringTypes>()
                .HasMany(e => e.ConnectionStrings)
                .WithOne(e => e.ConnectionStringType)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ApplicationUserWhitelist>(entity =>
            {
                entity.ToTable("UserWhitelists");
                entity.HasIndex(e => new { e.CompanyId, e.Email, e.UserId });
            });

            builder.Entity<ApplicationUserCompanyAccess>(entity =>
            {
                entity.ToTable("UserCompanyAccesses");
                entity.HasIndex(e => new { e.UserId, e.CompanyCode }).IsUnique();
                entity.HasOne(e => e.User)
                    .WithMany(u => u.AllowedCompanyCodes)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ApplicationUserPermission>(entity =>
            {
                entity.ToTable("UserPermissions");
                entity.HasIndex(e => new { e.UserId, e.ModuleId, e.SubModuleId }).IsUnique();
                entity.HasIndex(e => new { e.CompanyId, e.UserId });
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Permissions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ActionCenterItemState>()
                .HasIndex(e => new { e.ExternalId, e.CompanyId, e.UserId })
                .IsUnique(false);

            builder.Entity<AiQuotaPolicy>(entity =>
            {
                entity.ToTable("AiQuotaPolicies");
                entity.HasIndex(e => e.IsGlobal)
                    .IsUnique()
                    .HasFilter("[IsGlobal] = 1");
                entity.HasIndex(e => e.CompanyId)
                    .IsUnique()
                    .HasFilter("[IsGlobal] = 0 AND [CompanyId] IS NOT NULL");
            });

            builder.Entity<WebApp.Models.Telemetry.UserUsageTotal>(entity =>
            {
                entity.ToTable("UserUsageTotals");
                entity.HasKey(e => new { e.UserId, e.CompanyId });
            });

            builder.Entity<DocumentSigningRecord>(entity =>
            {
                entity.ToTable("DocumentSignings");
                entity.HasIndex(e => e.DocumentId).IsUnique();
                entity.HasIndex(e => e.PublicToken).IsUnique();
                entity.HasIndex(e => new { e.CompanyId, e.CorrelationKey }).IsUnique();
                entity.HasIndex(e => new { e.CompanyId, e.JeevesCompanyCode, e.OrderNo });
            });

            builder.Entity<DocumentSigningParticipantRecord>(entity =>
            {
                entity.ToTable("DocumentSigningParticipants");
                entity.HasIndex(e => new { e.SigningId, e.OneflowParticipantId }).IsUnique();
                entity.HasIndex(e => new { e.SigningId, e.NormalizedEmail, e.IsSignatory, e.IsMyParticipant });
            });

            builder.Entity<FlowEngineJobRecord>(entity =>
            {
                entity.ToTable("FlowEngineJobs");
                entity.HasIndex(e => new { e.CompanyId, e.CreatedAtUtc });
            });

            builder.Entity<BackgroundJobRecord>(entity =>
            {
                entity.ToTable("BackgroundJobs", tableBuilder =>
                {
                    tableBuilder.HasCheckConstraint("CK_BackgroundJobs_Status",
                        "[Status] IN ('Queued', 'Running', 'Completed', 'Failed', 'Canceled')");
                });
                entity.HasIndex(e => new { e.CompanyId, e.CreatedAtUtc });
                entity.HasIndex(e => new { e.CompanyId, e.Status, e.CreatedAtUtc });
                entity.HasIndex(e => new { e.CompanyId, e.CorrelationKey });
                entity.HasIndex(e => new { e.Status, e.AvailableAtUtc });
                entity.HasIndex(e => new { e.Status, e.LeaseExpiresAtUtc });
                entity.HasIndex(e => new { e.CompanyId, e.UpdatedAtUtc });
            });

            builder.Entity<BackgroundJobRuntimeEventRecord>(entity =>
            {
                entity.ToTable("BackgroundJobRuntimeEvents");
                entity.HasIndex(e => new { e.CompanyId, e.OccurredAtUtc });
                entity.HasIndex(e => new { e.CompanyId, e.AggregateKey, e.OccurredAtUtc });
                entity.HasIndex(e => e.JobId);
            });

            builder.Entity<CustomerSyncMappingRecord>(entity =>
            {
                entity.ToTable("CustomerSyncMappings");
                entity.HasIndex(e => new { e.CompanyId, e.JeevesCompanyCode, e.JeevesCustomerNumber })
                    .IsUnique()
                    .HasFilter("[JeevesCustomerNumber] IS NOT NULL");
                entity.HasIndex(e => new { e.CompanyId, e.HubSpotCompanyId })
                    .IsUnique()
                    .HasFilter("[HubSpotCompanyId] IS NOT NULL");
                entity.HasIndex(e => new { e.CompanyId, e.OrganizationNumber });
                entity.Property(e => e.JeevesCustomerNumber).HasMaxLength(64);
                entity.Property(e => e.HubSpotCompanyId).HasMaxLength(64);
                entity.Property(e => e.HubSpotContactId).HasMaxLength(64);
                entity.Property(e => e.OrganizationNumber).HasMaxLength(64);
                entity.Property(e => e.NormalizedName).HasMaxLength(256);
                entity.Property(e => e.Domain).HasMaxLength(256);
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.Phone).HasMaxLength(64);
            });

            builder.Entity<CustomerSyncCheckpointRecord>(entity =>
            {
                entity.ToTable("CustomerSyncCheckpoints");
                entity.HasIndex(e => new { e.CompanyId, e.JeevesCompanyCode, e.Direction }).IsUnique();
                entity.Property(e => e.Direction).HasMaxLength(64);
                entity.Property(e => e.CheckpointValue).HasMaxLength(256);
            });

            builder.Entity<CustomerSyncRunRecord>(entity =>
            {
                entity.ToTable("CustomerSyncRuns");
                entity.HasIndex(e => new { e.CompanyId, e.StartedAtUtc });
                entity.HasIndex(e => new { e.CompanyId, e.Direction, e.Status, e.StartedAtUtc });
                entity.Property(e => e.Direction).HasMaxLength(64);
                entity.Property(e => e.Trigger).HasMaxLength(64);
                entity.Property(e => e.Status).HasMaxLength(64);
                entity.Property(e => e.CorrelationId).HasMaxLength(128);
            });

            builder.Entity<CustomerSyncRunItemRecord>(entity =>
            {
                entity.ToTable("CustomerSyncRunItems");
                entity.HasIndex(e => new { e.CompanyId, e.CreatedAtUtc });
                entity.HasIndex(e => e.RunId);
                entity.Property(e => e.ExternalKey).HasMaxLength(128);
                entity.Property(e => e.JeevesCustomerNumber).HasMaxLength(64);
                entity.Property(e => e.HubSpotObjectId).HasMaxLength(64);
                entity.Property(e => e.Status).HasMaxLength(64);
                entity.Property(e => e.ErrorCode).HasMaxLength(64);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.HasOne(e => e.Run)
                    .WithMany(e => e.Items)
                    .HasForeignKey(e => e.RunId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CustomerSyncEventRecord>(entity =>
            {
                entity.ToTable("CustomerSyncEvents");
                entity.HasIndex(e => new { e.CompanyId, e.HubSpotEventId }).IsUnique();
                entity.HasIndex(e => new { e.CompanyId, e.ReceivedAtUtc });
                entity.Property(e => e.HubSpotEventId).HasMaxLength(128);
                entity.Property(e => e.HubSpotObjectId).HasMaxLength(64);
                entity.Property(e => e.EventType).HasMaxLength(128);
                entity.Property(e => e.PayloadHash).HasMaxLength(128);
                entity.Property(e => e.Status).HasMaxLength(64);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            });

            builder.Entity<CustomerSyncRuntimeConfigurationRecord>(entity =>
            {
                entity.ToTable("CustomerSyncRuntimeConfiguration");
                entity.HasIndex(e => e.ConfigurationName).IsUnique();
                entity.Property(e => e.ConfigurationName).HasMaxLength(64);
                entity.Property(e => e.ConfigurationJson);
            });

            builder.Entity<SidebarRuntimeNotificationReadStateRecord>(entity =>
            {
                entity.ToTable("SidebarRuntimeNotificationReadStates");
                entity.HasIndex(e => new { e.UserId, e.CompanyId }).IsUnique();
            });

            builder.Entity<DashboardWidgetPreferenceRecord>(entity =>
            {
                entity.ToTable("DashboardWidgetPreferences");
                entity.HasIndex(e => new { e.UserId, e.CompanyId, e.WidgetId }).IsUnique();
                entity.HasIndex(e => new { e.UserId, e.CompanyId, e.SortOrder });
            });

            builder.Entity<PortalEventLogRecord>(entity =>
            {
                entity.ToTable("EventLogs");
                entity.HasIndex(e => e.OccurredAtUtc);
                entity.HasIndex(e => new { e.CompanyId, e.OccurredAtUtc });
                entity.HasIndex(e => new { e.Module, e.OccurredAtUtc });
                entity.HasIndex(e => new { e.JeevesCompanyCode, e.OccurredAtUtc });
                entity.HasIndex(e => e.CorrelationId);
            });

            builder.Entity<PortalAuthenticationTicketRecord>(entity =>
            {
                entity.ToTable("AuthenticationTickets");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(128);
                entity.Property(e => e.UserId).HasMaxLength(450);
                entity.HasIndex(e => e.ExpiresAtUtc);
                entity.HasIndex(e => new { e.UserId, e.ExpiresAtUtc });
            });

            builder.Entity<ApprovalChainRuleRecord>(entity =>
            {
                entity.ToTable("q_zu_approval_chains", "dbo");
                entity.HasKey(e => new { e.ForetagKod, e.SqlIdentity });
                entity.Property(e => e.SqlIdentity).HasColumnName("SQLIDENTITY");
                entity.Property(e => e.FlowId).HasColumnName("q_zu_approval_flowid");
                entity.Property(e => e.CurrentApproverPersSign).HasColumnName("perssign2").HasMaxLength(60);
                entity.Property(e => e.NextApproverPersSign).HasColumnName("attestsign").HasMaxLength(60);
                entity.Property(e => e.PurchaseOrderType).HasColumnName("besttyp");
                entity.Property(e => e.SalesOrderType).HasColumnName("ordtyp");
                entity.Property(e => e.PriceListId).HasColumnName("prislista");
                entity.Property(e => e.Limit).HasColumnName("attestlimit").HasColumnType("money");
                entity.Property(e => e.NegativeLimit).HasColumnName("q_zu_attestlimit_2").HasColumnType("money");
                entity.Property(e => e.RegisteredAt).HasColumnName("RegDat");
                entity.Property(e => e.PersSign).HasColumnName("PersSign").HasMaxLength(30);
                entity.Property(e => e.RowCreatedBy).HasMaxLength(30);
                entity.Property(e => e.RowCreatedAt).HasColumnName("RowCreatedDt");
                entity.Property(e => e.RowUpdatedBy).HasMaxLength(30);
                entity.Property(e => e.RowUpdatedAt).HasColumnName("RowUpdatedDt");
                entity.Property(e => e.IsDefaultRaw).HasColumnName("q_zu_approval_default").HasMaxLength(1);
                entity.Property(e => e.SendMailRaw).HasColumnName("q_zu_approval_mail").HasMaxLength(1);
            });

            builder.Entity<BankReconciliationStateRecord>(entity =>
            {
                entity.ToTable("BankReconciliationStates");
                entity.HasKey(e => new { e.CompanyId, e.StateKeyHash });
                entity.Property(e => e.StateKeyHash).HasMaxLength(64);
                entity.Property(e => e.StateJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Version).IsConcurrencyToken();
                entity.HasIndex(e => e.UpdatedAtUtc);
            });

            builder.Entity<BankReconciliationImportRegistryRecord>(entity =>
            {
                entity.ToTable("BankReconciliationImportRegistries");
                entity.HasKey(e => new { e.CompanyId, e.AccountFingerprint });
                entity.Property(e => e.AccountFingerprint).HasMaxLength(64);
                entity.Property(e => e.RegistryJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Version).IsConcurrencyToken();
                entity.HasIndex(e => e.UpdatedAtUtc);
            });

            builder.Entity<BankReconciliationCodingRuleRecord>(entity =>
            {
                entity.ToTable("BankReconciliationCodingRules");
                entity.HasKey(e => new { e.CompanyId, e.BankAccountKeyHash });
                entity.Property(e => e.BankAccountKeyHash).HasMaxLength(64);
                entity.Property(e => e.RuleSetJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Version).IsConcurrencyToken();
                entity.HasIndex(e => e.UpdatedAtUtc);
            });

        }


        public DbSet<ApplicationCompany>? Companies { get; set; }
        public DbSet<ApplicationCompanyLicense>? Licenses { get; set; }
        public DbSet<ApplicationCompanyPermission>? CompanyPermissions { get; set; }
        public DbSet<ApplicationConnectionStringTypes>? ConnectionStringTypes { get; set; }
        public DbSet<ApplicationCompanyConnectionStrings>? ConnectionStrings { get; set; }
        public DbSet<ApplicationCompanyJeevesCompany>? CompanyJeevesCompanies { get; set; }
        public DbSet<ApplicationModule>? Modules { get; set; }
        public DbSet<ApplicationSubModule>? SubModules { get; set; }
        public DbSet<ApplicationZeeuProduct>? ZeeuProducts { get; set; }
        public DbSet<ApplicationUserWhitelist>? UserWhitelists { get; set; }
        public DbSet<ApplicationUserCompanyAccess>? UserCompanyAccesses { get; set; }
        public DbSet<ApplicationUserPermission> UserPermissions => Set<ApplicationUserPermission>();
        public DbSet<WebApp.Models.Telemetry.ExcelImportLog>? ExcelImportLogs { get; set; }
        public DbSet<WebApp.Models.Telemetry.AiQueryLog>? AiQueryLogs { get; set; }
        public DbSet<AiQuotaPolicy>? AiQuotaPolicies { get; set; }
        public DbSet<WebApp.Models.Telemetry.UserUsageTotal>? UserUsageTotals { get; set; }
        public DbSet<ActionCenterItemState>? ActionCenterItemStates { get; set; }
        public DbSet<DocumentSigningRecord>? DocumentSignings { get; set; }
        public DbSet<DocumentSigningParticipantRecord>? DocumentSigningParticipants { get; set; }
        public DbSet<FlowEngineJobRecord>? FlowEngineJobs { get; set; }
        public DbSet<BackgroundJobRecord>? BackgroundJobs { get; set; }
        public DbSet<BackgroundJobRuntimeEventRecord>? BackgroundJobRuntimeEvents { get; set; }
        public DbSet<CustomerSyncMappingRecord>? CustomerSyncMappings { get; set; }
        public DbSet<CustomerSyncCheckpointRecord>? CustomerSyncCheckpoints { get; set; }
        public DbSet<CustomerSyncRunRecord>? CustomerSyncRuns { get; set; }
        public DbSet<CustomerSyncRunItemRecord>? CustomerSyncRunItems { get; set; }
        public DbSet<CustomerSyncEventRecord>? CustomerSyncEvents { get; set; }
        public DbSet<CustomerSyncRuntimeConfigurationRecord>? CustomerSyncRuntimeConfiguration { get; set; }
        public DbSet<SidebarRuntimeNotificationReadStateRecord>? SidebarRuntimeNotificationReadStates { get; set; }
        public DbSet<DashboardWidgetPreferenceRecord>? DashboardWidgetPreferences { get; set; }
        public DbSet<PortalEventLogRecord>? PortalEventLogs { get; set; }
        public DbSet<PortalAuthenticationTicketRecord>? PortalAuthenticationTickets { get; set; }
        public DbSet<ApprovalChainRuleRecord>? ApprovalChainRules { get; set; }
        public DbSet<BankReconciliationStateRecord> BankReconciliationStates => Set<BankReconciliationStateRecord>();
        public DbSet<BankReconciliationImportRegistryRecord> BankReconciliationImportRegistries => Set<BankReconciliationImportRegistryRecord>();
        public DbSet<BankReconciliationCodingRuleRecord> BankReconciliationCodingRules => Set<BankReconciliationCodingRuleRecord>();
    }
}
