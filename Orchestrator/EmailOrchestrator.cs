using System;
using Microsoft.SemanticKernel;
using AgenticCustomerContactCopilot.Models;
using AgenticCustomerContactCopilot.Services;
using AgenticCustomerContactCopilot.Agents;
using System.Linq;
using System.Collections.Generic;

namespace AgenticCustomerContactCopilot.Orchestrator;

/// <summary>
/// Main orchestrator for processing customer emails using Semantic Kernel.
/// Coordinates text extraction, intent detection, authentication, and response generation.
/// Supports multi-turn conversations with persistent session state.
/// Includes observability logging and PII redaction for data protection.
/// </summary>
public class EmailOrchestrator
{
    private readonly Kernel _kernel;
    private readonly CustomerService _customerService;
    private readonly MeterReadingService _meterReadingService;
    private readonly SessionStateManager _sessionManager;
    private readonly AgentLogger _logger;
    private readonly PiiRedactionService _piiRedactor;
    private readonly ProductInfoAgent _productInfoAgent;

    public EmailOrchestrator(
        Kernel kernel, 
        CustomerService customerService, 
        MeterReadingService? meterReadingService = null,
        SessionStateManager? sessionManager = null,
        AgentLogger? logger = null,
        PiiRedactionService? piiRedactor = null,
        ProductInfoAgent? productInfoAgent = null)
    {
        _kernel = kernel;
        _customerService = customerService;
        _meterReadingService = meterReadingService ?? new MeterReadingService();
        _sessionManager = sessionManager ?? new SessionStateManager();
        _logger = logger ?? new AgentLogger();
        _piiRedactor = piiRedactor ?? new PiiRedactionService();
        _productInfoAgent = productInfoAgent ?? new ProductInfoAgent(kernel);
    }

    /// <summary>
    /// Processes a customer email through the complete workflow.
    /// </summary>
    public async Task<ProcessingState> ProcessEmailAsync(CustomerEmail email)
    {
        var traceId = _logger.StartProcessing(email.Id, email.FromAddress);
        var state = new ProcessingState
        {
            EmailId = email.Id,
            CurrentStep = ProcessingStep.Received,
            ProcessingStartedAt = DateTime.UtcNow
        };

        try
        {
            // Step 1: Extract text content
            state.CurrentStep = ProcessingStep.AnalyzingIntent;
            var emailContent = await ExtractTextFromEmailAsync(email);

            // Step 2: Detect intents
            var intents = await DetectIntentsAsync(emailContent);
            var mainIntent = intents.FirstOrDefault(); // Fallback to first
            state.DetectedIntent = string.Join(", ", intents);

            // Step 3: Extract entities
            state.CurrentStep = ProcessingStep.ExtractingEntities;
            state.ExtractedEntities = await ExtractEntitiesAsync(emailContent, mainIntent, traceId);

            // Step 4: Check if authentication is required
            if (mainIntent.RequiresAuthentication())
            {
                state.CurrentStep = ProcessingStep.Authenticating;
                var authResult = _customerService.Authenticate(state.ExtractedEntities);

                if (!authResult.IsAuthenticated)
                {
                    // Generate response asking for missing data
                    state.RequiresHumanReview = false;
                    state.CurrentStep = ProcessingStep.GeneratingResponse;
                    state.ResponseDraft = await GenerateAuthenticationRequestAsync(
                        email, mainIntent, authResult);
                    state.ProcessingCompletedAt = DateTime.UtcNow;
                    return state;
                }

                state.IsAuthenticated = true;
                state.AuthenticatedCustomer = authResult.Customer;
            }

            // Step 5: Execute actions based on intent
            state.CurrentStep = ProcessingStep.ExecutingActions;
            foreach (var intent in intents)
            {
                 await ExecuteActionsAsync(state, null, intent, email.Body);
            }

            // Step 6: Generate response
            state.CurrentStep = ProcessingStep.GeneratingResponse;
            state.ResponseDraft = await GenerateResponseAsync(email, state, mainIntent);

            state.CurrentStep = ProcessingStep.Completed;
            state.ProcessingCompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            state.CurrentStep = ProcessingStep.Failed;
            state.Errors.Add($"Processing error: {ex.Message}");
            state.ProcessingCompletedAt = DateTime.UtcNow;
        }

        return state;
    }

    /// <summary>
    /// Processes a customer email with session state management for multi-turn conversations.
    /// Uses persistent sessions to track authentication and correlate follow-up emails.
    /// Includes observability logging and PII redaction.
    /// </summary>
    public async Task<(ProcessingState State, ConversationSession Session)> ProcessEmailWithSessionAsync(CustomerEmail email)
    {
        // Start trace for this processing request
        var traceId = _logger.StartProcessing(email.Id, email.FromAddress);
        
        // Get or create session for this email address
        var existingSession = _sessionManager.GetSession(email.FromAddress);
        var isSessionReused = existingSession != null;
        var session = existingSession ?? _sessionManager.GetOrCreateSession(email.FromAddress);
        _logger.LogSessionOperation(traceId, isSessionReused ? "Resumed" : "Created", session.SessionId, session.IsAuthenticated);
        
        var state = new ProcessingState
        {
            EmailId = email.Id,
            CurrentStep = ProcessingStep.Received,
            ProcessingStartedAt = DateTime.UtcNow
        };

        try
        {
            // Step 1: Extract text content (with PII awareness)
            state.CurrentStep = ProcessingStep.AnalyzingIntent;
            var emailContent = await ExtractTextFromEmailAsync(email);

            // Step 2: Detect intents (using redacted content for LLM)
            var redactedContent = _piiRedactor.RedactForEntityExtraction(emailContent);
            var intents = await DetectIntentsAsync(emailContent);
            var mainIntent = intents.FirstOrDefault();
            state.DetectedIntent = string.Join(", ", intents);
            
            // Log intent detection decision
            _logger.LogIntentDetection(
                traceId, 
                state.DetectedIntent, 
                $"Detected {intents.Count} intents. Auth required: {intents.Any(i => i.RequiresAuthentication())}"
            );

            // Step 3: Extract entities from this email (using main intent context if needed, or aggregate)
            state.CurrentStep = ProcessingStep.ExtractingEntities;
            // TODO: Ideally pass all intents to extraction or extract per intent. For now, use main intent or merge.
            // We use the most complex intent for extraction prompts usually.
            var entitiesIntent = intents.FirstOrDefault(i => i.RequiresAuthentication());
            if (entitiesIntent == CustomerIntent.Unknown)
            {
                entitiesIntent = mainIntent;
            }
                
            var currentEntities = await ExtractEntitiesAsync(emailContent, entitiesIntent, traceId);
            
            // Add sender email as a potential authentication factor
            if (!string.IsNullOrEmpty(email.FromAddress))
            {
                currentEntities["email"] = email.FromAddress;
            }
            

            
            // Log entity extraction (with PII redaction)
            _logger.LogEntityExtraction(traceId, currentEntities);
            
            // Merge with previously collected authentication data from session
            session.MergeAuthData(currentEntities);
            state.ExtractedEntities = new Dictionary<string, string>(session.CollectedAuthData);


            
            // Copy non-auth entities from current email
            foreach (var kvp in currentEntities)
            {
                if (!state.ExtractedEntities.ContainsKey(kvp.Key))
                {
                    state.ExtractedEntities[kvp.Key] = kvp.Value;
                }
            }

            // Step 4: Check if authentication is required for ANY intent
            // OR if there's a pending auth action from a previous turn (follow-up providing auth data)
            var hasPendingAuthAction = session.PendingActions
                .Any(a => a.ActionType == "provide_auth_data" && !a.IsResolved);
            var authRequired = intents.Any(i => i.RequiresAuthentication()) || hasPendingAuthAction;
            
            if (authRequired)
            {
                state.CurrentStep = ProcessingStep.Authenticating;
                
                // Check if session is already authenticated
                if (session.IsAuthenticated && session.AuthenticatedContractNumber != null)
                {
                    // Reuse existing authentication
                    var customer = _customerService.GetByContractNumber(session.AuthenticatedContractNumber);
                    if (customer != null)
                    {
                        state.IsAuthenticated = true;
                        state.AuthenticatedCustomer = customer;
                        state.Actions.Add("session_auth_reused");
                    }
                }
                
                if (!state.IsAuthenticated)
                {
                    // Try to authenticate with aggregated data
                    var authResult = _customerService.Authenticate(state.ExtractedEntities);
                    state.AuthResult = authResult;
                    
                    // Log authentication attempt
                    _logger.LogAuthenticationAttempt(
                        traceId,
                        authResult.IsAuthenticated,
                        state.ExtractedEntities.Count,
                        authResult.ValidatedFields.Count,
                        authResult.ValidatedFields,
                        authResult.MissingFields,
                        authResult.Customer?.ContractNumber
                    );

                    if (!authResult.IsAuthenticated)
                    {
                        // PROCESSING GAP FILLER: If we have ProductInfoRequest, answer it NOW even if auth failed
                        string? partialProductInfo = null;
                        if (intents.Contains(CustomerIntent.ProductInfoRequest))
                        {
                            // Avoid redundancy: Only send info if NOT sent before
                            bool infoAlreadySent = session.EmailHistory.Any(e => 
                                e.DetectedIntent != null && e.DetectedIntent.Contains("ProductInfoRequest"));

                            if (!infoAlreadySent)
                            {
                                var productQuestion = email.Body; // Simplification: assume body is the question
                                partialProductInfo = await _productInfoAgent.AnswerQuestionAsync(productQuestion);
                                state.Actions.Add("product_info_provided_during_auth");
                            }
                        }

                        // Check for pending auth response
                        var pendingAuth = session.PendingActions
                            .FirstOrDefault(a => a.ActionType == "provide_auth_data" && !a.IsResolved);
                        
                        if (pendingAuth != null)
                        {
                            // This is a follow-up email providing auth data
                            state.Actions.Add("followup_auth_attempt");
                            _logger.LogAction(traceId, "followup_auth_attempt", "Customer provided additional auth data");
                        }
                        
                        // Store pending action for auth data
                        if (pendingAuth == null)
                        {
                            _sessionManager.AddPendingAction(session, "provide_auth_data", email.Id);
                        }
                        
                        // Generate response asking for missing data AND including product info
                        state.RequiresHumanReview = false;
                        state.CurrentStep = ProcessingStep.GeneratingResponse;
                        
                        // We overload the auth request generator to include partial data
                        state.ResponseDraft = await GenerateAuthenticationRequestAsync(
                            email, mainIntent, authResult, partialProductInfo);
                        
                        session.AddEmailToHistory(email, state);
                        _sessionManager.UpdateSession(session);
                        state.ProcessingCompletedAt = DateTime.UtcNow;
                        
                        _logger.LogProcessingComplete(traceId, "AwaitingAuthData", 
                            DateTime.UtcNow - state.ProcessingStartedAt, state.Actions);
                        
                        return (state, session);
                    }

                    // Authentication successful
                    session.SetAuthenticated(authResult.Customer!);
                    _sessionManager.ResolvePendingAction(session, "provide_auth_data");
                    state.IsAuthenticated = true;
                    state.AuthenticatedCustomer = authResult.Customer;
                    state.Actions.Add("customer_authenticated");
                    _logger.LogAction(traceId, "customer_authenticated", 
                        $"Authenticated {authResult.Customer!.FullName} with {authResult.ValidatedFields.Count} fields");
                    
                    // RETROACTIVE PLAUSIBILITY CHECK: Now that we're authenticated,
                    // check if there was a meter reading in a previous email that needs validation
                    var previousMeterEmail = session.EmailHistory
                        .FirstOrDefault(e => e.DetectedIntent?.Contains("MeterReading") == true);
                    
                    if (previousMeterEmail != null && session.CollectedAuthData.TryGetValue("meterReading", out var pendingReading))
                    {
                        // We have a previous reading that was submitted before auth - validate it now
                        var retroValidation = _meterReadingService.ValidateWithPlausibility(
                            pendingReading, 
                            authResult.Customer!, 
                            DateTime.Now);
                        
                        if (retroValidation.IsPlausibilityWarning)
                        {
                            // High consumption detected - trigger warning
                            state.RequiresHumanReview = true;
                            state.HumanReviewReason = retroValidation.Message;
                            state.Actions.Add($"retroactive_plausibility_warning:{pendingReading}");
                            state.Actions.Add($"consumption_delta:{retroValidation.Consumption}kWh");
                            state.Actions.Add($"expected_consumption:{retroValidation.ExpectedConsumption}kWh");
                            state.ExtractedEntities["meterReading"] = pendingReading;
                            state.ExtractedEntities["consumption"] = retroValidation.Consumption.ToString();
                            state.ExtractedEntities["expectedConsumption"] = retroValidation.ExpectedConsumption.ToString();
                            
                            // Store pending action for correction handling in next email
                            var actionData = new Dictionary<string, string> { { "reading", pendingReading } };
                            _sessionManager.AddPendingAction(session, "meter_reading_confirmation", state.EmailId, actionData);
                            
                            _logger.LogAction(traceId, "retroactive_plausibility_warning", 
                                $"Meter reading {pendingReading} kWh validated after auth - consumption {retroValidation.Consumption} kWh exceeds expected");
                        }
                        else if (retroValidation.IsValid)
                        {
                            // Reading is valid - record it
                            state.Actions.Add($"meter_reading_recorded:{pendingReading}");
                            state.Actions.Add($"consumption:{retroValidation.Consumption}kWh");
                        }
                    }
                }
            }

            // Step 5: Check for pending actions that this email might resolve
            await HandlePendingActionsAsync(email, state, session, mainIntent);

            // Step 6: Execute actions based on ALL intents
            state.CurrentStep = ProcessingStep.ExecutingActions;
            foreach (var processIntent in intents)
            {
                 // Execute actions based on intent
                 await ExecuteActionsAsync(state, session, processIntent, email.Body);
            }

            // Step 7: Generate response
            state.CurrentStep = ProcessingStep.GeneratingResponse;
            state.ResponseDraft = await GenerateResponseAsync(email, state, mainIntent, session);

            state.CurrentStep = ProcessingStep.Completed;
            state.ProcessingCompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            state.CurrentStep = ProcessingStep.Failed;
            state.Errors.Add($"Processing error: {ex.Message}");
            state.ProcessingCompletedAt = DateTime.UtcNow;
        }

        // Save session state
        session.AddEmailToHistory(email, state);
        _sessionManager.UpdateSession(session);

        return (state, session);
    }

    /// <summary>
    /// Handles pending actions from previous emails in the conversation.
    /// </summary>
    private async Task HandlePendingActionsAsync(
        CustomerEmail email, 
        ProcessingState state, 
        ConversationSession session,
        CustomerIntent intent)
    {
        var pendingActions = _sessionManager.GetPendingActions(session);
        
        foreach (var action in pendingActions)
        {
            // Skip actions created in the current turn (they are for the NEXT email)
            if (action.SourceEmailId == state.EmailId)
            {
                continue;
            }

            switch (action.ActionType)
            {
                case "meter_reading_confirmation":
                    // Check if this email confirms or corrects a meter reading
                    var content = email.Body.ToLowerInvariant();
                    
                    // Priority 1: Did the customer provide a NEW reading? (Correction)
                    if (state.ExtractedEntities.TryGetValue("meterReading", out var newReading))
                    {
                        // Customer provided a corrected reading
                        state.Actions.Add($"meter_reading_corrected:{newReading}");
                        state.Actions.Add("correction_accepted");
                        _sessionManager.ResolvePendingAction(session, action.ActionType);
                    }
                    // Priority 2: Did they confirm the OLD reading?
                    else if (content.Contains("confirm") || content.Contains("yes") || 
                             content.Contains("bestätig") || content.Contains("stimmt") ||
                             // Be careful with "correct" - it can be "is correct" (confirm) or "the correct value is" (correction)
                             // Since we checked for new reading above, "correct" here likely means confirmation if no number provided
                             content.Contains("correct") || content.Contains("richtig"))
                    {
                        state.Actions.Add("meter_reading_confirmed");
                        _sessionManager.ResolvePendingAction(session, action.ActionType);
                        
                        // Record the confirmed reading (which is the OLD one from the pending action)
                        if (action.ActionData.TryGetValue("reading", out var confirmedReading))
                        {
                            state.Actions.Add($"confirmed_reading:{confirmedReading}");
                        }
                    }
                    break;

                case "provide_auth_data":
                    // Already handled in authentication step
                    break;
            }
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the session manager for direct access.
    /// </summary>
    public SessionStateManager SessionManager => _sessionManager;


    /// <summary>
    /// Extracts text content from an email file or email object.
    /// </summary>
    public async Task<string> ExtractTextFromEmailAsync(CustomerEmail email)
    {
        // Distinguish between subject and body for smarter analysis
        var content = $"SUBJECT/TOPIC: {email.Subject}\n\nLATEST MESSAGE BODY:\n{email.Body}";

        // If there are attachments, note them
        if (email.Attachments.Any())
        {
            content += $"\n\nAttachments: {string.Join(", ", email.Attachments)}";
        }

        return await Task.FromResult(content);
    }

    /// <summary>
    /// Extracts text from an email file (.eml, .txt, etc.).
    /// </summary>
    public async Task<string> ExtractTextFromEmailFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Email file not found: {filePath}");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".txt" => await File.ReadAllTextAsync(filePath),
            ".eml" => await ParseEmlFileAsync(filePath),
            _ => await File.ReadAllTextAsync(filePath)
        };
    }

    /// <summary>
    /// Simple EML file parser (basic implementation).
    /// </summary>
    private async Task<string> ParseEmlFileAsync(string filePath)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        var bodyStarted = false;
        var body = new System.Text.StringBuilder();
        var subject = "";

        foreach (var line in lines)
        {
            if (line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
            {
                subject = line.Substring(8).Trim();
            }
            else if (string.IsNullOrWhiteSpace(line) && !bodyStarted)
            {
                bodyStarted = true;
            }
            else if (bodyStarted)
            {
                body.AppendLine(line);
            }
        }

        return $"Subject: {subject}\n\n{body}";
    }

    /// <summary>
    /// Detects all applicable intents from a customer email using AI.
    /// </summary>
    public async Task<List<CustomerIntent>> DetectIntentsAsync(string emailContent)
    {
        var prompt = $"""
            Analyze the following customer request and detect ALL applicable intents.
            IMPORTANT: An email can have MULTIPLE intents. You MUST list ALL that apply.
            
            Possible intents:
            - MeterReadingSubmission: Customer provides a meter reading number (kWh, Zählerstand)
            - ProductInfoRequest: Customer asks about products, tariffs, pricing (dynamic tariff, hourly prices, green energy, Ökostrom, suitable for household)
            - AddressChange: Customer wants to change address (move, relocation, Umzug)
            - PaymentChange: Customer wants to change payment details (IBAN, bank)
            - ContractTermination: Customer wants to cancel contract
            - Complaint: Customer has a complaint
            - BillingInquiry: Customer has a question about billing
            - TariffChange: Customer wants to change tariff
            - CallbackRequest: Customer requests a callback
            - GeneralInquiry: General inquiry without specific request
            
            EXAMPLES:
            - "My meter reading is 2438 kWh. Also, is the dynamic tariff suitable for my household?" 
              → "MeterReadingSubmission, ProductInfoRequest"
            - "I want to submit my meter reading: 1500 kWh"
              → "MeterReadingSubmission"
            - "Can you tell me about your green energy options?"
              → "ProductInfoRequest"
            IMPORTANT: Focus primarily on the "LATEST MESSAGE BODY". 
            The "SUBJECT/TOPIC" provides context, but if it starts with "Re:" or "Aw:", 
            it likely reflects a previous inquiry. Do NOT detect "ProductInfoRequest" 
            just because it's in a "Re:" subject line if the body doesn't ask a new question.

            Customer request:
            {emailContent}
            
            Respond with a comma-separated list of ALL detected intents.
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var responseText = result.GetValue<string>()?.Trim() ?? "GeneralInquiry";
            
            var intents = new List<CustomerIntent>();
            var parts = responseText.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                var intentStr = part.Trim();
                // Remove potential numbering like "1. "
                if (intentStr.Contains(" ")) 
                    intentStr = intentStr.Split(' ').Last();
                    
                if (Enum.TryParse<CustomerIntent>(intentStr, true, out var intent))
                {
                    intents.Add(intent);
                }
                else
                {
                    // Try to match partial strings for robust fallback
                    intents.Add(MatchIntent(intentStr));
                }
            }
            
            return intents.Distinct().ToList();
        }
        catch
        {
            // Fallback to keyword-based detection (single intent wrapper)
            return new List<CustomerIntent> { DetectIntentByKeywords(emailContent) };
        }
    }

    /// <summary>
    /// Matches an intent string to the enum.
    /// </summary>
    private CustomerIntent MatchIntent(string intentString)
    {
        var lower = intentString.ToLowerInvariant();

        if (lower.Contains("meter") || lower.Contains("zähler"))
            return CustomerIntent.MeterReadingSubmission;
        if (lower.Contains("product") || lower.Contains("produkt") || lower.Contains("tarif"))
            return CustomerIntent.ProductInfoRequest;
        if (lower.Contains("address") || lower.Contains("adresse") || lower.Contains("umzug"))
            return CustomerIntent.AddressChange;
        if (lower.Contains("payment") || lower.Contains("zahlung") || lower.Contains("bank"))
            return CustomerIntent.PaymentChange;
        if (lower.Contains("termin") || lower.Contains("kündig"))
            return CustomerIntent.ContractTermination;
        if (lower.Contains("complaint") || lower.Contains("beschwer"))
            return CustomerIntent.Complaint;
        if (lower.Contains("bill") || lower.Contains("rechnung"))
            return CustomerIntent.BillingInquiry;
        if (lower.Contains("callback") || lower.Contains("rückruf"))
            return CustomerIntent.CallbackRequest;

        return CustomerIntent.GeneralInquiry;
    }

    /// <summary>
    /// Fallback keyword-based intent detection (bilingual: German + English).
    /// </summary>
    private CustomerIntent DetectIntentByKeywords(string content)
    {
        var lower = content.ToLowerInvariant();

        // Meter Reading
        if (lower.Contains("zählerstand") || lower.Contains("ablesung") || 
            lower.Contains("meter reading") || lower.Contains("kwh"))
            return CustomerIntent.MeterReadingSubmission;
            
        // Contract Termination
        if (lower.Contains("kündigen") || lower.Contains("kündigung") || lower.Contains("vertrag beenden") ||
            lower.Contains("cancel") || lower.Contains("terminate") || lower.Contains("termination"))
            return CustomerIntent.ContractTermination;
            
        // Address Change
        if (lower.Contains("umzug") || lower.Contains("neue adresse") || lower.Contains("adressänderung") ||
            lower.Contains("move") || lower.Contains("moved") || lower.Contains("address change") || 
            lower.Contains("update my address") || lower.Contains("new address"))
            return CustomerIntent.AddressChange;
            
        // Payment Change
        if (lower.Contains("bankverbindung") || lower.Contains("iban") || lower.Contains("zahlungsart") ||
            lower.Contains("payment") || lower.Contains("bank details"))
            return CustomerIntent.PaymentChange;
            
        // Billing Inquiry
        if (lower.Contains("rechnung") || lower.Contains("abrechnung") || lower.Contains("mahnung") ||
            lower.Contains("bill") || lower.Contains("billing") || lower.Contains("invoice"))
            return CustomerIntent.BillingInquiry;
            
        // Complaint
        if (lower.Contains("beschwerde") || lower.Contains("unzufrieden") || lower.Contains("ärgerlich") ||
            lower.Contains("complaint") || lower.Contains("unhappy") || lower.Contains("dissatisfied"))
            return CustomerIntent.Complaint;
            
        // Tariff Change
        if (lower.Contains("tarifwechsel") || lower.Contains("anderen tarif") || lower.Contains("wechseln") ||
            lower.Contains("tariff change") || lower.Contains("switch tariff") || lower.Contains("change tariff"))
            return CustomerIntent.TariffChange;
            
        // Callback Request
        if (lower.Contains("rückruf") || lower.Contains("anrufen") || lower.Contains("zurückrufen") ||
            lower.Contains("callback") || lower.Contains("call back") || lower.Contains("call me"))
            return CustomerIntent.CallbackRequest;
            
        // Product Info
        if (lower.Contains("produkt") || lower.Contains("angebot") || lower.Contains("information") ||
            lower.Contains("product") || lower.Contains("offer") || lower.Contains("tariff info"))
            return CustomerIntent.ProductInfoRequest;

        return CustomerIntent.GeneralInquiry;
    }

    /// <summary>
    /// Extracts relevant entities from the email based on the detected intent.
    /// </summary>
    public async Task<Dictionary<string, string>> ExtractEntitiesAsync(string emailContent, CustomerIntent intent, string traceId)
    {
        var prompt = $"""
            Extract the following information from the customer request (if available):
            - contractNumber: Contract number (Format: LB-XXXX-XXXXX)
            - fullName: Full customer name (check email signature like "Best, J. Meyer" or "Thanks, Julia M")
            - birthday: Date of birth (Format: DD.MM.YYYY)
            - street: Street and house number
            - postalCode: Postal code
            - city: City
            - meterNumber: Meter number (Format: LB-XXXXXXX)
            - meterReading: Meter reading (if mentioned)
            - newAddress: New address (for moves)
            - iban: IBAN (for payment changes)
            - monthlyPayment: Monthly installment amount (e.g. "50,-" or "85€")
            
            IMPORTANT: For fullName, also check email closings/signatures like:
            - "Best, J. Meyer" → extract "J. Meyer"
            - "Thanks, Julia M" → extract "Julia M"
            - "Grüße, Thomas Schmidt" → extract "Thomas Schmidt"
            
            Customer request:
            {emailContent}
            
            Respond in the format:
            fieldname: value
            
            If a field was not found, omit it.
            """;

        var entities = new Dictionary<string, string>();

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var output = result.GetValue<string>() ?? "";

            // Parse the response
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim().Replace("-", "");
                    var value = parts[1].Trim();
                    if (!string.IsNullOrWhiteSpace(value) && value != "nicht gefunden" && value != "nicht angegeben")
                    {
                        entities[key] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log LLM error if needed, but continue to regex
            _logger.LogError(traceId, "Extraction", $"LLM Entity Extraction failed: {ex.Message}. Falling back to regex.");
        }

        // Always attempt regex extraction as a backup/validation
        var regexEntities = ExtractEntitiesByRegex(emailContent);
        foreach (var kvp in regexEntities)
        {
            if (!entities.ContainsKey(kvp.Key))
            {
                entities[kvp.Key] = kvp.Value;
            }
        }

        return entities;
    }

    /// <summary>
    /// Fallback regex-based entity extraction.
    /// </summary>
    private Dictionary<string, string> ExtractEntitiesByRegex(string content)
    {
        var entities = new Dictionary<string, string>();

        // Contract number pattern: LB-XXXX-XXXXX
        var contractMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"LB-\d{4}-\d{5}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (contractMatch.Success)
            entities["contractNumber"] = contractMatch.Value.ToUpper();

        // Meter number pattern: LB-XXXXXXX
        var meterNumberMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"LB-\d{7}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (meterNumberMatch.Success)
            entities["meterNumber"] = meterNumberMatch.Value.ToUpper();

        // German date pattern (birthday typically follows "Geburtsdatum:" or similar)
        var birthdayMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"(?:Geburtsdatum|geboren|Geb\.?)\s*:?\s*(\d{1,2}\.\d{1,2}\.\d{4})", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (birthdayMatch.Success)
            entities["birthday"] = birthdayMatch.Groups[1].Value;

        // Full name pattern - supports both explicit fields and email signatures
        // Pattern 1: Explicit "Name:" or "Mein Name:" fields
        var nameMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"(?:Mein Name|Name|Full name)[\s:]+([A-ZÄÖÜ][a-zäöüß]+\s+[A-ZÄÖÜ][a-zäöüß]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Pattern 2: Email signature closings (Best, J. Meyer / Thanks, Julia M / Grüße, Thomas Schmidt)
        if (!nameMatch.Success)
        {
            nameMatch = System.Text.RegularExpressions.Regex.Match(
                content, @"(?:Best|Thanks|Grüße|Danke|Regards|Gruß),?\s+([A-Z]\.?\s+[A-ZÄÖÜ][a-zäöüß]+|[A-ZÄÖÜ][a-zäöüß]+\s+[A-Z]\.?|[A-ZÄÖÜ][a-zäöüß]+\s+[A-ZÄÖÜ][a-zäöüß]+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        if (nameMatch.Success)
            entities["fullName"] = nameMatch.Groups[1].Value.Trim();

        // Postal code with city pattern (5 digits followed by city name)
        var postalCityMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"\b(\d{5})\s+([A-ZÄÖÜ][a-zäöüß]+)\b");
        if (postalCityMatch.Success)
        {
            entities["postalCode"] = postalCityMatch.Groups[1].Value;
            entities["city"] = postalCityMatch.Groups[2].Value;
        }

        // Street address pattern (street name + house number)
        var streetMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"([A-ZÄÖÜ][a-zäöüß]+(?:straße|weg|allee|platz|gasse|ring))\s+(\d+[a-z]?)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (streetMatch.Success)
            entities["street"] = $"{streetMatch.Groups[1].Value} {streetMatch.Groups[2].Value}";

        // Meter reading (number with kWh, typically 4-8 digits)
        var meterMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"(?:Zählerstand|Stand|Aktueller)\s*:?\s*(\d{4,8})\s*(?:kWh)?", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (meterMatch.Success)
            entities["meterReading"] = meterMatch.Groups[1].Value;

        // Monthly payment / installment amount (Case Study: "50,-", "85€", "Abschlag: 50")
        var paymentMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"(?:Abschlag|installment|monatlich|Zahlung)\s*(?:amount|betrag)?\s*(?:is|ist|:)?\s*(\d+)\s*(?:,-|€|EUR)?", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (paymentMatch.Success)
            entities["monthlyPayment"] = paymentMatch.Groups[1].Value;

        return entities;
    }

    /// <summary>
    /// Generates a response asking for missing authentication data.
    /// </summary>
    public async Task<string> GenerateAuthenticationRequestAsync(
        CustomerEmail email, 
        CustomerIntent intent, 
        AuthenticationResult authResult,
        string? partialProductInfo = null)
    {
        var missingFieldsList = string.Join("\n• ", authResult.MissingFields);
        var intentDescription = intent.GetDescription();
        
        // Check if we have extracted any info (like meter number)
        var extractedInfo = "";
        // Note: We'd need to pass more context here, but for now we reference the email

        var productInfoSection = "";
        if (!string.IsNullOrEmpty(partialProductInfo))
        {
            productInfoSection = $"""

                IMPORTANT: The customer ALSO asked about the dynamic tariff. Include this information in your response:
                {partialProductInfo}
                """;
        }

        var prompt = $"""
            You are an AI-powered customer service assistant for EnergyCo, a German green energy provider.
            Generate a professional email response following this EXACT style:

            REFERENCE EXAMPLE (from case study):
            ---
            Hello Ms. Meyer,

            I'm your AI-powered service assistant. To process your request, we need your customer number in addition to your meter number (LB-9876543) and one of the following verification details:
            • Contract number
            • Full name of the contract holder
            • Customer address or service address
            • Current installment amount

            Simply reply to this email, and we will get back to you promptly to assist you.

            Additionally, here are some information on our Dynamic Tariff:
            Our dynamic electricity tariff reflects wholesale market prices, which may vary throughout the day.
            • Who benefits: Customers who can shift certain energy usage to lower-price periods
            • Requirements: A compatible smart meter is generally necessary
            • Switching process: You may request a tariff change via the checkout or customer portal

            Kind Regards
            ---

            NOW GENERATE A RESPONSE FOR THIS SITUATION:
            - Request type: {intentDescription}
            - Original subject: {email.Subject}
            - Missing verification data: {string.Join(", ", authResult.MissingFields)}
            - Additional data points needed: {authResult.PointsNeeded}
            {productInfoSection}

            RULES:
            1. Start with "Hello [Name]," or "Dear [Name]," if name is available, otherwise "Dear Customer,"
            2. Say "I'm your AI-powered service assistant." in the first sentence.
            3. State explicitly that we need exactly {authResult.PointsNeeded} more information(s) to process the request.
            4. LIST the verification options as bullet points (•) and use the EXACT phrase:
               "Please provide {authResult.PointsNeeded} of the following:"
               • Contract number
               • Full name of the contract holder  
               • Customer address or service address
               • Current installment amount
            5. Say "Simply reply to this email, and we will get back to you promptly to assist you."
            6. If product info about dynamic tariff should be included, add it with "Additionally, here are some information on our Dynamic Tariff:"
            7. End with just "Kind Regards" (nothing after)
            8. Write ENTIRELY in English.
            9. Do NOT include "Subject:" line.
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.GetValue<string>() ?? GenerateDefaultAuthRequest(email, authResult, partialProductInfo);
        }
        catch
        {
            return GenerateDefaultAuthRequest(email, authResult, partialProductInfo);
        }
    }

    /// <summary>
    /// Generates a default authentication request email.
    /// </summary>
    private string GenerateDefaultAuthRequest(CustomerEmail email, AuthenticationResult authResult, string? partialProductInfo = null)
    {
        var missingFields = string.Join("\n- ", authResult.MissingFields);
        var productInfoSection = "";
        
        if (!string.IsNullOrEmpty(partialProductInfo))
        {
            productInfoSection = $"\n\nWe are happy to answer your question in advance:\n\n{partialProductInfo}";
        }

        return $"""
            Subject: Re: {email.Subject}
            
            Dear Sir or Madam,
            
            Thank you for your message.
            
            To process your request, we require some additional information for 
            verification purposes for data protection reasons:
            
            - {missingFields}
            
            Please send us this information so we can process your request 
            as quickly as possible.{productInfoSection}
            
            Best regards,
            Your EnergyCo Customer Service
            """;
    }

    /// <summary>
    /// Executes actions based on the intent and extracted data.
    /// </summary>
    private async Task ExecuteActionsAsync(ProcessingState state, ConversationSession? session, CustomerIntent intent, string? emailBody = null)
    {
        switch (intent)
        {
            case CustomerIntent.MeterReadingSubmission:
                if (state.ExtractedEntities.TryGetValue("meterReading", out var reading))
                {
                    MeterReadingValidation validation;
                    DateTime? readingDate = null;
                    
                    // Try to extract reading date from email
                    if (state.ExtractedEntities.TryGetValue("readingDate", out var dateStr))
                    {
                        if (DateTime.TryParse(dateStr, out var parsedDate))
                        {
                            readingDate = parsedDate;
                        }
                    }
                    
                    // If we have authenticated customer data, do plausibility validation
                    if (state.IsAuthenticated && state.AuthenticatedCustomer != null)
                    {
                        validation = _meterReadingService.ValidateWithPlausibility(
                            reading, 
                            state.AuthenticatedCustomer, 
                            readingDate);
                    }
                    else
                    {
                        // Basic validation only
                        validation = _meterReadingService.ValidateReading(reading);
                    }
                    
                    // Store validation details in entities for response generation
                    state.ExtractedEntities["validationMessage"] = validation.Message;
                    state.ExtractedEntities["consumption"] = validation.Consumption.ToString();
                    state.ExtractedEntities["expectedConsumption"] = validation.ExpectedConsumption.ToString();
                    
                    // CORRECTION HANDLING: Check if this is a response to a previous warning
                    var pendingWarning = session?.PendingActions
                        .FirstOrDefault(a => a.ActionType == "meter_reading_confirmation" && !a.IsResolved);
                        
                    if (pendingWarning != null && session != null)
                    {
                        var oldReadingStr = pendingWarning.ActionData.GetValueOrDefault("reading", "0");
                        if (long.TryParse(oldReadingStr, out var oldReading) && validation.Reading != oldReading)
                        {
                            // Customer corrected the reading -> Force success
                            _sessionManager.ResolvePendingAction(session, "meter_reading_confirmation");
                            state.Actions.Add("correction_accepted");
                            state.Actions.Add($"meter_reading_recorded:{reading}");
                            
                            if (validation.Consumption > 0)
                                state.Actions.Add($"consumption:{validation.Consumption}kWh");
                                
                            // Skip standard validation errors logic below
                            return; 
                        }
                    }
                    
                    // IMPLICIT CORRECTION: Only trigger if:
                    // 1. Current email has a NEW reading different from the one in session
                    // 2. No pending plausibility warning exists
                    // 3. The new reading is valid
                    if (session != null && validation.IsValid && !validation.IsPlausibilityWarning)
                    {
                        // Check if there's already a pending warning - if so, don't auto-close
                        var hasPendingWarning = session.PendingActions
                            .Any(a => a.ActionType == "meter_reading_confirmation" && !a.IsResolved);
                        
                        if (hasPendingWarning)
                        {
                            // There's a pending warning - this email should resolve it
                            // Only accept as correction if the reading is DIFFERENT
                            var existingWarning = session.PendingActions
                                .First(a => a.ActionType == "meter_reading_confirmation" && !a.IsResolved);
                            var previousReadingStr = existingWarning.ActionData.GetValueOrDefault("reading", "0");
                            
                            if (long.TryParse(previousReadingStr, out var previousReading) && 
                                validation.Reading != previousReading)
                            {
                                // Customer sent a DIFFERENT reading - accept as correction
                                _sessionManager.ResolvePendingAction(session, "meter_reading_confirmation");
                                state.Actions.Add("correction_accepted");
                                state.Actions.Add($"meter_reading_recorded:{reading}");
                                
                                if (validation.Consumption > 0)
                                    state.Actions.Add($"consumption:{validation.Consumption}kWh");
                                    
                                return;
                            }
                            // Same reading as before - let the warning flow handle it
                        }
                        // No pending warning and reading is valid - this is a normal valid reading
                        // Don't mark as correction unless it's actually different from a previous reading
                    }

                    if (!validation.IsValid)
                    {
                        // Invalid reading - flag for review but don't block response generation
                        state.RequiresHumanReview = true;
                        state.HumanReviewReason = validation.Message;
                        state.Actions.Add($"meter_reading_invalid:{reading}");
                    }
                    else if (validation.IsPlausibilityWarning)
                    {
                        // Check if this is a correction that was already accepted
                        if (state.Actions.Contains("correction_accepted"))
                        {
                            // SUPPRESS WARNING: Customer explicitly corrected/confirmed this reading
                            state.Actions.Add("plausibility_warning_suppressed_due_to_correction");
                            state.Actions.Add($"meter_reading_recorded:{reading}");
                            if (validation.Consumption > 0)
                            {
                                state.Actions.Add($"consumption:{validation.Consumption}kWh");
                            }
                        }
                        else
                        {
                            // Plausibility warning - needs customer confirmation
                            state.RequiresHumanReview = true;
                            state.HumanReviewReason = validation.Message;
                            state.Actions.Add($"meter_reading_plausibility_warning:{reading}");
                            state.Actions.Add($"consumption_delta:{validation.Consumption}kWh");
                            state.Actions.Add($"expected_consumption:{validation.ExpectedConsumption}kWh");
                            
                            // Register pending action for smart correction detection next time
                            if (session != null)
                            {
                                var actionData = new Dictionary<string, string> { { "reading", reading } };
                                _sessionManager.AddPendingAction(session, "meter_reading_confirmation", state.EmailId, actionData);
                            }
                        }
                    }
                    else if (validation.Level == ValidationLevel.Warning)
                    {
                        // Other warning - flag for review but still record
                        state.RequiresHumanReview = true;
                        state.HumanReviewReason = validation.Message;
                        state.Actions.Add($"meter_reading_warning:{reading}");
                    }
                    else
                    {
                        // Valid reading
                        state.Actions.Add($"meter_reading_recorded:{reading}");
                        if (validation.Consumption > 0)
                        {
                            state.Actions.Add($"consumption:{validation.Consumption}kWh");
                        }
                    }
                }
                break;

            case CustomerIntent.AddressChange:
                if (state.ExtractedEntities.TryGetValue("newAddress", out var address))
                {
                    state.Actions.Add($"address_change_requested:{address}");
                }
                break;
                
            case CustomerIntent.ProductInfoRequest:
                if (!string.IsNullOrWhiteSpace(emailBody))
                {
                    var answer = await _productInfoAgent.AnswerQuestionAsync(emailBody, state.AuthenticatedCustomer);
                    state.Actions.Add("product_info_provided");
                    // Store answer to be included in response
                    state.ExtractedEntities["ProductInfoResponse"] = answer; 
                }
                break;

            case CustomerIntent.ContractTermination:
                state.RequiresHumanReview = true;
                state.HumanReviewReason = "Contract termination requires manual review.";
                state.Actions.Add("termination_flagged_for_review");
                break;

            case CustomerIntent.Complaint:
                state.RequiresHumanReview = true;
                state.HumanReviewReason = "Complaint requires personal handling.";
                state.Actions.Add("complaint_escalated");
                break;

            default:
                state.Actions.Add($"intent_processed:{intent}");
                break;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Generates a response email based on the processing state.
    /// </summary>
    public async Task<string> GenerateResponseAsync(
        CustomerEmail email, 
        ProcessingState state, 
        CustomerIntent intent,
        ConversationSession? session = null)
    {
        // Get customer name: prefer authenticated, then session data, then fallback
        var customerName = state.AuthenticatedCustomer?.FullName 
            ?? session?.CollectedAuthData.GetValueOrDefault("fullName")
            ?? "Customer";
        var salutation = GetProperSalutation(customerName);
        var intentDescription = intent.GetDescription();

        // Check if tariff info was already sent in this session
        bool tariffAlreadySent = session?.EmailHistory.Any(e => 
            e.DetectedIntent != null && e.DetectedIntent.Contains("ProductInfoRequest")) == true;

        // If we are providing it NOW, suppress the "already sent" flag to avoid AI confusion/hallucination
        if (state.Actions.Contains("product_info_provided"))
        {
            tariffAlreadySent = false;
        }

        var authRequired = intent.RequiresAuthentication();

        // Build context for the response
        var contextParts = new List<string>();
        
        if (authRequired && !state.IsAuthenticated)
        {
            contextParts.Add("Customer not authenticated - need additional verification details for this request");
        }
        else if (!authRequired)
        {
            contextParts.Add("Authentication is NOT required for this specific request");
        }

        if (state.Actions.Contains("meter_reading_recorded"))
        {
            var reading = state.ExtractedEntities.GetValueOrDefault("meterReading", "");
            contextParts.Add($"Meter reading recorded: {reading} kWh");
        }
        if (state.Actions.Contains("correction_accepted"))
        {
            contextParts.Add("CASE CLOSED: Customer CORRECTED the meter reading. Confirm success and state 'Your request is now completed.'");
        }
        if (state.Actions.Any(a => a.Contains("plausibility_warning")))
        {
            contextParts.Add("High consumption detected - needs customer confirmation");
        }
        if (state.Actions.Contains("product_info_provided"))
        {
            var productInfoResult = state.ExtractedEntities.GetValueOrDefault("ProductInfoResponse", "");
            contextParts.Add($"PRODUCT INFORMATION TO PROVIDE:\n{productInfoResult}");
        }
        if (tariffAlreadySent && intent == CustomerIntent.ProductInfoRequest)
        {
            contextParts.Add("Current Session State: Tariff information for ÖkoStrom Dynamic has already been successfully communicated to this customer in this session. Do not repeat unless providing updated or specifically requested information.");
        }

        var prompt = $"""
            You are an AI-powered customer service assistant for EnergyCo, a German green energy provider.
            Generate a professional email response following this EXACT style and structure:

            EXAMPLE FORMAT:
            ---
            Dear Ms. Meyer,

            This is your AI-powered service assistant. [Main message here]

            [If listing options or reasons, use bullet points:]
            • First item
            • Second item
            • Third item

            Simply reply to this email, and we will get back to you promptly to assist you.

            Kind Regards
            ---

            CONTEXT FOR THIS RESPONSE:
            - Customer name: {customerName}
            - Salutation to use: {salutation}
            - Customer request type: {intentDescription}
            - Original email subject: {email.Subject}
            - Actions performed: {string.Join(", ", state.Actions)}
            - Context: {string.Join("; ", contextParts)}
            - Authenticated: {state.IsAuthenticated}
            - Requires Authentication for this intent: {authRequired}
            - Requires human review: {(state.RequiresHumanReview ? "Yes - " + state.HumanReviewReason : "No")}

            RULES:
            1. Start with "{salutation}," on its own line
            2. Second line: "This is your AI-powered service assistant."
            3. Be concise but helpful
            4. Use bullet points (•) for lists
            5. ONLY if "Requires Authentication" is True AND "Authenticated" is False: List what verification details are needed (contract number, full name, address, or installment amount). Otherwise, DO NOT ask for verification details.
            6. STRICT: If context says "Tariff information ... already ... communicated" AND "product_info_provided" is NOT in actions, DO NOT mention dynamic tariff, hourly prices, or smart meters. Do NOT tell the customer that you have already sent this information. Simply ignore it and focus on the current task.
            7. If "PRODUCT INFORMATION TO PROVIDE" is in the context: Use that SPECIFIC information to answer the customer's questions accurately. Professional and friendly tone.
            8. If meter reading was recorded with high consumption, ask customer to confirm or explain
            9. If correction_accepted is in actions: Confirm the reading was successfully recorded and say "Your request is now completed." Do NOT repeat warnings or tariff info.
            10. Before "Kind Regards": Add "Simply reply to this email, and we will get back to you promptly to assist you."
            11. End with "Kind Regards" (NO company name after)
            12. Write ENTIRELY in English
            13. Do NOT include "Subject:" line - just the email body
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.GetValue<string>() ?? GenerateDefaultResponse(email, state, intent);
        }
        catch
        {
            return GenerateDefaultResponse(email, state, intent);
        }
    }

    /// <summary>
    /// Generates a default response when AI is unavailable.
    /// </summary>
    private string GenerateDefaultResponse(CustomerEmail email, ProcessingState state, CustomerIntent intent)
    {
        var customerName = state.AuthenticatedCustomer?.FullName ?? "Customer";

        // Check if this is a plausibility warning case
        if (intent == CustomerIntent.MeterReadingSubmission && 
            state.Actions.Any(a => a.Contains("plausibility_warning")))
        {
            return GeneratePlausibilityWarningEmail(email, state);
        }

        return $"""
            Subject: Re: {email.Subject}
            
            Dear {customerName},
            
            Thank you for your message regarding: {intent.GetDescription()}.
            
            We have received your request and are processing it.
            {(state.RequiresHumanReview ? "\nA staff member will contact you shortly." : "")}
            
            Best regards,
            Your EnergyCo Customer Service
            """;
    }

    /// <summary>
    /// Generates a plausibility warning email asking customer to confirm high consumption.
    /// </summary>
    private string GeneratePlausibilityWarningEmail(CustomerEmail email, ProcessingState state)
    {
        var customerName = state.AuthenticatedCustomer?.FullName ?? "Customer";
        var lastReading = state.AuthenticatedCustomer?.LastMeterReading ?? 0;
        var lastReadingDate = state.AuthenticatedCustomer?.LastMeterReadingDate ?? DateTime.MinValue;
        
        state.ExtractedEntities.TryGetValue("meterReading", out var currentReading);
        state.ExtractedEntities.TryGetValue("consumption", out var consumption);
        state.ExtractedEntities.TryGetValue("expectedConsumption", out var expectedConsumption);

        return $"""
            Subject: Re: {email.Subject} - Confirmation Required
            
            Dear {customerName},
            
            Thank you for submitting your meter reading.
            
            During our review, we noticed that the reported consumption is higher than expected:
            
            ┌─────────────────────────────────────────────────────┐
            │  Last meter reading:      {lastReading,10:N0} kWh  ({lastReadingDate:dd.MM.yyyy})
            │  Reported meter reading:  {currentReading,10} kWh
            │  ───────────────────────────────────────────────────
            │  Calculated consumption:  {consumption,10} kWh
            │  Expected consumption:    ~{expectedConsumption,9} kWh
            └─────────────────────────────────────────────────────┘
            
            Possible reasons for increased consumption may include:
            • New electrical appliances (e.g. heat pump, air conditioning, electric car)
            • More people in the household
            • Changed usage habits
            • Defective devices with high consumption
            
            Please choose one of the following options:
            
            ✓ If the meter reading is correct, please confirm this briefly 
              via email and let us know the reason for the increased 
              consumption if applicable.
            
            ✗ If you made a typo, please send us the correct meter reading.
            
            If you have any questions, we are happy to assist you.
            
            Best regards,
            Your EnergyCo Customer Service
            
            ---
            Note: This verification serves your protection to avoid incorrect billing.
            """;
    }

    /// <summary>
    /// Generates proper salutation based on customer name with gender detection.
    /// </summary>
    private string GetProperSalutation(string customerName)
    {
        if (customerName == "Customer" || string.IsNullOrWhiteSpace(customerName))
        {
            return "Dear Customer";
        }

        var nameParts = customerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length < 2)
        {
            return $"Dear {customerName}";
        }

        var firstName = nameParts[0];
        var lastName = nameParts[^1];

        // Common German female first names (ends with -a, -e, or specific names)
        var femaleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Julia", "Anna", "Maria", "Laura", "Sophie", "Emma", "Mia", "Lea", "Lena",
            "Sarah", "Lisa", "Nina", "Tina", "Sandra", "Petra", "Claudia", "Andrea",
            "Christina", "Stefanie", "Stephanie", "Nicole", "Sabine", "Karin", "Heike",
            "Susanne", "Monika", "Martina", "Birgit", "Barbara", "Silke", "Anja"
        };

        // Common German male first names
        var maleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Thomas", "Michael", "Andreas", "Peter", "Stefan", "Christian", "Martin",
            "Frank", "Klaus", "Hans", "Jürgen", "Markus", "Marcus", "Daniel", "Alexander",
            "Matthias", "Oliver", "Sebastian", "Jan", "Tim", "Lukas", "Felix", "Max"
        };

        if (femaleNames.Contains(firstName))
        {
            return $"Dear Ms. {lastName}";
        }
        else if (maleNames.Contains(firstName))
        {
            return $"Dear Mr. {lastName}";
        }
        else
        {
            // Fallback: use Hello with full name for unknown gender
            return $"Hello {customerName}";
        }
    }
}
