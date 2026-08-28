using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using WebApp.Controllers;
using Xunit;

namespace WebApp.Tests;

public sealed class ApprovalChainsRoutingTests
{
    [Fact]
    public void Admin_ApprovalChains_Redirects_To_WebApproval_AttestChains()
    {
        var controller = CreateUninitialized<AdminController>();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = controller.ApprovalChains();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("AttestChains", redirect.ActionName);
        Assert.Equal("WebApproval", redirect.ControllerName);
    }

    private static T CreateUninitialized<T>() where T : class
        => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
