using CustomerContactCopilot.Models;
using CustomerContactCopilot.Agents;

namespace CustomerContactCopilot.Services;

/// <summary>
/// Aggregates results from multiple agents and presents a unified output.
/// Separates completed tasks from those requiring action, and supports
/// a review/approval workflow before final output.
/// </summary>
public class ResultAggregator
{
    private readonly List<AgentResult> _results = new();
    private readonly AgentLogger? _logger;

    public ResultAggregator(AgentLogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a result from an agent.
    /// </summary>
    public void AddResult(AgentResult result)
    {
        _results.Add(result);
    }

    /// <summary>
    /// Adds a processing state result with AI reasoning.
    /// </summary>
    public void AddProcessingResult(string agentName, ProcessingState state)
    {
        var status = DetermineStatus(state);
        var reasoning = GenerateAiReasoning(state);
        
        _results.Add(new AgentResult
        {
            AgentName = agentName,
            Status = status,
            Summary = GenerateSummary(agentName, state),
            Details = state.Actions,
            RequiresAction = status == TaskStatus.ActionRequired || status == TaskStatus.AwaitingInput,
            ActionDescription = state.HumanReviewReason,
            ResponseDraft = state.ResponseDraft,
            AiReasoning = reasoning,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Adds a product recommendation result.
    /// </summary>
    public void AddProductRecommendation(ProductRecommendation recommendation, string customerName = "")
    {
        _results.Add(new AgentResult
        {
            AgentName = "ProductInfoAgent",
            Status = TaskStatus.Completed,
            Summary = $"Tariff recommendation for {customerName}: {recommendation.RecommendedTariff}",
            Details = new List<string>
            {
                $"Recommended tariff: {recommendation.RecommendedTariff}",
                $"Estimated savings: {recommendation.EstimatedSavingsPerYear}€/year",
                $"Suitability: {recommendation.SuitabilityScore}/10",
                recommendation.Reasoning
            },
            AiReasoning = $"Tarifempfehlung basiert auf Haushaltsprofil-Analyse. Begründung: {recommendation.Reasoning}",
            RequiresAction = false,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Adds a meter reading validation result.
    /// </summary>
    public void AddMeterReadingResult(MeterReadingValidation validation, string customerName = "")
    {
        var status = validation.IsPlausibilityWarning ? TaskStatus.ActionRequired :
                     validation.IsValid ? TaskStatus.Completed : TaskStatus.Failed;

        _results.Add(new AgentResult
        {
            AgentName = "MeterReadingAgent",
            Status = status,
            Summary = $"Meter reading validation for {customerName}: {validation.Reading:N0} kWh",
            Details = new List<string>
            {
                $"Meter reading: {validation.Reading:N0} kWh",
                $"Consumption: {validation.Consumption:N0} kWh",
                $"Expected: {validation.ExpectedConsumption:N0} kWh",
                validation.Message
            },
            RequiresAction = validation.IsPlausibilityWarning,
            ActionDescription = validation.IsPlausibilityWarning 
                ? "Customer confirmation required for unusually high consumption" 
                : null,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets aggregated results separated by status.
    /// </summary>
    public AggregatedOutput GetAggregatedOutput()
    {
        return new AggregatedOutput
        {
            CompletedTasks = _results.Where(r => r.Status == TaskStatus.Completed).ToList(),
            ActionRequiredTasks = _results.Where(r => r.Status == TaskStatus.ActionRequired).ToList(),
            AwaitingInputTasks = _results.Where(r => r.Status == TaskStatus.AwaitingInput).ToList(),
            FailedTasks = _results.Where(r => r.Status == TaskStatus.Failed).ToList(),
            TotalResults = _results.Count,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Displays the aggregated output and waits for approval.
    /// Returns true if approved, false otherwise.
    /// </summary>
    public bool DisplayAndAwaitApproval()
    {
        var output = GetAggregatedOutput();
        
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              AGGREGATED PROCESSING RESULTS                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // === COMPLETED TASKS ===
        if (output.CompletedTasks.Any())
        {
            Console.WriteLine("✅ COMPLETED TASKS:");
            Console.WriteLine(new string('─', 60));
            foreach (var task in output.CompletedTasks)
            {
                DisplayTask(task, "  ✓");
            }
            Console.WriteLine();
        }

        // === ACTION REQUIRED TASKS ===
        if (output.ActionRequiredTasks.Any())
        {
            Console.WriteLine("⚠️  ACTION REQUIRED:");
            Console.WriteLine(new string('─', 60));
            foreach (var task in output.ActionRequiredTasks)
            {
                DisplayTask(task, "  ⚡");
                if (!string.IsNullOrEmpty(task.ActionDescription))
                {
                    Console.WriteLine($"     📋 Aktion: {task.ActionDescription}");
                }
            }
            Console.WriteLine();
        }

        // === AWAITING INPUT TASKS ===
        if (output.AwaitingInputTasks.Any())
        {
            Console.WriteLine("⏳ AWAITING CUSTOMER INPUT:");
            Console.WriteLine(new string('─', 60));
            foreach (var task in output.AwaitingInputTasks)
            {
                DisplayTask(task, "  ⏸");
                if (!string.IsNullOrEmpty(task.ActionDescription))
                {
                    Console.WriteLine($"     📋 Benötigt: {task.ActionDescription}");
                }
            }
            Console.WriteLine();
        }

        // === FAILED TASKS ===
        if (output.FailedTasks.Any())
        {
            Console.WriteLine("❌ FAILED TASKS:");
            Console.WriteLine(new string('─', 60));
            foreach (var task in output.FailedTasks)
            {
                DisplayTask(task, "  ✗");
            }
            Console.WriteLine();
        }

        // === RESPONSE DRAFTS ===
        var drafts = _results.Where(r => !string.IsNullOrEmpty(r.ResponseDraft)).ToList();
        if (drafts.Any())
        {
            Console.WriteLine("📝 DRAFT RESPONSES:");
            Console.WriteLine(new string('─', 60));
            foreach (var draft in drafts)
            {
                Console.WriteLine($"  [{draft.AgentName}]:");
                var lines = draft.ResponseDraft!.Split('\n').Take(6);
                foreach (var line in lines)
                {
                    Console.WriteLine($"    {line.TrimEnd()}");
                }
                if (draft.ResponseDraft!.Split('\n').Length > 6)
                {
                    Console.WriteLine($"    ... (truncated)");
                }
                Console.WriteLine();
            }
        }

        // === SUMMARY ===
        Console.WriteLine(new string('═', 60));
        Console.WriteLine($"  Total: {output.TotalResults} | " +
                         $"Completed: {output.CompletedTasks.Count} | " +
                         $"Action Required: {output.ActionRequiredTasks.Count} | " +
                         $"Awaiting: {output.AwaitingInputTasks.Count} | " +
                         $"Failed: {output.FailedTasks.Count}");
        Console.WriteLine(new string('═', 60));
        Console.WriteLine();

        // === APPROVAL WORKFLOW ===
        return AwaitApproval();
    }

    /// <summary>
    /// Waits for service agent approval.
    /// </summary>
    private bool AwaitApproval()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    APPROVAL REQUIRED                         ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  A Service Agent must review and approve these results       ║");
        Console.WriteLine("║  before responses are sent to the customer.                  ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Type 'APPROVED' to proceed, or 'REJECT' to cancel:          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.Write("  Service Agent Decision: ");

        var maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var input = Console.ReadLine()?.Trim().ToUpperInvariant();

            if (input == "APPROVED")
            {
                Console.WriteLine();
                Console.WriteLine("  ✅ APPROVED - Responses will be sent to customers.");
                _logger?.LogAction("approval", "service_agent_approved", 
                    $"Results approved by service agent");
                return true;
            }
            else if (input == "REJECT" || input == "REJECTED")
            {
                Console.WriteLine();
                Console.WriteLine("  ❌ REJECTED - Responses will NOT be sent. Review needed.");
                _logger?.LogAction("approval", "service_agent_rejected", 
                    $"Results rejected by service agent");
                return false;
            }
            else
            {
                if (attempt < maxAttempts)
                {
                    Console.WriteLine($"  ⚠️  Invalid input. Please type 'APPROVED' or 'REJECT'. ({maxAttempts - attempt} attempts remaining)");
                    Console.Write("  Service Agent Decision: ");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("  ⏱️  No valid response - defaulting to REJECT for safety.");
        return false;
    }

    /// <summary>
    /// Displays a single task result with AI reasoning.
    /// </summary>
    private void DisplayTask(AgentResult task, string prefix)
    {
        Console.WriteLine($"{prefix} [{task.AgentName}] {task.Summary}");
        
        // Show AI Reasoning for observability
        if (!string.IsNullOrEmpty(task.AiReasoning))
        {
            Console.WriteLine($"     🤖 KI-Begründung: {task.AiReasoning}");
        }
        
        // Show details
        foreach (var detail in task.Details.Take(4))
        {
            Console.WriteLine($"     • {detail}");
        }
        if (task.Details.Count > 4)
        {
            Console.WriteLine($"     ... und {task.Details.Count - 4} weitere");
        }
    }

    /// <summary>
    /// Determines task status from processing state.
    /// </summary>
    private TaskStatus DetermineStatus(ProcessingState state)
    {
        if (state.Errors.Any())
            return TaskStatus.Failed;

        if (!state.IsAuthenticated && state.DetectedIntent != "ProductInfoRequest")
            return TaskStatus.AwaitingInput;

        if (state.RequiresHumanReview)
            return TaskStatus.ActionRequired;

        if (state.CurrentStep == ProcessingStep.Completed)
            return TaskStatus.Completed;

        return TaskStatus.InProgress;
    }

    /// <summary>
    /// Generates a summary for a processing state.
    /// </summary>
    private string GenerateSummary(string agentName, ProcessingState state)
    {
        var intent = state.DetectedIntent ?? "Unknown";
        var customer = state.AuthenticatedCustomer?.FullName ?? "Unbekannter Kunde";
        
        return state.IsAuthenticated
            ? $"{intent} für {customer}"
            : $"{intent} - Authentifizierung ausstehend";
    }

    /// <summary>
    /// Generates AI reasoning/justification for observability.
    /// </summary>
    private string GenerateAiReasoning(ProcessingState state)
    {
        var reasoning = new List<string>();

        // Intent detection reasoning
        if (!string.IsNullOrEmpty(state.DetectedIntent))
        {
            reasoning.Add($"Intent '{state.DetectedIntent}' erkannt durch Analyse von Schlüsselwörtern und Kontext.");
        }

        // Authentication reasoning
        if (state.IsAuthenticated && state.AuthenticatedCustomer != null)
        {
            // Find which auth fields were used
            var authFields = new List<string>();
            if (state.ExtractedEntities.ContainsKey("contractNumber"))
                authFields.Add("Vertragsnummer");
            if (state.ExtractedEntities.ContainsKey("fullName"))
                authFields.Add("Name");
            if (state.ExtractedEntities.ContainsKey("birthday"))
                authFields.Add("Geburtsdatum");
            if (state.ExtractedEntities.ContainsKey("street"))
                authFields.Add("Adresse");
            if (state.ExtractedEntities.ContainsKey("postalCode"))
                authFields.Add("PLZ");

            if (authFields.Count >= 3)
            {
                reasoning.Add($"Authentifizierung erfolgreich durch {authFields.Count} Datenpunkte: {string.Join(", ", authFields)}.");
            }
            else if (authFields.Count > 0)
            {
                reasoning.Add($"Session-Authentifizierung wiederverwendet (ursprünglich validiert mit: {string.Join(", ", authFields)}).");
            }
        }
        else if (!state.IsAuthenticated && state.DetectedIntent != "ProductInfoRequest")
        {
            var providedFields = new List<string>();
            if (state.ExtractedEntities.ContainsKey("contractNumber"))
                providedFields.Add("Vertragsnummer");
            if (state.ExtractedEntities.ContainsKey("fullName"))
                providedFields.Add("Name");
            if (state.ExtractedEntities.ContainsKey("birthday"))
                providedFields.Add("Geburtsdatum");

            reasoning.Add($"Authentifizierung nicht möglich: {providedFields.Count}/3 benötigte Datenpunkte vorhanden" +
                         (providedFields.Count > 0 ? $" ({string.Join(", ", providedFields)})" : "") + ".");
        }

        // Action reasoning
        foreach (var action in state.Actions.Take(3))
        {
            if (action.Contains("meter_reading_recorded"))
            {
                reasoning.Add("Zählerstand validiert und in System übernommen.");
            }
            else if (action.Contains("plausibility_warning"))
            {
                reasoning.Add("Plausibility check detected unusually high consumption. Customer confirmation required.");
            }
            else if (action.Contains("session_auth_reused"))
            {
                reasoning.Add("Vorherige Session-Authentifizierung wiederverwendet (innerhalb gültiger Session).");
            }
            else if (action.Contains("customer_authenticated"))
            {
                // Already covered above
            }
        }

        // Human review reasoning
        if (state.RequiresHumanReview && !string.IsNullOrEmpty(state.HumanReviewReason))
        {
            reasoning.Add($"Manuelle Prüfung erforderlich: {state.HumanReviewReason}");
        }

        return reasoning.Count > 0 
            ? string.Join(" ", reasoning) 
            : "Standardverarbeitung ohne besondere Entscheidungen.";
    }

    /// <summary>
    /// Clears all results.
    /// </summary>
    public void Clear()
    {
        _results.Clear();
    }
}

/// <summary>
/// Result from a single agent.
/// </summary>
public class AgentResult
{
    public string AgentName { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();
    public bool RequiresAction { get; set; }
    public string? ActionDescription { get; set; }
    public string? ResponseDraft { get; set; }
    /// <summary>
    /// AI reasoning/justification for the decision (for observability).
    /// </summary>
    public string? AiReasoning { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Aggregated output with categorized results.
/// </summary>
public class AggregatedOutput
{
    public List<AgentResult> CompletedTasks { get; set; } = new();
    public List<AgentResult> ActionRequiredTasks { get; set; } = new();
    public List<AgentResult> AwaitingInputTasks { get; set; } = new();
    public List<AgentResult> FailedTasks { get; set; } = new();
    public int TotalResults { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Status of a task.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task completed successfully.</summary>
    Completed,
    /// <summary>Task in progress.</summary>
    InProgress,
    /// <summary>Task requires human action.</summary>
    ActionRequired,
    /// <summary>Task awaiting customer input.</summary>
    AwaitingInput,
    /// <summary>Task failed.</summary>
    Failed
}
