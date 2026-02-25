namespace CustomerContactCopilot.Models;

/// <summary>
/// Represents the current state of email processing workflow.
/// </summary>
public class ProcessingState
{
    /// <summary>
    /// Reference to the email being processed.
    /// </summary>
    public string EmailId { get; set; } = string.Empty;

    /// <summary>
    /// Current step in the processing workflow.
    /// </summary>
    public ProcessingStep CurrentStep { get; set; } = ProcessingStep.Received;

    /// <summary>
    /// Whether the customer has been successfully authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; } = false;

    /// <summary>
    /// The authenticated customer's data (null if not authenticated).
    /// </summary>
    public CustomerData? AuthenticatedCustomer { get; set; }

    /// <summary>
    /// Detected intent/category of the customer request.
    /// </summary>
    public string? DetectedIntent { get; set; }

    /// <summary>
    /// Result of the authentication attempt.
    /// </summary>
    public AuthenticationResult? AuthResult { get; set; }

    /// <summary>
    /// Extracted entities from the email (e.g., dates, addresses, meter readings).
    /// </summary>
    public Dictionary<string, string> ExtractedEntities { get; set; } = new();

    /// <summary>
    /// Generated response draft to be reviewed or sent.
    /// </summary>
    public string? ResponseDraft { get; set; }

    /// <summary>
    /// Actions taken or to be taken (e.g., "update_address", "schedule_callback").
    /// </summary>
    public List<string> Actions { get; set; } = new();

    /// <summary>
    /// Any errors or issues encountered during processing.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Timestamp when processing started.
    /// </summary>
    public DateTime ProcessingStartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when processing completed (null if still in progress).
    /// </summary>
    public DateTime? ProcessingCompletedAt { get; set; }

    /// <summary>
    /// Indicates if human review is required.
    /// </summary>
    public bool RequiresHumanReview { get; set; } = false;

    /// <summary>
    /// Reason why human review is required.
    /// </summary>
    public string? HumanReviewReason { get; set; }
}

/// <summary>
/// Enum representing the steps in the email processing workflow.
/// </summary>
public enum ProcessingStep
{
    /// <summary>Email received and queued for processing.</summary>
    Received,
    
    /// <summary>Attempting to authenticate the customer.</summary>
    Authenticating,
    
    /// <summary>Analyzing email content and detecting intent.</summary>
    AnalyzingIntent,
    
    /// <summary>Extracting relevant entities from the email.</summary>
    ExtractingEntities,
    
    /// <summary>Executing requested actions.</summary>
    ExecutingActions,
    
    /// <summary>Generating response to customer.</summary>
    GeneratingResponse,
    
    /// <summary>Awaiting human review before proceeding.</summary>
    AwaitingReview,
    
    /// <summary>Processing completed successfully.</summary>
    Completed,
    
    /// <summary>Processing failed with errors.</summary>
    Failed
}
