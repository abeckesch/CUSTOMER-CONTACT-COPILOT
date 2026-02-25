using System.Text.RegularExpressions;

namespace CustomerContactCopilot.Services;

/// <summary>
/// Service for detecting and redacting Personally Identifiable Information (PII)
/// before sending content to LLMs. Implements data protection best practices.
/// </summary>
public class PiiRedactionService
{
    // PII detection patterns
    private static readonly Dictionary<string, Regex> PiiPatterns = new()
    {
        // German date format (likely birthday)
        ["birthday"] = new Regex(@"\b(\d{1,2})\.(\d{1,2})\.(\d{4})\b", RegexOptions.Compiled),
        
        // EnergyCo contract number
        ["contractNumber"] = new Regex(@"LB-\d{4}-\d{5}", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // German IBAN
        ["iban"] = new Regex(@"[A-Z]{2}\d{2}\s?\d{4}\s?\d{4}\s?\d{4}\s?\d{4}\s?\d{0,2}", RegexOptions.Compiled),
        
        // Email addresses
        ["email"] = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled),
        
        // German phone numbers
        ["phone"] = new Regex(@"(\+49|0049|0)\s?[1-9]\d{1,4}[\s/-]?\d{3,10}", RegexOptions.Compiled),
        
        // German postal codes (5 digits)
        ["postalCode"] = new Regex(@"\b\d{5}\b", RegexOptions.Compiled),
        
        // Credit card numbers
        ["creditCard"] = new Regex(@"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", RegexOptions.Compiled),
        
        // Meter reading numbers (for context, not strictly PII but can be sensitive)
        ["meterNumber"] = new Regex(@"Zähler(?:nummer|nr\.?)\s*:?\s*\d{8,12}", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    // Placeholders for redacted content
    private static readonly Dictionary<string, string> RedactionPlaceholders = new()
    {
        ["birthday"] = "[GEBURTSDATUM]",
        ["contractNumber"] = "[VERTRAGSNUMMER]",
        ["iban"] = "[IBAN]",
        ["email"] = "[EMAIL]",
        ["phone"] = "[TELEFONNUMMER]",
        ["postalCode"] = "[PLZ]",
        ["creditCard"] = "[KREDITKARTE]",
        ["meterNumber"] = "[ZÄHLERNUMMER]"
    };

    /// <summary>
    /// Redacts all detected PII from text before sending to LLM.
    /// Returns the redacted text and a mapping of placeholders to original values.
    /// </summary>
    public RedactionResult RedactForLlm(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new RedactionResult { RedactedText = text };
        }

        var result = new RedactionResult
        {
            OriginalText = text,
            RedactedText = text
        };

        foreach (var (piiType, pattern) in PiiPatterns)
        {
            var placeholder = RedactionPlaceholders[piiType];
            var matches = pattern.Matches(result.RedactedText);

            foreach (Match match in matches.Reverse()) // Reverse to maintain indices
            {
                var originalValue = match.Value;
                var indexedPlaceholder = $"{placeholder}_{result.RedactedItems.Count}";
                
                result.RedactedItems.Add(new RedactedItem
                {
                    Type = piiType,
                    OriginalValue = originalValue,
                    Placeholder = indexedPlaceholder,
                    StartIndex = match.Index,
                    Length = match.Length
                });

                result.RedactedText = result.RedactedText.Remove(match.Index, match.Length)
                                                         .Insert(match.Index, indexedPlaceholder);
            }
        }

        result.HasPii = result.RedactedItems.Count > 0;
        return result;
    }

    /// <summary>
    /// Restores original values in a response from the LLM.
    /// </summary>
    public string RestoreOriginalValues(string llmResponse, RedactionResult redactionResult)
    {
        if (string.IsNullOrEmpty(llmResponse) || !redactionResult.HasPii)
        {
            return llmResponse;
        }

        var restored = llmResponse;
        
        // Restore each redacted item
        foreach (var item in redactionResult.RedactedItems)
        {
            restored = restored.Replace(item.Placeholder, item.OriginalValue);
        }

        return restored;
    }

    /// <summary>
    /// Redacts PII for entity extraction prompt.
    /// Uses consistent placeholders so the LLM can still identify field types.
    /// </summary>
    public string RedactForEntityExtraction(string text)
    {
        var result = text;

        // For entity extraction, we use descriptive placeholders
        // that help the LLM understand what type of data was there
        result = PiiPatterns["birthday"].Replace(result, "[DATUM_TT.MM.JJJJ]");
        result = PiiPatterns["contractNumber"].Replace(result, "[VERTRAG_LB-XXXX-XXXXX]");
        result = PiiPatterns["iban"].Replace(result, "[IBAN_DEXX_XXXX_XXXX]");
        result = PiiPatterns["email"].Replace(result, "[EMAIL_***@***.de]");
        result = PiiPatterns["phone"].Replace(result, "[TELEFON_+49_XXX]");

        return result;
    }

    /// <summary>
    /// Creates a PII-safe summary of extracted entities for logging.
    /// </summary>
    public Dictionary<string, string> CreateSafeEntitySummary(Dictionary<string, string> entities)
    {
        var safe = new Dictionary<string, string>();

        foreach (var (key, value) in entities)
        {
            safe[key] = key.ToLowerInvariant() switch
            {
                "birthday" => "[REDACTED_DATE]",
                "fullname" => MaskName(value),
                "contractnumber" => MaskContractNumber(value),
                "street" => MaskStreet(value),
                "postalcode" => MaskPostalCode(value),
                "iban" => "[REDACTED_IBAN]",
                "email" => MaskEmail(value),
                "phone" => "[REDACTED_PHONE]",
                _ => value // Keep non-PII values
            };
        }

        return safe;
    }

    /// <summary>
    /// Validates that sensitive data handling meets security requirements.
    /// </summary>
    public SecurityValidation ValidateSecurityCompliance(string textBeforeLlm)
    {
        var validation = new SecurityValidation();

        foreach (var (piiType, pattern) in PiiPatterns)
        {
            if (pattern.IsMatch(textBeforeLlm))
            {
                validation.HasViolations = true;
                validation.Violations.Add(new SecurityViolation
                {
                    Type = piiType,
                    Message = $"Unredacted {piiType} detected in LLM input",
                    Severity = GetSeverity(piiType)
                });
            }
        }

        return validation;
    }

    // Masking helpers
    private static string MaskName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 3)
            return "***";
        return $"{name[0]}*** {name.Split(' ').LastOrDefault()?[0] ?? '*'}***";
    }

    private static string MaskContractNumber(string contract)
    {
        if (string.IsNullOrEmpty(contract) || contract.Length < 8)
            return "LB-****-*****";
        return $"LB-****-{contract[^5..]}";
    }

    private static string MaskStreet(string street)
    {
        if (string.IsNullOrEmpty(street) || street.Length < 5)
            return "*** ****";
        return $"{street[..3]}*** ***";
    }

    private static string MaskPostalCode(string plz)
    {
        if (string.IsNullOrEmpty(plz) || plz.Length < 5)
            return "*****";
        return $"{plz[..2]}***";
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "***@***.***";
        var parts = email.Split('@');
        return $"{parts[0][0]}***@***";
    }

    private static ViolationSeverity GetSeverity(string piiType)
    {
        return piiType switch
        {
            "iban" or "creditCard" => ViolationSeverity.Critical,
            "birthday" or "contractNumber" => ViolationSeverity.High,
            "email" or "phone" => ViolationSeverity.Medium,
            _ => ViolationSeverity.Low
        };
    }
}

/// <summary>
/// Result of PII redaction operation.
/// </summary>
public class RedactionResult
{
    public string OriginalText { get; set; } = string.Empty;
    public string RedactedText { get; set; } = string.Empty;
    public bool HasPii { get; set; }
    public List<RedactedItem> RedactedItems { get; set; } = new();
}

/// <summary>
/// A single redacted PII item.
/// </summary>
public class RedactedItem
{
    public string Type { get; set; } = string.Empty;
    public string OriginalValue { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int Length { get; set; }
}

/// <summary>
/// Security validation result.
/// </summary>
public class SecurityValidation
{
    public bool HasViolations { get; set; }
    public List<SecurityViolation> Violations { get; set; } = new();
}

/// <summary>
/// A security violation.
/// </summary>
public class SecurityViolation
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; }
}

/// <summary>
/// Severity levels for security violations.
/// </summary>
public enum ViolationSeverity
{
    Low,
    Medium,
    High,
    Critical
}
