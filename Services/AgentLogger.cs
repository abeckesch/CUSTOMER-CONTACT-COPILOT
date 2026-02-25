using System.Text.Json;

namespace CustomerContactCopilot.Services;

/// <summary>
/// Logging service for observability and tracing agent decision-making.
/// Provides structured logging with trace correlation.
/// </summary>
public class AgentLogger
{
    private readonly string _logPath;
    private readonly List<LogEntry> _logBuffer = new();
    private readonly object _lock = new();
    private readonly bool _consoleOutput;

    public AgentLogger(string? logPath = null, bool consoleOutput = true)
    {
        _logPath = logPath ?? Path.Combine(AppContext.BaseDirectory, "Logs", "agent_decisions.jsonl");
        _consoleOutput = consoleOutput;
        EnsureLogDirectory();
    }

    /// <summary>
    /// Logs the start of email processing.
    /// </summary>
    public string StartProcessing(string emailId, string fromAddress)
    {
        var traceId = Guid.NewGuid().ToString("N")[..12];
        
        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Info,
            Category = "Processing",
            Event = "EmailProcessingStarted",
            Message = $"Started processing email {emailId}",
            Data = new Dictionary<string, object>
            {
                ["emailId"] = emailId,
                ["fromAddress"] = RedactEmail(fromAddress)
            }
        });

        return traceId;
    }

    /// <summary>
    /// Logs intent detection decision.
    /// </summary>
    public void LogIntentDetection(string traceId, string detectedIntent, string reasoning, double? confidence = null)
    {
        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Info,
            Category = "IntentDetection",
            Event = "IntentDetected",
            Message = $"Detected intent: {detectedIntent}",
            Data = new Dictionary<string, object>
            {
                ["intent"] = detectedIntent,
                ["reasoning"] = reasoning,
                ["confidence"] = confidence ?? 0.0,
                ["requiresAuth"] = IsAuthRequired(detectedIntent)
            }
        });
    }

    /// <summary>
    /// Logs entity extraction results.
    /// </summary>
    public void LogEntityExtraction(string traceId, Dictionary<string, string> entities)
    {
        // Redact PII in entity values for logging
        var redactedEntities = entities
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (object)RedactPiiField(kvp.Key, kvp.Value));

        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Info,
            Category = "EntityExtraction",
            Event = "EntitiesExtracted",
            Message = $"Extracted {entities.Count} entities",
            Data = new Dictionary<string, object>
            {
                ["entityCount"] = entities.Count,
                ["entityTypes"] = entities.Keys.ToList(),
                ["redactedEntities"] = redactedEntities
            }
        });
    }

    /// <summary>
    /// Logs authentication attempt and result.
    /// </summary>
    public void LogAuthenticationAttempt(
        string traceId, 
        bool success, 
        int providedFieldCount,
        int validatedFieldCount,
        List<string> validatedFields,
        List<string> missingFields,
        string? contractNumber = null)
    {
        string message;
        if (success)
        {
            message = $"Customer authenticated with {validatedFieldCount} fields";
        }
        else if (validatedFieldCount == 0 && missingFields != null && missingFields.Count > 0)
        {
            message = $"Authentication verification incomplete: {providedFieldCount} entities found, but key data missing ({string.Join(", ", missingFields.Take(3))})";
        }
        else
        {
            message = $"Authentication failed: {providedFieldCount} fields provided, {validatedFieldCount} validated";
        }

        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = success ? LogLevel.Info : LogLevel.Warning,
            Category = "Authentication",
            Event = success ? "AuthenticationSucceeded" : "AuthenticationFailed",
            Message = message,
            Data = new Dictionary<string, object>
            {
                ["success"] = success,
                ["providedFields"] = providedFieldCount,
                ["validatedFields"] = validatedFieldCount,
                ["validatedFieldNames"] = validatedFields,
                ["missingFields"] = missingFields,
                ["contractNumber"] = contractNumber != null ? RedactContractNumber(contractNumber) : "N/A",
                ["meetsThreshold"] = validatedFieldCount >= 3
            }
        });
    }

    /// <summary>
    /// Logs session state operations.
    /// </summary>
    public void LogSessionOperation(string traceId, string operation, string sessionId, bool isAuthenticated)
    {
        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Info,
            Category = "Session",
            Event = $"Session{operation}",
            Message = $"Session {operation.ToLower()}: {sessionId[..8]}...",
            Data = new Dictionary<string, object>
            {
                ["sessionId"] = sessionId[..8] + "...",
                ["operation"] = operation,
                ["isAuthenticated"] = isAuthenticated
            }
        });
    }

    /// <summary>
    /// Logs actions taken by the agent.
    /// </summary>
    public void LogAction(string traceId, string actionType, string details, bool requiresHumanReview = false)
    {
        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = requiresHumanReview ? LogLevel.Warning : LogLevel.Info,
            Category = "Action",
            Event = "ActionTaken",
            Message = $"Action: {actionType}",
            Data = new Dictionary<string, object>
            {
                ["actionType"] = actionType,
                ["details"] = details,
                ["requiresHumanReview"] = requiresHumanReview
            }
        });
    }

    /// <summary>
    /// Logs LLM interactions (with PII redaction).
    /// </summary>
    public void LogLlmInteraction(string traceId, string purpose, int promptTokens, int responseTokens)
    {
        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Debug,
            Category = "LLM",
            Event = "LlmInvocation",
            Message = $"LLM call for {purpose}",
            Data = new Dictionary<string, object>
            {
                ["purpose"] = purpose,
                ["promptTokens"] = promptTokens,
                ["responseTokens"] = responseTokens
            }
        });
    }

    /// <summary>
    /// Logs errors and exceptions.
    /// </summary>
    public void LogError(string traceId, string category, string message, Exception? exception = null)
    {
        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Error,
            Category = category,
            Event = "Error",
            Message = message,
            Data = new Dictionary<string, object>
            {
                ["exceptionType"] = exception?.GetType().Name ?? "N/A",
                ["exceptionMessage"] = exception?.Message ?? "N/A"
            }
        });
    }

    /// <summary>
    /// Logs processing completion.
    /// </summary>
    public void LogProcessingComplete(string traceId, string status, TimeSpan duration, List<string> actions)
    {
        Log(new LogEntry
        {
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Info,
            Category = "Processing",
            Event = "EmailProcessingCompleted",
            Message = $"Processing completed: {status}",
            Data = new Dictionary<string, object>
            {
                ["status"] = status,
                ["durationMs"] = duration.TotalMilliseconds,
                ["actionCount"] = actions.Count,
                ["actions"] = actions
            }
        });
    }

    /// <summary>
    /// Gets all logs for a specific trace.
    /// </summary>
    public IReadOnlyList<LogEntry> GetLogsForTrace(string traceId)
    {
        lock (_lock)
        {
            return _logBuffer.Where(l => l.TraceId == traceId).ToList().AsReadOnly();
        }
    }

    private void Log(LogEntry entry)
    {
        lock (_lock)
        {
            _logBuffer.Add(entry);
            
            // Keep buffer bounded
            if (_logBuffer.Count > 10000)
            {
                _logBuffer.RemoveRange(0, 1000);
            }
        }

        // Console output
        if (_consoleOutput)
        {
            var levelIcon = entry.Level switch
            {
                LogLevel.Debug => "🔍",
                LogLevel.Info => "ℹ️",
                LogLevel.Warning => "⚠️",
                LogLevel.Error => "❌",
                _ => "📝"
            };
            Console.WriteLine($"  {levelIcon} [{entry.TraceId}] {entry.Category}: {entry.Message}");
        }

        // Persist to file
        PersistLog(entry);
    }

    private void PersistLog(LogEntry entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry);
            File.AppendAllText(_logPath, json + Environment.NewLine);
        }
        catch
        {
            // Fail silently for logging
        }
    }

    private void EnsureLogDirectory()
    {
        var dir = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    // PII Redaction helpers
    private static string RedactEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "***@***.***";
        
        var parts = email.Split('@');
        var local = parts[0].Length > 2 
            ? parts[0][..2] + new string('*', parts[0].Length - 2) 
            : "***";
        return $"{local}@***";
    }

    private static string RedactContractNumber(string contract)
    {
        if (string.IsNullOrEmpty(contract) || contract.Length < 8)
            return "LB-****-*****";
        return $"{contract[..3]}****-*****";
    }

    private static string RedactPiiField(string fieldName, string value)
    {
        return fieldName.ToLowerInvariant() switch
        {
            "birthday" => "**.**.****.".Replace("*", "*"),
            "fullname" => value.Length > 2 ? value[..1] + "***" + value[^1..] : "***",
            "contractnumber" => RedactContractNumber(value),
            "street" => value.Length > 3 ? value[..3] + "***" : "***",
            "postalcode" => value.Length >= 2 ? value[..2] + "***" : "***",
            "iban" => "DE** **** **** ****",
            "email" => RedactEmail(value),
            _ => value
        };
    }

    private static bool IsAuthRequired(string intent)
    {
        var authRequiredIntents = new[] 
        { 
            "MeterReadingSubmission", "AddressChange", "PaymentChange", 
            "ContractTermination", "BillingInquiry", "TariffChange", "Complaint" 
        };
        return authRequiredIntents.Contains(intent);
    }
}

/// <summary>
/// Structured log entry.
/// </summary>
public class LogEntry
{
    public string TraceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Log severity levels.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
