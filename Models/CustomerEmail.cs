namespace AgenticCustomerContactCopilot.Models;

/// <summary>
/// Represents an incoming customer email to be processed.
/// </summary>
public class CustomerEmail
{
    /// <summary>
    /// Unique identifier for the email.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Sender's email address.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Email subject line.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Full body content of the email.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the email was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional attachments (file names or paths).
    /// </summary>
    public List<string> Attachments { get; set; } = new();
}
