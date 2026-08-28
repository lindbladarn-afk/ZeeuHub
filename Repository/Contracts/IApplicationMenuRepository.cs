namespace Repository.Contracts;

public interface IApplicationMenuRepository
{
    Task<SideMenuViewModel> GetMenuAsync(Guid companyId);
}
