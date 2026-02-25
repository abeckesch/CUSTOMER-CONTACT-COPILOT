using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using AgenticCustomerContactCopilot.Models;
using AgenticCustomerContactCopilot.Services;
using AgenticCustomerContactCopilot.Orchestrator;

// ============================================================================
// INTERACTIVE DEMO MODE - EnergyCo Customer Contact Copilot
// ============================================================================

// --- Configuration & Initialization ---
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables()
    .Build();

var modelId = configuration["AI:Model"] ?? "o3-mini";
var openAiApiKey = configuration["OpenAI:ApiKey"] 
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    AgenticCustomerContactCopilot.Utils.ConsoleHelpers.WriteColor("❌ ERROR: OpenAI API Key not found!", ConsoleColor.Red);
    AgenticCustomerContactCopilot.Utils.ConsoleHelpers.WriteColor("   Set via: dotnet user-secrets set \"OpenAI:ApiKey\" \"sk-...\"", ConsoleColor.Yellow);
    return;
}

var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId, openAiApiKey)
    .Build();

var customerService = new CustomerService();
var meterReadingService = new MeterReadingService();
var sessionManager = new SessionStateManager();
var logger = new AgentLogger(consoleOutput: false); // We'll handle our own output
var piiRedactor = new PiiRedactionService();
var orchestrator = new EmailOrchestrator(kernel, customerService, meterReadingService, sessionManager, logger, piiRedactor);

var testEmailsPath = Path.Combine(Directory.GetCurrentDirectory(), "TestEmails");
if (!Directory.Exists(testEmailsPath))
{
    testEmailsPath = Path.Combine(AppContext.BaseDirectory, "TestEmails");
}

var demo = new AgenticCustomerContactCopilot.UI.InteractiveDemo(orchestrator, sessionManager, modelId);
await demo.StartAsync();
