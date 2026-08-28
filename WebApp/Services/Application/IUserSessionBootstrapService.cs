namespace WebApp.Services.Application;

// Builds and persists the portal session user after login.
// This keeps login/session bootstrap logic out of MVC helpers and makes the flow easier to test in isolation.
public interface IUserSessionBootstrapService
{
    Task<bool> AddUserToSessionAsync(string email);
}
