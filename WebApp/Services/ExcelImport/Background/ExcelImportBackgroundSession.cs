using Entities.Application;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace WebApp.Services.ExcelImport;

// Provides the existing import services with the same session values they use during a web request.
internal sealed class ExcelImportBackgroundSession : ISession
{
    private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

    public ExcelImportBackgroundSession(UserSession userSession)
    {
        _values["UserObject"] = JsonSerializer.SerializeToUtf8Bytes(userSession);
    }

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public IEnumerable<string> Keys => _values.Keys;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Clear() => _values.Clear();
    public void Remove(string key) => _values.Remove(key);
    public void Set(string key, byte[] value) => _values[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
}
