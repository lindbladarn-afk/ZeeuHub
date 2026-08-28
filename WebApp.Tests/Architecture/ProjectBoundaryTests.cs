// Protects project dependency direction between shared models, persistence and the web application.
using Entities.Application;
using Repository;

namespace WebApp.Tests.Architecture;

public sealed class ProjectBoundaryTests
{
    [Fact]
    public void Entities_DoesNotReferencePersistenceOrPresentationProjects()
    {
        var references = GetReferenceNames(typeof(Company).Assembly);

        Assert.DoesNotContain("Repository", references);
        Assert.DoesNotContain("WebApp", references);
    }

    [Fact]
    public void Repository_DependsOnEntitiesButNotWebApp()
    {
        var references = GetReferenceNames(typeof(ApplicationRepository).Assembly);

        Assert.Contains("Entities", references);
        Assert.DoesNotContain("WebApp", references);
    }

    private static HashSet<string> GetReferenceNames(System.Reflection.Assembly assembly)
        => assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
}
