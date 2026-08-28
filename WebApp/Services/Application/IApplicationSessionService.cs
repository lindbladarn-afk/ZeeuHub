using Entities.Application;
namespace WebApp.Services.Application;

public interface IApplicationSessionService
{
    bool TrySetUserSession(UserSession sessionUser);
}
