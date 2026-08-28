namespace WebApp.Services.Application;

// Resolves the default Jeeves connection while preserving existing legacy fallbacks.
public interface IJeevesConnectionResolver
{
    string ResolveConnectionString();
}
