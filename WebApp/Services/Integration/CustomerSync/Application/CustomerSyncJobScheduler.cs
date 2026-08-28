using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.CustomerSync.Background;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Application;

// Queues customer sync jobs with stable correlation keys to prevent duplicate active work.
public sealed class CustomerSyncJobScheduler
{
    private readonly IBackgroundJobStore _backgroundJobStore;

    public CustomerSyncJobScheduler(IBackgroundJobStore backgroundJobStore)
    {
        _backgroundJobStore = backgroundJobStore;
    }

    public BackgroundJobSnapshot EnqueueJeevesToHubSpotIfMissing(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncTrigger trigger,
        DateTime utcNow)
    {
        var correlationKey = BuildBatchCorrelationKey(companyId, jeevesCompanyCode, CustomerSyncDirection.JeevesToHubSpot);
        return EnqueueIfMissing(
            companyId,
            correlationKey,
            new CustomerSyncBackgroundJobPayload
            {
                CompanyId = companyId,
                JeevesCompanyCode = jeevesCompanyCode,
                Direction = CustomerSyncDirection.JeevesToHubSpot,
                Trigger = trigger,
                CorrelationKey = correlationKey
            },
            utcNow);
    }

    public BackgroundJobSnapshot EnqueueHubSpotToJeevesIfMissing(
        Guid companyId,
        int jeevesCompanyCode,
        string hubSpotEventId,
        string? hubSpotObjectId,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(hubSpotEventId))
            throw new ArgumentException("HubSpot event id is required.", nameof(hubSpotEventId));

        var correlationKey = BuildHubSpotEventCorrelationKey(companyId, hubSpotEventId);
        return EnqueueIfMissing(
            companyId,
            correlationKey,
            new CustomerSyncBackgroundJobPayload
            {
                CompanyId = companyId,
                JeevesCompanyCode = jeevesCompanyCode,
                Direction = CustomerSyncDirection.HubSpotToJeeves,
                Trigger = CustomerSyncTrigger.Webhook,
                HubSpotEventId = hubSpotEventId.Trim(),
                HubSpotObjectId = string.IsNullOrWhiteSpace(hubSpotObjectId) ? null : hubSpotObjectId.Trim(),
                CorrelationKey = correlationKey
            },
            utcNow);
    }

    public static string BuildBatchCorrelationKey(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncDirection direction)
        => $"customersync:{direction.ToString().ToLowerInvariant()}:{companyId:N}:{jeevesCompanyCode}";

    public static string BuildHubSpotEventCorrelationKey(Guid companyId, string hubSpotEventId)
        => $"customersync:hubspot-event:{companyId:N}:{hubSpotEventId.Trim()}";

    private BackgroundJobSnapshot EnqueueIfMissing(
        Guid companyId,
        string correlationKey,
        CustomerSyncBackgroundJobPayload payload,
        DateTime utcNow)
    {
        var existing = _backgroundJobStore.FindActive(
            companyId,
            CustomerSyncBackgroundJobConstants.ExecuteJobType,
            correlationKey);

        if (existing is not null)
            return existing;

        return _backgroundJobStore.Enqueue(
            new BackgroundJobEnqueueRequest
            {
                CompanyId = companyId,
                JobType = CustomerSyncBackgroundJobConstants.ExecuteJobType,
                CorrelationKey = correlationKey,
                PayloadJson = payload.ToJson(),
                MaxAttempts = CustomerSyncBackgroundJobConstants.DefaultMaxAttempts,
                AvailableAtUtc = utcNow
            },
            utcNow);
    }
}
