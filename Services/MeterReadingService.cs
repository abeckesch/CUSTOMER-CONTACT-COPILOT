using CustomerContactCopilot.Models;

namespace CustomerContactCopilot.Services;

/// <summary>
/// Service for validating meter readings and checking plausibility.
/// </summary>
public class MeterReadingService
{
    // Reasonable bounds for meter readings (in kWh)
    private const long MinReasonableReading = 0;
    private const long MaxReasonableReading = 999999; // ~1 million kWh is very high for residential
    
    // Plausibility thresholds
    private const double HighConsumptionThreshold = 1.5; // 150% of expected
    private const double VeryHighConsumptionThreshold = 2.0; // 200% of expected

    /// <summary>
    /// Validates a meter reading value (basic validation without customer context).
    /// </summary>
    public MeterReadingValidation ValidateReading(string readingString, long? previousReading = null)
    {
        // Clean and normalize the input
        var cleanedReading = readingString
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("kWh", "", StringComparison.OrdinalIgnoreCase)
            .Replace("kwh", "", StringComparison.OrdinalIgnoreCase)
            .Replace("KWH", "")
            .Trim();
        
        // Try to parse the reading
        if (!long.TryParse(cleanedReading, out var reading))
        {
            return MeterReadingValidation.Error(0, 
                "The meter reading could not be recognized as a number. Please check your input.");
        }

        // Check for negative values
        if (reading < MinReasonableReading)
        {
            return MeterReadingValidation.Error(reading, 
                "A negative meter reading is not possible. Please check your input.");
        }

        // Check for unrealistically high values
        if (reading > MaxReasonableReading)
        {
            return MeterReadingValidation.Error(reading,
                $"The provided meter reading of {reading:N0} kWh appears unrealistically high. " +
                "Please verify the value or contact us for a meter inspection.");
        }

        // If we have a previous reading, check the delta
        if (previousReading.HasValue)
        {
            var delta = reading - previousReading.Value;

            // Reading should not decrease (unless meter was replaced)
            if (delta < 0)
            {
                return MeterReadingValidation.Warning(reading,
                    $"The new meter reading ({reading:N0} kWh) is lower than the previous one " +
                    $"({previousReading.Value:N0} kWh). If your meter was replaced, please let us know.");
            }
        }

        // Check for suspicious round numbers or repeated digits
        if (IsSuspiciousPattern(reading))
        {
            return MeterReadingValidation.Warning(reading,
                $"The meter reading {reading:N0} kWh contains a suspicious pattern. " +
                "Please verify that you have read the correct value.");
        }

        return MeterReadingValidation.Valid(reading);
    }

    /// <summary>
    /// Validates a meter reading with plausibility check against customer's expected consumption.
    /// </summary>
    /// <param name="readingString">The new meter reading as string.</param>
    /// <param name="customer">Customer data with consumption history.</param>
    /// <param name="readingDate">Date of the new reading (optional).</param>
    /// <returns>Validation result with plausibility information.</returns>
    public MeterReadingValidation ValidateWithPlausibility(
        string readingString, 
        CustomerData customer,
        DateTime? readingDate = null)
    {
        // First do basic validation
        var basicValidation = ValidateReading(readingString, customer.LastMeterReading);
        
        // If basic validation failed or has errors, return it
        if (!basicValidation.IsValid)
        {
            return basicValidation;
        }

        var reading = basicValidation.Reading;
        var currentDate = readingDate ?? DateTime.Now;
        
        // Calculate consumption since last reading
        var consumption = reading - customer.LastMeterReading;
        
        if (consumption < 0)
        {
            // Already handled in basic validation, but double-check
            return MeterReadingValidation.Warning(reading,
                $"The new meter reading ({reading:N0} kWh) is lower than your last reading " +
                $"({customer.LastMeterReading:N0} kWh from {customer.LastMeterReadingDate:dd.MM.yyyy}). " +
                "Was your meter replaced?");
        }

        // Calculate days since last reading
        var daysSinceLastReading = (currentDate - customer.LastMeterReadingDate).Days;
        if (daysSinceLastReading <= 0) daysSinceLastReading = 1; // Prevent division by zero

        // Calculate expected consumption for this period
        var dailyExpectedConsumption = customer.ExpectedYearlyConsumption / 365.0;
        var expectedConsumption = dailyExpectedConsumption * daysSinceLastReading;

        // Check plausibility
        var consumptionRatio = consumption / Math.Max(expectedConsumption, 1);

        if (consumptionRatio >= VeryHighConsumptionThreshold)
        {
            // Very high consumption - definitely needs attention
            return MeterReadingValidation.PlausibilityWarning(
                reading: reading,
                consumption: consumption,
                expectedConsumption: (long)expectedConsumption,
                daysSinceLastReading: daysSinceLastReading,
                message: $"The reported consumption of {consumption:N0} kWh since {customer.LastMeterReadingDate:dd.MM.yyyy} " +
                        $"is significantly higher than expected (approx. {expectedConsumption:N0} kWh). " +
                        "Please confirm the meter reading or let us know if your consumption behavior has changed " +
                        "(e.g. new electrical appliances, heat pump, more people in household).",
                severity: ValidationLevel.Warning
            );
        }
        else if (consumptionRatio >= HighConsumptionThreshold)
        {
            // High but not extreme - flag for review but still accept
            return MeterReadingValidation.PlausibilityWarning(
                reading: reading,
                consumption: consumption,
                expectedConsumption: (long)expectedConsumption,
                daysSinceLastReading: daysSinceLastReading,
                message: $"Your consumption of {consumption:N0} kWh is above the expected value " +
                        $"(approx. {expectedConsumption:N0} kWh). If your consumption behavior has changed, " +
                        "you can ignore this.",
                severity: ValidationLevel.Info
            );
        }

        // Consumption is within expected range
        var result = MeterReadingValidation.Valid(reading);
        result.Consumption = consumption;
        result.ExpectedConsumption = (long)expectedConsumption;
        result.DaysSinceLastReading = daysSinceLastReading;
        return result;
    }

    /// <summary>
    /// Checks for suspicious patterns like repeated digits.
    /// </summary>
    private bool IsSuspiciousPattern(long reading)
    {
        var str = reading.ToString();
        
        // All same digit (e.g., 11111, 99999999)
        if (str.Length >= 5 && str.Distinct().Count() == 1)
            return true;

        // Sequential digits
        if (str == "12345678" || str == "123456" || str == "1234567")
            return true;

        return false;
    }

    /// <summary>
    /// Parses a meter reading from email content.
    /// </summary>
    public (bool Found, string Value) ExtractMeterReading(string content)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            content, 
            @"(?:Zählerstand|Stand|Aktueller)\s*:?\s*(\d{4,8})\s*(?:kWh)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return (true, match.Groups[1].Value);
        }

        return (false, string.Empty);
    }

    /// <summary>
    /// Extracts a reading date from email content.
    /// </summary>
    public (bool Found, DateTime Date) ExtractReadingDate(string content)
    {
        // Look for "Ablesedatum:" or similar patterns
        var match = System.Text.RegularExpressions.Regex.Match(
            content,
            @"(?:Ablesedatum|Datum|vom|am)\s*:?\s*(\d{1,2})\.(\d{1,2})\.(\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            if (DateTime.TryParse($"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}", out var date))
            {
                return (true, date);
            }
        }

        return (false, DateTime.MinValue);
    }
}
