namespace AzureDevOpsMcp.Shared.Helpers;

public static class ErrorHandler
{
    public static async Task<T> ExecuteWithErrorHandling<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" | Inner Error: {ex.InnerException.Message}";
            }
            
            // If T is string, return the error message
            if (typeof(T) == typeof(string))
            {
                return (T)(object)errorMessage;
            }
            
            // Otherwise, rethrow so MCP can handle it
            throw;
        }
    }
    
    public static async Task<string> ExecuteWithErrorHandling(Func<Task<string>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" | Inner Error: {ex.InnerException.Message}";
            }
            return errorMessage;
        }
    }
}
