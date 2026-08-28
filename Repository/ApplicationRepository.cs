namespace Repository;

public class ApplicationRepository : IApplicationRepository
{
    private readonly IApplicationMenuRepository _menuRepository;
    private readonly IApplicationUserRepository _userRepository;

    public ApplicationRepository(
        IApplicationMenuRepository menuRepository,
        IApplicationUserRepository userRepository)
    {
        _menuRepository = menuRepository;
        _userRepository = userRepository;
    }

    public Task<SideMenuViewModel> GetMenuAsync(Guid companyId)
        => _menuRepository.GetMenuAsync(companyId);

    public Task<IUser> GetUserAsync(string userId)
        => _userRepository.GetUserAsync(userId);
}
