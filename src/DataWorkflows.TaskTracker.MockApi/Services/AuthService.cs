namespace DataWorkflows.TaskTracker.MockApi.Services;

/// <summary>
/// Mock authentication service with simple token generation.
/// </summary>
public class AuthService
{
    private readonly HashSet<string> _validTokens = new();

    /// <summary>
    /// Mock login - accepts any username/password and generates a token.
    /// </summary>
    public string? Login(string username, string password)
    {
        // For MVP, accept any non-empty credentials
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        // Generate a simple mock token (not cryptographically secure - MVP only!)
        var token = $"mock-token-{Guid.NewGuid()}";
        _validTokens.Add(token);
        return token;
    }

    /// <summary>
    /// Validate that a token exists in our set of valid tokens.
    /// </summary>
    public bool ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        return _validTokens.Contains(token);
    }

    /// <summary>
    /// Remove token (logout).
    /// </summary>
    public void RevokeToken(string token)
    {
        _validTokens.Remove(token);
    }
}
