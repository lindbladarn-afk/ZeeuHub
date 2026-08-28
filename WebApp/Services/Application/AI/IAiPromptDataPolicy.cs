// Defines which SQL result values may be sent to the language model.
namespace WebApp.Services.Application.AI;

public interface IAiPromptDataPolicy
{
    string FormatCell(string columnName, object? value);
}
