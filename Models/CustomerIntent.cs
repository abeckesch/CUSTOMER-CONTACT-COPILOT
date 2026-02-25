namespace CustomerContactCopilot.Models;

/// <summary>
/// Represents the detected intent of a customer email.
/// </summary>
public enum CustomerIntent
{
    /// <summary>Customer wants to submit a meter reading.</summary>
    MeterReadingSubmission,
    
    /// <summary>Customer is requesting product or tariff information.</summary>
    ProductInfoRequest,
    
    /// <summary>Customer wants to change their address.</summary>
    AddressChange,
    
    /// <summary>Customer wants to change their payment method or bank details.</summary>
    PaymentChange,
    
    /// <summary>Customer wants to terminate their contract.</summary>
    ContractTermination,
    
    /// <summary>Customer has a complaint or issue.</summary>
    Complaint,
    
    /// <summary>Customer has a billing question or dispute.</summary>
    BillingInquiry,
    
    /// <summary>Customer wants to change their tariff.</summary>
    TariffChange,
    
    /// <summary>Customer is requesting a callback.</summary>
    CallbackRequest,
    
    /// <summary>General inquiry that doesn't fit other categories.</summary>
    GeneralInquiry,
    
    /// <summary>Intent could not be determined.</summary>
    Unknown
}

/// <summary>
/// Extension methods for CustomerIntent.
/// </summary>
public static class CustomerIntentExtensions
{
    /// <summary>
    /// Determines if the intent requires customer authentication.
    /// Sensitive operations require verification of identity.
    /// </summary>
    public static bool RequiresAuthentication(this CustomerIntent intent)
    {
        return intent switch
        {
            CustomerIntent.MeterReadingSubmission => true,
            CustomerIntent.AddressChange => true,
            CustomerIntent.PaymentChange => true,
            CustomerIntent.ContractTermination => true,
            CustomerIntent.BillingInquiry => true,
            CustomerIntent.TariffChange => true,
            CustomerIntent.ProductInfoRequest => false,
            CustomerIntent.Complaint => true,
            CustomerIntent.CallbackRequest => false,
            CustomerIntent.GeneralInquiry => false,
            CustomerIntent.Unknown => false,
            _ => false
        };
    }
    
    /// <summary>
    /// Gets a human-readable English description of the intent.
    /// </summary>
    public static string GetDescription(this CustomerIntent intent)
    {
        return intent switch
        {
            CustomerIntent.MeterReadingSubmission => "Meter Reading Submission",
            CustomerIntent.ProductInfoRequest => "Product Inquiry",
            CustomerIntent.AddressChange => "Address Change",
            CustomerIntent.PaymentChange => "Payment Information Change",
            CustomerIntent.ContractTermination => "Contract Termination",
            CustomerIntent.Complaint => "Complaint",
            CustomerIntent.BillingInquiry => "Billing Inquiry",
            CustomerIntent.TariffChange => "Tariff Change",
            CustomerIntent.CallbackRequest => "Callback Request",
            CustomerIntent.GeneralInquiry => "General Inquiry",
            CustomerIntent.Unknown => "Unknown",
            _ => "Unknown"
        };
    }
}
