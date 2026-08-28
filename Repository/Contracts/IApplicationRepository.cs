namespace Repository.Contracts;

public interface IApplicationRepository
{
	/// <summary>
	/// Method for retrieving the modules and permissions for the menu
	/// </summary>
	/// <returns>A list of modules and sub modules</returns>
	Task<SideMenuViewModel> GetMenuAsync(Guid companyId);

	Task<IUser> GetUserAsync(string userId);

}