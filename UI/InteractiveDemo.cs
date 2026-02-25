using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CustomerContactCopilot.Models;
using CustomerContactCopilot.Orchestrator;
using CustomerContactCopilot.Services;
using CustomerContactCopilot.Utils;

namespace CustomerContactCopilot.UI;

public class InteractiveDemo
{
    private readonly EmailOrchestrator _orchestrator;
    private readonly SessionStateManager _sessionManager;
    private readonly string _testEmailsPath;
    private readonly string _modelId;

    public InteractiveDemo(EmailOrchestrator orchestrator, SessionStateManager sessionManager, string modelId)
    {
        _orchestrator = orchestrator;
        _sessionManager = sessionManager;
        _modelId = modelId;
        
        _testEmailsPath = Path.Combine(Directory.GetCurrentDirectory(), "TestEmails");
        if (!Directory.Exists(_testEmailsPath))
        {
            _testEmailsPath = Path.Combine(AppContext.BaseDirectory, "TestEmails");
        }
    }

    public async Task StartAsync()
    {
        while (true)
        {
            Console.Clear();
            DrawHeader();
            DrawMainMenu();
            
            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1":
                    await RunCaseStudyAsync();
                    break;
                case "2":
                    await RunMultiturnDemoAsync();
                    break;
                case "3":
                    await RunIndividualTestMenuAsync();
                    break;
                case "4":
                    ClearAllSessions();
                    break;
                case "5":
                case "q":
                case "Q":
                    ConsoleHelpers.WriteColor("\n👋 Auf Wiedersehen!\n", ConsoleColor.Cyan);
                    return;
                default:
                    ConsoleHelpers.WriteColor("\n⚠️ Ungültige Auswahl. Bitte 1-5 eingeben.", ConsoleColor.Yellow);
                    Thread.Sleep(1500);
                    break;
            }
        }
    }

    private void DrawHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     EnergyCo Customer Contact Copilot                      ║");
        Console.WriteLine("║     Interactive Demo Mode                                    ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        ConsoleHelpers.WriteColor($"🤖 Model: {_modelId}", ConsoleColor.DarkGray);
        ConsoleHelpers.WriteColor($"📂 Test Emails: {Path.GetFileName(_testEmailsPath)}/", ConsoleColor.DarkGray);
        ConsoleHelpers.WriteColor($"🔐 Active Sessions: {_sessionManager.GetActiveSessions().Count}", ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    private void DrawMainMenu()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  [1] 📧 Case Study (Julia Meyer) - 3 sequential emails");
        Console.WriteLine("  [2] 🔄 Multiturn Session Demo - with session persistence");
        Console.WriteLine("  [3] 📁 Individual Test Emails - select single file");
        Console.WriteLine("  [4] 🗑️  Clear All Sessions - reset for fresh demo");
        Console.WriteLine("  [5] ❌ Exit");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.Write("\n  Select option (1-5): ");
    }

    private async Task RunCaseStudyAsync()
    {
        Console.Clear();
        ConsoleHelpers.WriteColor("\n═══════════════════════════════════════════════════════════════", ConsoleColor.Cyan);
        ConsoleHelpers.WriteColor("📧 CASE STUDY: Julia Meyer - Meter Reading & Dynamic Tariff", ConsoleColor.Cyan);
        ConsoleHelpers.WriteColor("═══════════════════════════════════════════════════════════════\n", ConsoleColor.Cyan);
        
        _sessionManager.ClearAllSessions();
        ConsoleHelpers.WriteColor("🔄 Sessions cleared for fresh start\n", ConsoleColor.DarkGray);
        
        var caseStudyPath = Path.Combine(_testEmailsPath, "case_study");
        var files = Directory.GetFiles(caseStudyPath, "*.txt").OrderBy(f => f).ToArray();
        
        foreach (var file in files)
        {
            await ProcessEmailWithStructuredOutputAsync(file);
            
            ConsoleHelpers.WriteColor("\n  Press ENTER to continue to next email...", ConsoleColor.DarkGray);
            Console.ReadLine();
        }
        
        ConsoleHelpers.WriteColor("\n✅ Case Study Complete!", ConsoleColor.Green);
        ShowSessionSummary();
        
        ConsoleHelpers.WriteColor("\n  Press ENTER to return to menu...", ConsoleColor.DarkGray);
        Console.ReadLine();
    }

    private async Task RunMultiturnDemoAsync()
    {
        Console.Clear();
        ConsoleHelpers.WriteColor("\n═══════════════════════════════════════════════════════════════", ConsoleColor.Cyan);
        ConsoleHelpers.WriteColor("🔄 MULTITURN SESSION DEMO - Session Persistence Test", ConsoleColor.Cyan);
        ConsoleHelpers.WriteColor("═══════════════════════════════════════════════════════════════\n", ConsoleColor.Cyan);
        
        ConsoleHelpers.WriteColor("This demo shows how session state persists across multiple emails.", ConsoleColor.DarkGray);
        ConsoleHelpers.WriteColor("The customer 'Julia Meyer' sends 3 emails - auth builds up over time.\n", ConsoleColor.DarkGray);
        
        _sessionManager.ClearAllSessions();
        
        var multiturnPath = Path.Combine(_testEmailsPath, "multiturn");
        var files = Directory.GetFiles(multiturnPath, "*.txt").OrderBy(f => f).ToArray();
        
        foreach (var file in files)
        {
            await ProcessEmailWithStructuredOutputAsync(file);
            
            ShowSessionSummary();
            
            ConsoleHelpers.WriteColor("\n  Press ENTER to continue...", ConsoleColor.DarkGray);
            Console.ReadLine();
        }
        
        ConsoleHelpers.WriteColor("\n✅ Multiturn Demo Complete!", ConsoleColor.Green);
        ConsoleHelpers.WriteColor("\n  Press ENTER to return to menu...", ConsoleColor.DarkGray);
        Console.ReadLine();
    }

    private async Task RunIndividualTestMenuAsync()
    {
        while (true)
        {
            Console.Clear();
            ConsoleHelpers.WriteColor("\n═══════════════════════════════════════════════════════════════", ConsoleColor.Cyan);
            ConsoleHelpers.WriteColor("📁 INDIVIDUAL TEST EMAILS", ConsoleColor.Cyan);
            ConsoleHelpers.WriteColor("═══════════════════════════════════════════════════════════════\n", ConsoleColor.Cyan);
            
            var files = Directory.GetFiles(_testEmailsPath, "*.txt", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f).ToArray();
            
            for (int i = 0; i < files.Length; i++)
            {
                Console.WriteLine($"  [{i + 1}] {Path.GetFileName(files[i])}");
            }
            Console.WriteLine($"\n  [0] ← Back to Main Menu");
            Console.Write("\n  Select file (0-{0}): ", files.Length);
            
            var input = Console.ReadLine()?.Trim();
            if (input == "0" || string.IsNullOrEmpty(input)) return;
            
            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= files.Length)
            {
                await ProcessEmailWithStructuredOutputAsync(files[choice - 1]);
                ConsoleHelpers.WriteColor("\n  Press ENTER to continue...", ConsoleColor.DarkGray);
                Console.ReadLine();
            }
        }
    }

    private void ClearAllSessions()
    {
        _sessionManager.ClearAllSessions();
        ConsoleHelpers.WriteColor("\n🗑️ All sessions cleared!", ConsoleColor.Green);
        Thread.Sleep(1500);
    }

    private async Task ProcessEmailWithStructuredOutputAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        Console.WriteLine();
        ConsoleHelpers.WriteColor("┌─────────────────────────────────────────────────────────────────┐", ConsoleColor.DarkGray);
        ConsoleHelpers.WriteColor($"│ 📧 Processing: {fileName,-49} │", ConsoleColor.White);
        ConsoleHelpers.WriteColor("├─────────────────────────────────────────────────────────────────┤", ConsoleColor.DarkGray);
        
        var content = await File.ReadAllTextAsync(filePath);
        var email = EmailParser.ParseEmailFile(content, filePath);
        
        // Show email preview
        ConsoleHelpers.WriteColor("│ 📥 EMAIL CONTENT                                                │", ConsoleColor.DarkGray);
        ConsoleHelpers.WriteColor($"│    From: {email.FromAddress,-54} │", ConsoleColor.White);
        ConsoleHelpers.WriteColor($"│    Subject: {ConsoleHelpers.TruncateString(email.Subject ?? "(no subject)", 50),-50} │", ConsoleColor.White);
        ConsoleHelpers.WriteColor("├─────────────────────────────────────────────────────────────────┤", ConsoleColor.DarkGray);
        
        // TRIAGE PHASE
        ConsoleHelpers.WriteColor("│ 🔍 TRIAGE PHASE                                                 │", ConsoleColor.Cyan);
        
        var (state, session) = await _orchestrator.ProcessEmailWithSessionAsync(email);
        
        ConsoleHelpers.WriteColor($"│    Intent: {ConsoleHelpers.TruncateString(state.DetectedIntent ?? "Unknown", 52),-52} │", ConsoleColor.Cyan);
        var authRequired = state.DetectedIntent?.Contains("MeterReading") == true || 
                           state.DetectedIntent?.Contains("Address") == true ||
                           state.DetectedIntent?.Contains("Contract") == true;
        ConsoleHelpers.WriteColor($"│    Auth Required: {(authRequired ? "Yes" : "No"),-44} │", ConsoleColor.Cyan);
        
        ConsoleHelpers.WriteColor("├─────────────────────────────────────────────────────────────────┤", ConsoleColor.DarkGray);
        
        // AUTHENTICATION PHASE - Only show if authentication is required or already authenticated
        if (authRequired || session.IsAuthenticated)
        {
            ConsoleHelpers.WriteColor("│ 🔐 AUTHENTICATION PHASE                                         │", ConsoleColor.Yellow);
            
            var validatedPoints = state.AuthResult?.ValidatedFields ?? new List<string>();
            var entitiesStr = validatedPoints.Count > 0 ? string.Join(", ", validatedPoints) : "None";
            ConsoleHelpers.WriteColor($"│    Known Auth: {ConsoleHelpers.TruncateString(entitiesStr, 48),-48} │", ConsoleColor.Yellow);
            
            var neededStr = !session.IsAuthenticated && state.AuthResult != null ? $" ({state.AuthResult.PointsNeeded} points needed)" : "";
            ConsoleHelpers.WriteColor($"│    Session ID: {session.SessionId[..8]}...{((session.IsAuthenticated ? " ✓ Authenticated" : " ⏳ Pending" + neededStr)),-26} │", ConsoleColor.Yellow);

            if (session.IsAuthenticated)
            {
                ConsoleHelpers.WriteColor($"│    Customer: {session.AuthenticatedCustomerName,-49} │", ConsoleColor.Green);
            }
            
            ConsoleHelpers.WriteColor("├─────────────────────────────────────────────────────────────────┤", ConsoleColor.DarkGray);
        }
        
        // AGENT THOUGHTS
        ConsoleHelpers.WriteColor("│ 💭 AGENT THOUGHTS                                               │", ConsoleColor.Magenta);
        var thought = GenerateAgentThought(state, session);
        foreach (var line in ConsoleHelpers.WrapText(thought, 60))
        {
            ConsoleHelpers.WriteColor($"│    \"{line}\"", ConsoleColor.Magenta);
        }
        
        ConsoleHelpers.WriteColor("├─────────────────────────────────────────────────────────────────┤", ConsoleColor.DarkGray);
        
        // RESULT
        if (state.Errors.Any())
        {
            ConsoleHelpers.WriteColor("│ ❌ PROCESSING ERROR                                             │", ConsoleColor.Red);
            ConsoleHelpers.WriteColor($"│    {ConsoleHelpers.TruncateString(state.Errors.First(), 60),-60} │", ConsoleColor.Red);
            ConsoleHelpers.WriteColor("└─────────────────────────────────────────────────────────────────┘", ConsoleColor.DarkGray);
            return;
        }
        
        if (state.RequiresHumanReview)
        {
            ConsoleHelpers.WriteColor("│ ⚠️ PROCESSING COMPLETE - ATTENTION REQUIRED                     │", ConsoleColor.Yellow);
            ConsoleHelpers.WriteColor("└─────────────────────────────────────────────────────────────────┘", ConsoleColor.DarkGray);
            Console.WriteLine();
            ConsoleHelpers.WriteColor("📋 ATTENTION:", ConsoleColor.Yellow);
            ConsoleHelpers.WriteColor($"   {state.HumanReviewReason ?? "Manual review recommended"}", ConsoleColor.Yellow);
        }
        else
        {
            ConsoleHelpers.WriteColor("│ ✅ PROCESSING COMPLETE                                          │", ConsoleColor.Green);
            ConsoleHelpers.WriteColor($"│    Status: {(state.IsAuthenticated ? "Authenticated & Processed" : "Response Generated"),-52} │", ConsoleColor.Green);
            ConsoleHelpers.WriteColor("└─────────────────────────────────────────────────────────────────┘", ConsoleColor.DarkGray);
        }
        
        if (!string.IsNullOrEmpty(state.ResponseDraft))
        {
            Console.WriteLine();
            ConsoleHelpers.WriteColor("📝 GENERATED RESPONSE (awaiting agent review):", ConsoleColor.DarkGray);
            ConsoleHelpers.WriteColor("─────────────────────────────────────────────────────────────────", ConsoleColor.DarkGray);
            Console.ForegroundColor = ConsoleColor.White;
            foreach (var line in state.ResponseDraft.Split('\n'))
            {
                Console.WriteLine($"   {line.TrimEnd()}");
            }
            Console.ResetColor();
            ConsoleHelpers.WriteColor("─────────────────────────────────────────────────────────────────", ConsoleColor.DarkGray);
            
            Console.WriteLine();
            var boxColor = state.RequiresHumanReview ? ConsoleColor.Yellow : ConsoleColor.Cyan;
            ConsoleHelpers.WriteColor("╔═══════════════════════════════════════════════════════════════╗", boxColor);
            ConsoleHelpers.WriteColor("║  📋 SERVICE AGENT REVIEW (Workflow Step f)                    ║", boxColor);
            ConsoleHelpers.WriteColor("╠═══════════════════════════════════════════════════════════════╣", boxColor);
            ConsoleHelpers.WriteColor("║  [1] ✅ APPROVE - Send response to customer                   ║", ConsoleColor.Green);
            ConsoleHelpers.WriteColor("║  [0] ❌ REJECT  - Hold for manual handling                    ║", ConsoleColor.Red);
            ConsoleHelpers.WriteColor("╚═══════════════════════════════════════════════════════════════╝", boxColor);
            
            Console.Write("  Decision (1/0): ");
            var decision = Console.ReadLine()?.Trim();
            
            if (decision == "1")
            {
                ConsoleHelpers.WriteColor("\n  ✅ APPROVED - Response sent to customer.", ConsoleColor.Green);
            }
            else
            {
                ConsoleHelpers.WriteColor("\n  ❌ REJECTED - Response held for manual handling.", ConsoleColor.Red);
            }
        }
    }

    private void ShowSessionSummary()
    {
        var sessions = _sessionManager.GetActiveSessions();
        if (!sessions.Any()) return;
        
        Console.WriteLine();
        ConsoleHelpers.WriteColor("═══════════════════════════════════════════════════════════════", ConsoleColor.DarkGray);
        ConsoleHelpers.WriteColor("🔐 ACTIVE SESSIONS", ConsoleColor.DarkGray);
        ConsoleHelpers.WriteColor("═══════════════════════════════════════════════════════════════", ConsoleColor.DarkGray);
        
        foreach (var s in sessions)
        {
            var status = s.IsAuthenticated ? $"✓ {s.AuthenticatedCustomerName}" : "⏳ Pending";
            var data = string.Join(", ", s.CollectedAuthData.Keys);
            ConsoleHelpers.WriteColor($"  {s.CustomerEmail}", ConsoleColor.White);
            ConsoleHelpers.WriteColor($"    Status: {status}", s.IsAuthenticated ? ConsoleColor.Green : ConsoleColor.Yellow);
            ConsoleHelpers.WriteColor($"    Data: {data}", ConsoleColor.DarkGray);
        }
    }

    private string GenerateAgentThought(ProcessingState state, ConversationSession session)
    {
        var thoughts = new List<string>();
        
        if (session.IsAuthenticated)
        {
            thoughts.Add($"✓ Customer authenticated as: {session.AuthenticatedCustomerName}");
        }
        else if (state.ExtractedEntities.Count > 0)
        {
            var fields = string.Join(", ", state.ExtractedEntities.Keys.Take(4));
            thoughts.Add($"Authentication check: {state.ExtractedEntities.Count} fields extracted ({fields})");
            thoughts.Add("→ Insufficient for authentication. Triggering verification request loop.");
        }
        
        if (state.DetectedIntent?.Contains(",") == true)
        {
            thoughts.Add($"📋 Multi-intent detected: {state.DetectedIntent}");
        }
        
        if (state.Actions.Contains("correction_accepted"))
        {
            thoughts.Add("✓ Detected typo correction → Validation successful → Closing case.");
        }
        
        if (session.EmailHistory.Any(e => e.DetectedIntent?.Contains("ProductInfoRequest") == true) &&
            state.DetectedIntent?.Contains("ProductInfo") == true)
        {
            thoughts.Add("ℹ Tariff info already sent in previous email → Skipping redundancy.");
        }
        
        if (state.Actions.Any(a => a.Contains("plausibility_warning")))
        {
            if (state.ExtractedEntities.TryGetValue("consumption", out var consumption) &&
                state.ExtractedEntities.TryGetValue("expectedConsumption", out var expected))
            {
                thoughts.Add($"⚠ Plausibility check: consumption {consumption} kWh vs expected {expected} kWh");
            }
            else if (state.ExtractedEntities.TryGetValue("consumption", out var cons))
            {
                thoughts.Add($"⚠ Plausibility check triggered: Consumption {cons} kWh exceeds expected range.");
            }
            thoughts.Add("→ Flagging for customer confirmation before processing.");
        }
        
        if (state.Actions.Any(a => a.Contains("meter_reading_invalid")))
        {
            thoughts.Add("⚠ Plausibility check FAILED: New reading < Previous reading.");
            thoughts.Add("→ Triggering clarification loop. Customer must correct the reading.");
        }
        
        if (state.DetectedIntent?.Contains("ProductInfo") == true && !session.EmailHistory.Any(e => e.DetectedIntent?.Contains("ProductInfoRequest") == true))
        {
            thoughts.Add("ℹ Product inquiry detected - providing tariff information.");
        }
        
        if (state.DetectedIntent?.Contains("MeterReading") == true && session.IsAuthenticated)
        {
            if (!state.RequiresHumanReview && state.Actions.Contains("correction_accepted"))
            {
                thoughts.Add("✓ Corrected meter reading validated and recorded. Request completed.");
            }
            else if (!state.RequiresHumanReview)
            {
                thoughts.Add("✓ Meter reading validated and recorded.");
            }
        }
        
        if (state.RequiresHumanReview && session.IsAuthenticated)
        {
            thoughts.Add($"→ Request requires agent review: {state.HumanReviewReason ?? "Policy requirement"}");
        }
        
        if (!thoughts.Any())
        {
            thoughts.Add("Analyzing customer request and determining required actions.");
        }
        
        return string.Join(" ", thoughts);
    }
}
