using Microsoft.AspNetCore.Mvc;
using WebApp.Controllers;
using System.Reflection;

namespace WebApp.Tests;

// Guards the most important state-changing endpoints against accidental CSRF regressions.
public sealed class SecuritySurfaceTests
{
    [Fact]
    public void ChangeActiveCompany_Is_Post_And_Uses_AntiForgery()
    {
        var method = typeof(BaseController).GetMethod(nameof(BaseController.ChangeActiveCompany));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>(inherit: true));
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Fact]
    public void SetLanguage_Is_Post_And_Uses_AntiForgery()
    {
        var method = typeof(HomeController).GetMethod(nameof(HomeController.SetLanguage));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>(inherit: true));
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Fact]
    public void ResendEmailVerificationToken_Is_Post_And_Uses_AntiForgery()
    {
        var method = typeof(AdminController).GetMethod(nameof(AdminController.ResendEmailVerificationToken));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>(inherit: true));
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Fact]
    public void GetJeevesCompanies_Is_Post_And_Uses_AntiForgery()
    {
        var method = typeof(AdminController).GetMethod(nameof(AdminController.GetJeevesCompanies));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>(inherit: true));
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Fact]
    public void ProductionDashboardAdmin_Update_Is_Post_And_Uses_AntiForgery()
    {
        var method = typeof(ZeeuDashboardController).GetMethod("_CttProductionAdmin");

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>(inherit: true));
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Fact]
    public void ActionCenter_UpdateStatus_Is_Post_And_Uses_AntiForgery()
    {
        var method = typeof(ActionCenterController).GetMethod(nameof(ActionCenterController.UpdateStatus));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>(inherit: true));
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Theory]
    [InlineData(nameof(MemberController.SaveDashboardLayout))]
    [InlineData(nameof(MemberController.ResetDashboardLayout))]
    public void Dashboard_Layout_Changes_Are_Post_And_Use_AntiForgery(string methodName)
    {
        var method = typeof(MemberController).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>(inherit: true));
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }
}
