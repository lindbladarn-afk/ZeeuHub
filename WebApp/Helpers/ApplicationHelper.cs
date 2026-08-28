using WebApp.Services;
using WebApp.Services.Application;

namespace WebApp.Helpers
{
    // Compatibility facade used by login pages and legacy views.
    // The actual session bootstrap now lives in IUserSessionBootstrapService.
    public class ApplicationHelper : IApplicationHelper
    {
        private readonly IUserSessionBootstrapService _userSessionBootstrapService;

        public ApplicationHelper(
            IUserSessionBootstrapService userSessionBootstrapService)
        {
            _userSessionBootstrapService = userSessionBootstrapService;
        }

        public Task<bool> AddUserToSession(string email)
            => _userSessionBootstrapService.AddUserToSessionAsync(email);
    }
}
