namespace CustomerContactCopilot.Models;

/// <summary>
/// Result of meter reading validation including plausibility check.
/// </summary>
public class MeterReadingValidation
{
    /// <summary>
    /// Whether the meter reading is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The meter reading value.
    /// </summary>
    public long Reading { get; set; }

    /// <summary>
    /// Calculated consumption since last reading.
    /// </summary>
    public long Consumption { get; set; }

    /// <summary>
    /// Expected consumption based on customer history.
    /// </summary>
    public long ExpectedConsumption { get; set; }

    /// <summary>
    /// Days since the last meter reading.
    /// </summary>
    public int DaysSinceLastReading { get; set; }

    /// <summary>
    /// Validation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Warning level: None, Info, Warning, Error.
    /// </summary>
    public ValidationLevel Level { get; set; } = ValidationLevel.None;

    /// <summary>
    /// Whether this is a plausibility warning requiring customer confirmation.
    /// </summary>
    public bool IsPlausibilityWarning { get; set; }

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static MeterReadingValidation Valid(long reading)
    {
        return new MeterReadingValidation
        {
            IsValid = true,
            Reading = reading,
            Level = ValidationLevel.None,
            Message = "Zählerstand erfolgreich validiert."
        };
    }

    /// <summary>
    /// Creates a warning result (reading is unusual but possible).
    /// </summary>
    public static MeterReadingValidation Warning(long reading, string message)
    {
        return new MeterReadingValidation
        {
            IsValid = true,
            Reading = reading,
            Level = ValidationLevel.Warning,
            Message = message
        };
    }

    /// <summary>
    /// Creates a plausibility warning result requiring customer confirmation.
    /// </summary>
    public static MeterReadingValidation PlausibilityWarning(
        long reading,
        long consumption,
        long expectedConsumption,
        int daysSinceLastReading,
        string message,
        ValidationLevel severity = ValidationLevel.Warning)
    {
        return new MeterReadingValidation
        {
            IsValid = true, // The reading itself is valid, just needs confirmation
            Reading = reading,
            Consumption = consumption,
            ExpectedConsumption = expectedConsumption,
            DaysSinceLastReading = daysSinceLastReading,
            Level = severity,
            Message = message,
            IsPlausibilityWarning = true
        };
    }

    /// <summary>
    /// Creates an error result (reading is invalid).
    /// </summary>
    public static MeterReadingValidation Error(long reading, string message)
    {
        return new MeterReadingValidation
        {
            IsValid = false,
            Reading = reading,
            Level = ValidationLevel.Error,
            Message = message
        };
    }
}

/// <summary>
/// Validation severity levels.
/// </summary>
public enum ValidationLevel
{
    None,
    Info,
    Warning,
    Error
}
