namespace WebApp.Helpers
{
    public interface IApplicationHelper
    {
        //Task<(bool, string)> AddUserObjectToSession(string mailAddress);

        Task<bool> AddUserToSession(string email);
    }
}
