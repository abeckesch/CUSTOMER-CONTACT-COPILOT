using System.Text.Json;
using CustomerContactCopilot.Models;

namespace CustomerContactCopilot.Services;

/// <summary>
/// Manages persistent conversation sessions across multiple email interactions.
/// Sessions are correlated by customer email address and persisted to disk.
/// </summary>
public class SessionStateManager
{
    private readonly string _persistencePath;
    private readonly Dictionary<string, ConversationSession> _sessions;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _lock = new();

    public SessionStateManager(string? persistencePath = null)
    {
        _persistencePath = persistencePath ?? Path.Combine(AppContext.BaseDirectory, "Data", "sessions.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _sessions = LoadSessions();
    }

    /// <summary>
    /// Gets or creates a session for the given email address.
    /// </summary>
    public ConversationSession GetOrCreateSession(string customerEmail)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            throw new ArgumentException("Customer email cannot be empty", nameof(customerEmail));
        }

        var normalizedEmail = customerEmail.ToLowerInvariant().Trim();

        lock (_lock)
        {
            // Look for existing session by email
            if (_sessions.TryGetValue(normalizedEmail, out var existingSession))
            {
                if (!existingSession.IsExpired)
                {
                    existingSession.Touch();
                    return existingSession;
                }
                else
                {
                    // Session expired, remove it
                    _sessions.Remove(normalizedEmail);
                }
            }

            // Create new session
            var newSession = new ConversationSession
            {
                CustomerEmail = normalizedEmail
            };
            _sessions[normalizedEmail] = newSession;
            SaveSessions();
            return newSession;
        }
    }

    /// <summary>
    /// Gets an existing session by email address, or null if not found or expired.
    /// </summary>
    public ConversationSession? GetSession(string customerEmail)
    {
        var normalizedEmail = customerEmail.ToLowerInvariant().Trim();

        lock (_lock)
        {
            if (_sessions.TryGetValue(normalizedEmail, out var session))
            {
                if (!session.IsExpired)
                {
                    return session;
                }
                else
                {
                    // Clean up expired session
                    _sessions.Remove(normalizedEmail);
                    SaveSessions();
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Gets an existing session by contract number.
    /// </summary>
    public ConversationSession? GetSessionByContract(string contractNumber)
    {
        lock (_lock)
        {
            return _sessions.Values.FirstOrDefault(s => 
                !s.IsExpired && 
                s.AuthenticatedContractNumber?.Equals(contractNumber, StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    /// <summary>
    /// Updates a session and persists changes.
    /// </summary>
    public void UpdateSession(ConversationSession session)
    {
        lock (_lock)
        {
            session.Touch();
            _sessions[session.CustomerEmail] = session;
            SaveSessions();
        }
    }

    /// <summary>
    /// Adds a pending action to a session.
    /// </summary>
    public void AddPendingAction(ConversationSession session, string actionType, string sourceEmailId, Dictionary<string, string>? actionData = null)
    {
        var action = new PendingAction
        {
            ActionType = actionType,
            SourceEmailId = sourceEmailId,
            ActionData = actionData ?? new Dictionary<string, string>()
        };

        lock (_lock)
        {
            session.PendingActions.Add(action);
            UpdateSession(session);
        }
    }

    /// <summary>
    /// Resolves a pending action.
    /// </summary>
    public void ResolvePendingAction(ConversationSession session, string actionType)
    {
        lock (_lock)
        {
            var action = session.PendingActions.FirstOrDefault(a => 
                a.ActionType == actionType && !a.IsResolved);
            
            if (action != null)
            {
                action.IsResolved = true;
                action.ResolvedAt = DateTime.UtcNow;
                UpdateSession(session);
            }
        }
    }

    /// <summary>
    /// Gets unresolved pending actions for a session.
    /// </summary>
    public List<PendingAction> GetPendingActions(ConversationSession session)
    {
        return session.PendingActions.Where(a => !a.IsResolved).ToList();
    }

    /// <summary>
    /// Cleans up expired sessions.
    /// </summary>
    public int CleanupExpiredSessions()
    {
        lock (_lock)
        {
            var expiredKeys = _sessions
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _sessions.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                SaveSessions();
            }

            return expiredKeys.Count;
        }
    }

    /// <summary>
    /// Gets all active (non-expired) sessions.
    /// </summary>
    public IReadOnlyList<ConversationSession> GetActiveSessions()
    {
        lock (_lock)
        {
            return _sessions.Values.Where(s => !s.IsExpired).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Loads sessions from the persistence file.
    /// </summary>
    private Dictionary<string, ConversationSession> LoadSessions()
    {
        try
        {
            if (File.Exists(_persistencePath))
            {
                var json = File.ReadAllText(_persistencePath);
                var sessions = JsonSerializer.Deserialize<Dictionary<string, ConversationSession>>(json, _jsonOptions);
                return sessions ?? new Dictionary<string, ConversationSession>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not load sessions: {ex.Message}");
        }
        return new Dictionary<string, ConversationSession>();
    }

    /// <summary>
    /// Saves sessions to the persistence file.
    /// </summary>
    private void SaveSessions()
    {
        try
        {
            var directory = Path.GetDirectoryName(_persistencePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_sessions, _jsonOptions);
            File.WriteAllText(_persistencePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not save sessions: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all sessions (useful for testing).
    /// </summary>
    public void ClearAllSessions()
    {
        lock (_lock)
        {
            _sessions.Clear();
            SaveSessions();
        }
    }
}
