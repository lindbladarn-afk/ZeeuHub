using Entities.Application;
using Entities.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using WebApp.Controllers;
using WebApp.Models.ActionCenter;
using WebApp.Models.ControlPanel;
using WebApp.Helpers;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Services.ControlPanel;
using WebApp.Services.ActionCenter;

namespace WebApp.Tests;

// Regression tests for tenant and authorization boundaries in ZeeU Hub.
public sealed class AuthorizationGuardTests
{
    [Fact]
    public void ControlPanelAccess_Allows_Only_ZeeU_Tenant()
    {
        var service = CreateControlPanelAccessService();

        var allowed = service.IsAuthorizedTenant(new UserSession
        {
            UserId = "user-1",
            CompanyName = "zeeu ab"
        });

        var denied = service.IsAuthorizedTenant(new UserSession
        {
            UserId = "user-2",
            CompanyName = "Other AB"
        });

        Assert.True(allowed);
        Assert.False(denied);
    }

    [Fact]
    public void TenantGuard_Allows_Only_Companies_In_User_Scope()
    {
        var guard = CreateGuard(new[] { 100, 200 });
        var session = new UserSession
        {
            UserId = "user-1",
            JeevesActiveCompany = 100
        };

        var result = guard.Validate(session);

        Assert.True(result.Success);
        Assert.True(result.Value);
    }

    [Fact]
    public void TenantGuard_Blocks_Requesting_Company_Outside_User_Scope()
    {
        var guard = CreateGuard(new[] { 100, 200 });
        var session = new UserSession
        {
            UserId = "user-1",
            JeevesActiveCompany = 100
        };

        var result = guard.Validate(session, requestedCompanyCode: 300);

        Assert.False(result.Success);
        Assert.Equal("Unauthorized company", result.Error);
    }

    [Fact]
    public void TenantGuard_Blocks_Missing_Or_Incomplete_Session()
    {
        var guard = CreateGuard(new[] { 100 });

        var missingSession = guard.Validate(null);
        var missingActiveCompany = guard.Validate(new UserSession { UserId = "user-1" });

        Assert.False(missingSession.Success);
        Assert.False(missingActiveCompany.Success);
        Assert.Equal("No session user", missingSession.Error);
        Assert.Equal("No active company", missingActiveCompany.Error);
    }

    [Fact]
    public async Task ConnectionStringResolver_Blocks_CrossCompany_Mapping()
    {
        var resolver = new ConnectionStringResolver(new NullLogger<ConnectionStringResolver>());
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var wrongCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var activeConnectionStringId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var result = await resolver.ResolveAsync(
            new[]
            {
                new WebApp.Models.Identity.ApplicationCompanyConnectionStrings
                {
                    Id = activeConnectionStringId,
                    CompanyId = wrongCompanyId,
                    ConnectionStringTypeId = Guid.NewGuid(),
                    DatabaseName = "TenantDb",
                    IsActive = true
                }
            },
            activeConnectionStringId,
            companyId);

        Assert.False(result.Success);
        Assert.Contains("does not belong to this company", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ConnectionStringResolver_Resolves_Only_When_Environment_Secret_Belongs_To_Requesting_Company()
    {
        var resolver = new ConnectionStringResolver(new NullLogger<ConnectionStringResolver>());
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var activeConnectionStringId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var envKey = $"CONNECTION_STRING_{activeConnectionStringId.ToString().ToUpper().Replace("-", string.Empty)}";
        var previous = Environment.GetEnvironmentVariable(envKey);

        try
        {
            Environment.SetEnvironmentVariable(envKey, "Server=test;Database=TenantDb;User Id=demo;Password=secret;");

            var result = await resolver.ResolveAsync(
                new[]
                {
                    new WebApp.Models.Identity.ApplicationCompanyConnectionStrings
                    {
                        Id = activeConnectionStringId,
                        CompanyId = companyId,
                        ConnectionStringTypeId = Guid.NewGuid(),
                        DatabaseName = "TenantDb",
                        IsActive = true
                    }
                },
                activeConnectionStringId,
                companyId);

            Assert.True(result.Success);
            Assert.Equal("Server=test;Database=TenantDb;User Id=demo;Password=secret;", result.Value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, previous);
        }
    }

    [Fact]
    public async Task ActionCenterController_Returns_Generic_Message_When_Update_Fails()
    {
        var controller = CreateActionCenterController(new ThrowingActionCenterStateStore());
        controller.ControllerContext = CreateControllerContextWithSessionUser(new UserSession
        {
            UserId = "user-1",
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        });

        var result = await controller.UpdateStatus(new ActionCenterUpdateRequest
        {
            InsightId = "insight-1",
            Status = "Active"
        }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);

        var payloadJson = JsonSerializer.Serialize(objectResult.Value);
        using var payload = JsonDocument.Parse(payloadJson);
        Assert.False(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Kunde inte uppdatera status just nu.", payload.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ActionCenterController_Rejects_Invalid_Status_Before_Persisting()
    {
        var stateStore = new RecordingActionCenterStateStore();
        var controller = CreateActionCenterController(stateStore);
        controller.ControllerContext = CreateControllerContextWithSessionUser(new UserSession
        {
            UserId = "user-1",
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        });

        var result = await controller.UpdateStatus(new ActionCenterUpdateRequest
        {
            InsightId = "insight-1",
            Status = "Archived"
        }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        using var payload = JsonDocument.Parse(payloadJson);
        Assert.False(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Ogiltig status.", payload.RootElement.GetProperty("message").GetString());
        Assert.False(stateStore.WasCalled);
    }

    [Fact]
    public void ActionCenterUpdateRequest_Accepts_Priority_String_From_Dashboard_Payload()
    {
        var request = JsonSerializer.Deserialize<ActionCenterUpdateRequest>(
            """
            {
              "insightId": "insight-1",
              "status": "Completed",
              "priority": "High"
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal(ActionCenterPriority.High, request!.Priority);
    }

    private static TenantGuard CreateGuard(IEnumerable<int> companies)
        => new(new FakeJeevesCompanyAccessService(companies), new NullLogger<TenantGuard>());

    private static ControlPanelAccessService CreateControlPanelAccessService()
        => new(Options.Create(new ControlPanelOptions
        {
            AllowedCompanyName = "ZeeU AB"
        }));

    private sealed class FakeJeevesCompanyAccessService : IJeevesCompanyAccessService
    {
        private readonly IReadOnlyList<JeevesCompanyVM> _companies;

        public FakeJeevesCompanyAccessService(IEnumerable<int> companyCodes)
        {
            _companies = companyCodes
                .Select(code => new JeevesCompanyVM
                {
                    CompanyCode = code,
                    Name = $"Company {code}",
                    IsDefault = code == companyCodes.FirstOrDefault()
                })
                .ToList();
        }

        public Task<IReadOnlyList<JeevesCompanyVM>> GetCompaniesAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(_companies);

        public Task<bool> HasCompanyAccessAsync(UserSession? sessionUser, int companyCode, CancellationToken cancellationToken = default)
            => Task.FromResult(_companies.Any(x => x.CompanyCode == companyCode));

        public Task<int?> ResolveCompanyCodeAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(_companies.FirstOrDefault()?.CompanyCode);

        public Task<string> ResolveCompanyNameAsync(UserSession? sessionUser, int? companyCode, CancellationToken cancellationToken = default)
            => Task.FromResult(_companies.FirstOrDefault(x => x.CompanyCode == companyCode)?.Name ?? string.Empty);

        public void Store(UserSession? sessionUser, IReadOnlyList<JeevesCompanyVM> companies)
        {
        }
    }

    private static ActionCenterController CreateActionCenterController(IActionCenterStateStore stateStore)
        => new(stateStore, new FakeActionCenterService(), NullLogger<ActionCenterController>.Instance);

    private static ControllerContext CreateControllerContextWithSessionUser(UserSession sessionUser)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new TestSessionFeature());
        context.Session.Set("UserObject", sessionUser);
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, sessionUser.UserId),
            new Claim(ClaimTypes.Email, sessionUser.Email ?? "user@example.com")
        }, "Cookies"));

        return new ControllerContext { HttpContext = context };
    }

    private sealed class ThrowingActionCenterStateStore : IActionCenterStateStore
    {
        public Task<IReadOnlyList<ActionCenterItemState>> GetStatesAsync(Guid? companyId, string userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ActionCenterItemState>>(Array.Empty<ActionCenterItemState>());

        public Task UpsertAsync(string externalId, ActionCenterItemStatus status, Guid? companyId, string userId, ActionCenterUpdateRequest snapshot, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Database details should not leak");

        public Task<IReadOnlyList<ActionCenterItemState>> GetHistoryAsync(Guid? companyId, string userId, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ActionCenterItemState>>(Array.Empty<ActionCenterItemState>());
    }

    private sealed class RecordingActionCenterStateStore : IActionCenterStateStore
    {
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<ActionCenterItemState>> GetStatesAsync(Guid? companyId, string userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ActionCenterItemState>>(Array.Empty<ActionCenterItemState>());

        public Task UpsertAsync(string externalId, ActionCenterItemStatus status, Guid? companyId, string userId, ActionCenterUpdateRequest snapshot, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ActionCenterItemState>> GetHistoryAsync(Guid? companyId, string userId, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ActionCenterItemState>>(Array.Empty<ActionCenterItemState>());
    }

    private sealed class FakeActionCenterService : IActionCenterService
    {
        public Task<ActionCenterViewModel> GetInsightsAsync(UserSession user, int take, CancellationToken cancellationToken)
            => Task.FromResult(new ActionCenterViewModel());

        public Task<ActionCenterSummaryDto> GetSummaryAsync(UserSession user, CancellationToken cancellationToken)
            => Task.FromResult(new ActionCenterSummaryDto());

        public void InvalidateCache(UserSession user)
        {
        }
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value.ToArray();
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
