using System.Reflection;
using System.Net;
using AspNetCoreHero.ToastNotification.Abstractions;
using NotificationService;
using Xunit;

namespace WebApp.Tests;

// Keeps hub toast payloads aligned with the shared top-right toast design.
public class NotificationManagerTests
{
    [Fact]
    public async Task Success_UsesDarkHubToastStyle()
    {
        var recorder = NotyfRecorder.Create();
        var manager = new NotificationManager(recorder.Service);

        await manager.Success("Ordern uppdaterades.");

        Assert.Equal("Custom", recorder.LastMethodName);
        Assert.Equal(4, recorder.LastArguments.Length);
        Assert.Equal(6, recorder.LastArguments[1]);
        Assert.Equal("#0f172a", recorder.LastArguments[2] as string);
        Assert.Equal("fas fa-check", recorder.LastArguments[3] as string);

        var message = Assert.IsType<string>(recorder.LastArguments[0]);
        Assert.Contains("hub-success-toast", message);
        Assert.Contains("Klart", message);
        Assert.Contains(WebUtility.HtmlEncode("Ordern uppdaterades."), message);
    }

    [Fact]
    public async Task HubStatus_UsesDarkHubToastStyle()
    {
        var recorder = NotyfRecorder.Create();
        var manager = new NotificationManager(recorder.Service);

        await manager.HubStatus("Demodata för inköp är aktiverad.");

        Assert.Equal("Custom", recorder.LastMethodName);
        Assert.Equal(4, recorder.LastArguments.Length);
        Assert.Equal(10, recorder.LastArguments[1]);
        Assert.Equal("#0f172a", recorder.LastArguments[2] as string);
        Assert.Equal("fas fa-check", recorder.LastArguments[3] as string);

        var message = Assert.IsType<string>(recorder.LastArguments[0]);
        Assert.Contains("hub-status-toast", message);
        Assert.Contains(WebUtility.HtmlEncode("Demodata för inköp är aktiverad."), message);
    }

    [Fact]
    public async Task TemporaryPassword_UsesDarkHubToastStyle()
    {
        var recorder = NotyfRecorder.Create();
        var manager = new NotificationManager(recorder.Service);

        await manager.TemporaryPassword("user@example.com", "Temp123!");

        Assert.Equal("Custom", recorder.LastMethodName);
        Assert.Equal(4, recorder.LastArguments.Length);
        Assert.Equal(18, recorder.LastArguments[1]);
        Assert.Equal("#0f172a", recorder.LastArguments[2] as string);
        Assert.Equal("fas fa-key", recorder.LastArguments[3] as string);

        var message = Assert.IsType<string>(recorder.LastArguments[0]);
        Assert.Contains("hub-password-toast", message);
        Assert.Contains("user@example.com", message);
        Assert.Contains("Temp123!", message);
    }

    public class NotyfRecorder : DispatchProxy
    {
        public INotyfService Service { get; private set; } = default!;
        public string? LastMethodName { get; private set; }
        public object?[] LastArguments { get; private set; } = [];

        public static NotyfRecorder Create()
        {
            var proxy = DispatchProxy.Create<INotyfService, NotyfRecorder>();
            var recorder = (NotyfRecorder)(object)proxy;
            recorder.Service = proxy;
            return recorder;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            LastMethodName = targetMethod?.Name;
            LastArguments = args ?? [];
            return targetMethod?.ReturnType == typeof(void)
                ? null
                : targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null;
        }
    }
}
