namespace CustomerContactCopilot.Models;

/// <summary>
/// Represents a conversation session with a customer, tracking state across multiple emails.
/// </summary>
public class ConversationSession
{
    /// <summary>
    /// Unique identifier for the session.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Email address that identifies this conversation thread.
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Whether the customer has been authenticated in this session.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// The authenticated customer's contract number.
    /// </summary>
    public string? AuthenticatedContractNumber { get; set; }

    /// <summary>
    /// The authenticated customer's full name.
    /// </summary>
    public string? AuthenticatedCustomerName { get; set; }

    /// <summary>
    /// Timestamp when authentication occurred.
    /// </summary>
    public DateTime? AuthenticatedAt { get; set; }

    /// <summary>
    /// Data points collected across conversation turns for authentication.
    /// </summary>
    public Dictionary<string, string> CollectedAuthData { get; set; } = new();

    /// <summary>
    /// History of processed emails in this session.
    /// </summary>
    public List<EmailHistoryEntry> EmailHistory { get; set; } = new();

    /// <summary>
    /// The most recent processing state.
    /// </summary>
    public ProcessingState? LastProcessingState { get; set; }

    /// <summary>
    /// Pending actions that require follow-up (e.g., meter reading confirmation).
    /// </summary>
    public List<PendingAction> PendingActions { get; set; } = new();

    /// <summary>
    /// Session creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp.
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session expiration time (default: 24 hours of inactivity).
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Checks if the session has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow - LastActivityAt > SessionTimeout;

    /// <summary>
    /// Updates the last activity timestamp.
    /// </summary>
    public void Touch()
    {
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Merges new authentication data with existing collected data.
    /// Preserves more complete values (e.g., won't overwrite "Julia Meyer" with "J. Meyer").
    /// </summary>
    public void MergeAuthData(Dictionary<string, string> newData)
    {
        foreach (var kvp in newData)
        {
            if (string.IsNullOrWhiteSpace(kvp.Value))
                continue;
                
            // Check if we should preserve the existing value
            if (CollectedAuthData.TryGetValue(kvp.Key, out var existingValue))
            {
                // For fullName: preserve the longer/more complete version
                if (kvp.Key == "fullName")
                {
                    if (IsAbbreviatedVersion(kvp.Value, existingValue))
                    {
                        // New value is abbreviated version of existing - keep existing
                        continue;
                    }
                }
                
                // For other fields: keep longer non-empty values
                if (existingValue.Length > kvp.Value.Length && 
                    !string.IsNullOrWhiteSpace(existingValue))
                {
                    // Only overwrite if new value is more specific (same root)
                    if (!kvp.Value.Contains(existingValue.Split(' ')[0]))
                    {
                        continue;
                    }
                }
            }
            
            CollectedAuthData[kvp.Key] = kvp.Value;
        }
        Touch();
    }
    
    /// <summary>
    /// Checks if 'candidate' is an abbreviated version of 'full'.
    /// E.g., "J. Meyer" is abbreviated version of "Julia Meyer"
    /// </summary>
    private static bool IsAbbreviatedVersion(string candidate, string full)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(full))
            return false;
            
        var candidateParts = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fullParts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (candidateParts.Length == 0 || fullParts.Length == 0)
            return false;
            
        // Same length comparison: "J. Meyer" vs "Julia Meyer"  
        if (candidateParts.Length == fullParts.Length)
        {
            bool allMatch = true;
            for (int i = 0; i < candidateParts.Length; i++)
            {
                var cp = candidateParts[i].TrimEnd('.');
                var fp = fullParts[i];
                
                // Check if candidate part is initial of full part
                if (cp.Length == 1 && fp.StartsWith(cp, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                // Check if exact match
                if (cp.Equals(fp, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                allMatch = false;
                break;
            }
            
            // If all parts match but candidate is shorter overall, it's abbreviated
            if (allMatch && candidate.Length < full.Length)
                return true;
        }
        
        // Single name vs full name: "Meyer" could be part of "Julia Meyer"
        if (candidateParts.Length == 1 && fullParts.Length > 1)
        {
            return fullParts.Any(fp => fp.Equals(candidate, StringComparison.OrdinalIgnoreCase));
        }
        
        return false;
    }

    /// <summary>
    /// Sets the session as authenticated.
    /// </summary>
    public void SetAuthenticated(CustomerData customer)
    {
        IsAuthenticated = true;
        AuthenticatedContractNumber = customer.ContractNumber;
        AuthenticatedCustomerName = customer.FullName;
        AuthenticatedAt = DateTime.UtcNow;
        Touch();
    }

    /// <summary>
    /// Adds an email to the history.
    /// </summary>
    public void AddEmailToHistory(CustomerEmail email, ProcessingState state)
    {
        EmailHistory.Add(new EmailHistoryEntry
        {
            EmailId = email.Id,
            Subject = email.Subject,
            ReceivedAt = email.ReceivedAt,
            ProcessedAt = DateTime.UtcNow,
            DetectedIntent = state.DetectedIntent,
            WasSuccessful = state.CurrentStep == ProcessingStep.Completed
        });
        LastProcessingState = state;
        Touch();
    }
}

/// <summary>
/// Represents an entry in the email history.
/// </summary>
public class EmailHistoryEntry
{
    public string EmailId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? DetectedIntent { get; set; }
    public bool WasSuccessful { get; set; }
}

/// <summary>
/// Represents a pending action awaiting customer response.
/// </summary>
public class PendingAction
{
    /// <summary>
    /// Unique identifier for the pending action.
    /// </summary>
    public string ActionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of action (e.g., "meter_reading_confirmation", "provide_auth_data").
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Email ID that triggered this pending action.
    /// </summary>
    public string SourceEmailId { get; set; } = string.Empty;

    /// <summary>
    /// Data associated with the pending action.
    /// </summary>
    public Dictionary<string, string> ActionData { get; set; } = new();

    /// <summary>
    /// When the action was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this action has been resolved.
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// When the action was resolved.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
}
