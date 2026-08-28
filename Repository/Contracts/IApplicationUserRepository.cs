namespace Repository.Contracts;

public interface IApplicationUserRepository
{
    Task<IUser> GetUserAsync(string userId);
}
