namespace AgenticCustomerContactCopilot.Models;

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Whether the authentication was successful.
    /// </summary>
    public bool IsAuthenticated { get; set; }
    
    /// <summary>
    /// The authenticated customer data if successful.
    /// </summary>
    public CustomerData? Customer { get; set; }
    
    /// <summary>
    /// Data points that were successfully validated.
    /// </summary>
    public List<string> ValidatedFields { get; set; } = new();
    
    /// <summary>
    /// Data points that are missing and required for authentication.
    /// </summary>
    public List<string> MissingFields { get; set; } = new();
    
    /// <summary>
    /// Data points that were provided but didn't match.
    /// </summary>
    public List<string> MismatchedFields { get; set; } = new();
    
    /// <summary>
    /// Number of additional data points needed for successful authentication.
    /// </summary>
    public int PointsNeeded { get; set; }
    
    /// <summary>
    /// Human-readable message about the authentication result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    public static AuthenticationResult Success(CustomerData customer, List<string> validatedFields)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = true,
            Customer = customer,
            ValidatedFields = validatedFields,
            Message = "Authentifizierung erfolgreich."
        };
    }
    
    /// <summary>
    /// Creates a failed authentication result due to missing data.
    /// </summary>
    public static AuthenticationResult MissingData(List<string> missingFields, int pointsNeeded, List<string>? validatedFields = null)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = false,
            MissingFields = missingFields,
            PointsNeeded = pointsNeeded,
            ValidatedFields = validatedFields ?? new List<string>(),
            Message = $"Zur Authentifizierung fehlen noch {pointsNeeded} der folgenden Angaben: {string.Join(", ", missingFields)}"
        };
    }
    
    /// <summary>
    /// Creates a failed authentication result due to mismatched data.
    /// </summary>
    public static AuthenticationResult DataMismatch(List<string> mismatchedFields)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = false,
            MismatchedFields = mismatchedFields,
            Message = "Die angegebenen Daten stimmen nicht mit unseren Unterlagen überein."
        };
    }
    
    /// <summary>
    /// Creates a failed authentication result when customer is not found.
    /// </summary>
    public static AuthenticationResult CustomerNotFound()
    {
        return new AuthenticationResult
        {
            IsAuthenticated = false,
            Message = "Es konnte kein Kundenkonto mit den angegebenen Daten gefunden werden."
        };
    }
}
