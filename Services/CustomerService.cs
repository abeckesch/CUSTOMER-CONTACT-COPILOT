using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticCustomerContactCopilot.Models;

namespace AgenticCustomerContactCopilot.Services;

/// <summary>
/// Service for managing customer data and authentication.
/// </summary>
public class CustomerService
{
    private readonly List<CustomerData> _customers;
    private readonly JsonSerializerOptions _jsonOptions;

    public CustomerService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _customers = LoadCustomers();
    }

    /// <summary>
    /// Loads customers from the mock JSON file.
    /// </summary>
    private List<CustomerData> LoadCustomers()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "mock_customers.json");
        
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"Warning: Customer data file not found at {jsonPath}");
            return new List<CustomerData>();
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            var wrapper = JsonSerializer.Deserialize<CustomerDataWrapper>(json, _jsonOptions);
            return wrapper?.Customers ?? new List<CustomerData>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading customer data: {ex.Message}");
            return new List<CustomerData>();
        }
    }

    /// <summary>
    /// Attempts to authenticate a customer based on extracted data points.
    /// Requires at least 3 matching data points for sensitive operations.
    /// </summary>
    /// <param name="extractedData">Dictionary of extracted data from the email.</param>
    /// <returns>Authentication result with details.</returns>
    public AuthenticationResult Authenticate(Dictionary<string, string> extractedData)
    {
        // Define required fields for authentication (need at least 2 per Case Study)
        // Valid fields: contract number, full name, birthday, street, postal code, installment amount
        // Note: Email is NOT a valid authentication field per Case Study specification
        var authFields = new[] { "contractNumber", "fullName", "birthday", "street", "postalCode", "monthlyPayment", "meterNumber" };
        var providedFields = new List<string>();
        var missingFields = new List<string>();

        // Check which fields are provided
        foreach (var field in authFields)
        {
            if (extractedData.ContainsKey(field) && !string.IsNullOrWhiteSpace(extractedData[field]))
            {
                providedFields.Add(field);
            }
        }

        // Try to identify a matching customer
        var matchedCustomer = FindMatchingCustomer(extractedData, providedFields);
        var validatedFields = matchedCustomer != null 
            ? ValidateFields(matchedCustomer, extractedData, providedFields)
            : new List<string>();

        // Threshold check (Rule 2.34): 3 points needed for sensitive operations.
        var secureFields = new[] { "contractNumber", "fullName", "birthday", "street", "postalCode", "monthlyPayment", "meterNumber" };
        var secureValidated = validatedFields.Where(f => secureFields.Contains(f)).ToList();

        const int threshold = 3;
        
        // If we have enough validated points, success!
        if (secureValidated.Count >= threshold && matchedCustomer != null)
        {
            return AuthenticationResult.Success(matchedCustomer, secureValidated);
        }

        // Otherwise, ask for missing data points to reach the threshold
        foreach (var field in secureFields)
        {
            if (!secureValidated.Contains(field))
            {
                missingFields.Add(GetGermanFieldName(field));
            }
        }
            
        var pointsNeeded = threshold - secureValidated.Count;

        // If we found a customer but missing points, return missing data with what we have
        if (matchedCustomer != null)
        {
            return AuthenticationResult.MissingData(missingFields, pointsNeeded, secureValidated);
        }

        // If no customer was found at all, we still ask for 3 points
        return AuthenticationResult.MissingData(missingFields, threshold, new List<string>());
    }

    /// <summary>
    /// Finds a customer that matches the provided data.
    /// </summary>
    private CustomerData? FindMatchingCustomer(Dictionary<string, string> data, List<string> fieldsToCheck)
    {
        foreach (var customer in _customers)
        {
            int matchCount = 0;
            
            if (fieldsToCheck.Contains("contractNumber") && 
                data.TryGetValue("contractNumber", out var contract) &&
                customer.ContractNumber.Equals(contract, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }

            if (data.TryGetValue("meterNumber", out var meter) &&
                !string.IsNullOrWhiteSpace(customer.MeterNumber) &&
                customer.MeterNumber.Equals(meter, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
            
            if (fieldsToCheck.Contains("fullName") && 
                data.TryGetValue("fullName", out var name) &&
                IsPartialNameMatch(name, customer.FullName))
            {
                matchCount++;
            }
            
            if (fieldsToCheck.Contains("birthday") && 
                data.TryGetValue("birthday", out var birthday) &&
                TryParseAndMatchDate(birthday, customer.Birthday))
            {
                matchCount++;
            }
            
            if (fieldsToCheck.Contains("street") && 
                data.TryGetValue("street", out var street) &&
                customer.Street.Equals(street, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
            
            if (fieldsToCheck.Contains("postalCode") && 
                data.TryGetValue("postalCode", out var postal) &&
                customer.PostalCode.Equals(postal, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
            
            // Monthly payment / installment amount (Case Study: "50,-")
            if (fieldsToCheck.Contains("monthlyPayment") && 
                data.TryGetValue("monthlyPayment", out var payment) &&
                TryMatchPayment(payment, customer.MonthlyPayment))
            {
                matchCount++;
            }

            // Email address
            if (fieldsToCheck.Contains("email") && 
                data.TryGetValue("email", out var email) &&
                customer.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
            
            // If at least 2 fields match, this is likely the right customer
            if (matchCount >= 2)
            {
                return customer;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Validates which fields match for the given customer.
    /// </summary>
    private List<string> ValidateFields(CustomerData customer, Dictionary<string, string> data, List<string> fieldsToCheck)
    {
        var validated = new List<string>();
        
        if (data.TryGetValue("meterNumber", out var meter) &&
            !string.IsNullOrWhiteSpace(customer.MeterNumber) &&
            customer.MeterNumber.Equals(meter, StringComparison.OrdinalIgnoreCase))
        {
            validated.Add("meterNumber");
        }

        if (fieldsToCheck.Contains("contractNumber") && 
            data.TryGetValue("contractNumber", out var contract) &&
            customer.ContractNumber.Equals(contract, StringComparison.OrdinalIgnoreCase))
        {
            validated.Add("contractNumber");
        }
        
        if (fieldsToCheck.Contains("fullName") && 
            data.TryGetValue("fullName", out var name) &&
            IsPartialNameMatch(name, customer.FullName))
        {
            validated.Add("fullName");
        }
        
        if (fieldsToCheck.Contains("birthday") && 
            data.TryGetValue("birthday", out var birthday) &&
            TryParseAndMatchDate(birthday, customer.Birthday))
        {
            validated.Add("birthday");
        }
        
        if (fieldsToCheck.Contains("street") && 
            data.TryGetValue("street", out var street) &&
            customer.Street.Equals(street, StringComparison.OrdinalIgnoreCase))
        {
            validated.Add("street");
        }
        
        if (fieldsToCheck.Contains("postalCode") && 
            data.TryGetValue("postalCode", out var postal) &&
            customer.PostalCode.Equals(postal, StringComparison.OrdinalIgnoreCase))
        {
            validated.Add("postalCode");
        }
        
        if (fieldsToCheck.Contains("monthlyPayment") && 
            data.TryGetValue("monthlyPayment", out var payment) &&
            TryMatchPayment(payment, customer.MonthlyPayment))
        {
            validated.Add("monthlyPayment");
        }

        if (fieldsToCheck.Contains("email") && 
            data.TryGetValue("email", out var email) &&
            customer.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
        {
            validated.Add("email");
        }
        
        return validated;
    }

    /// <summary>
    /// Tries to parse a date string and match it against a target date.
    /// </summary>
    private bool TryParseAndMatchDate(string dateString, DateTime target)
    {
        // Try various German date formats
        var formats = new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy" };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateString, format, 
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
            {
                return parsed.Date == target.Date;
            }
        }
        
        return DateTime.TryParse(dateString, out var fallbackParsed) && 
               fallbackParsed.Date == target.Date;
    }

    /// <summary>
    /// Tries to match a payment amount string against a target value.
    /// Handles formats like "50,-", "50€", "50.00", "50,00"
    /// </summary>
    private bool TryMatchPayment(string paymentString, decimal target)
    {
        // Clean the input: remove currency symbols, whitespace, and common separators
        var cleaned = paymentString
            .Replace("€", "")
            .Replace("EUR", "")
            .Replace(",-", "")
            .Replace(" ", "")
            .Trim();
        
        // Try parsing with different culture formats
        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var germanParsed))
        {
            return Math.Abs(germanParsed - target) < 0.01m;
        }
        
        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var invariantParsed))
        {
            return Math.Abs(invariantParsed - target) < 0.01m;
        }
        
        return false;
    }

    /// <summary>
    /// Checks if provided name matches stored name (supports abbreviations).
    /// Examples: "J. Meyer" matches "Julia Meyer", "Schmidt" matches "Thomas Schmidt"
    /// </summary>
    private bool IsPartialNameMatch(string provided, string stored)
    {
        // Exact match
        if (provided.Equals(stored, StringComparison.OrdinalIgnoreCase))
            return true;
        
        var providedParts = provided.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var storedParts = stored.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Case: "J. Meyer" vs "Julia Meyer"
        if (providedParts.Length == storedParts.Length)
        {
            for (int i = 0; i < providedParts.Length; i++)
            {
                var prov = providedParts[i].TrimEnd('.');
                var stor = storedParts[i];
                
                // Check if abbreviated (J matches Julia)
                if (prov.Length == 1 && stor.StartsWith(prov, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // Check if exact match
                if (prov.Equals(stor, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                return false;
            }
            return true;
        }
        
        // Case: "Schmidt" vs "Thomas Schmidt" (last name only)
        if (providedParts.Length == 1 && storedParts.Length > 1)
        {
            return storedParts.Any(part => 
                part.Equals(provided, StringComparison.OrdinalIgnoreCase));
        }
        
        // Case: "Julia M" vs "Julia Meyer" (abbreviated last name)
        if (providedParts.Length == storedParts.Length && providedParts.Length == 2)
        {
            var lastProvided = providedParts[1].TrimEnd('.');
            var lastStored = storedParts[1];
            
            if (lastProvided.Length == 1 && lastStored.StartsWith(lastProvided, StringComparison.OrdinalIgnoreCase) &&
                providedParts[0].Equals(storedParts[0], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets the German name for a field.
    /// </summary>
    private string GetGermanFieldName(string fieldName)
    {
        return fieldName switch
        {
            "contractNumber" => "Vertragsnummer",
            "fullName" => "Vollständiger Name",
            "birthday" => "Geburtsdatum",
            "street" => "Straße und Hausnummer",
            "postalCode" => "Postleitzahl",
            "monthlyPayment" => "Abschlagsbetrag",
            "email" => "E-Mail-Adresse",
            _ => fieldName
        };
    }

    /// <summary>
    /// Gets a customer by contract number.
    /// </summary>
    public CustomerData? GetByContractNumber(string contractNumber)
    {
        return _customers.FirstOrDefault(c => 
            c.ContractNumber.Equals(contractNumber, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a customer by email address.
    /// </summary>
    public CustomerData? GetByEmail(string email)
    {
        return _customers.FirstOrDefault(c => 
            c.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all customers.
    /// </summary>
    public IReadOnlyList<CustomerData> GetAll() => _customers.AsReadOnly();
}

/// <summary>
/// Wrapper class for deserializing the JSON structure.
/// </summary>
internal class CustomerDataWrapper
{
    public List<CustomerData> Customers { get; set; } = new();
}
