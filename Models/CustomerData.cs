namespace CustomerContactCopilot.Models;

/// <summary>
/// Represents customer data used for authentication and account lookup.
/// </summary>
public class CustomerData
{
    /// <summary>
    /// Unique contract number identifying the customer account.
    /// </summary>
    public string ContractNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Meter number (e.g., "LB-9876543").
    /// </summary>
    public string MeterNumber { get; set; } = string.Empty;


    /// <summary>
    /// Customer's full name (first and last name).
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Customer's date of birth (used for authentication).
    /// </summary>
    public DateTime Birthday { get; set; }

    /// <summary>
    /// Street address including house number.
    /// </summary>
    public string Street { get; set; } = string.Empty;

    /// <summary>
    /// Postal code (PLZ) of the customer's address.
    /// </summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// City name.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Customer's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Current tariff/product the customer is subscribed to.
    /// </summary>
    public string CurrentTariff { get; set; } = string.Empty;

    /// <summary>
    /// Monthly payment amount in EUR.
    /// </summary>
    public decimal MonthlyPayment { get; set; }

    /// <summary>
    /// Date when the current contract started.
    /// </summary>
    public DateTime ContractStartDate { get; set; }

    /// <summary>
    /// Indicates whether the customer account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last recorded meter reading in kWh.
    /// </summary>
    public long LastMeterReading { get; set; }

    /// <summary>
    /// Date of the last meter reading.
    /// </summary>
    public DateTime LastMeterReadingDate { get; set; }

    /// <summary>
    /// Expected yearly electricity consumption in kWh.
    /// Used for plausibility checks.
    /// </summary>
    public int ExpectedYearlyConsumption { get; set; }
}
