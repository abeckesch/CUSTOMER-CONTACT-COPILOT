# Customer Contact Copilot

> **AI-powered email processing system for automated customer service**  
> Built with C#, .NET 8, and Semantic Kernel

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Semantic Kernel](https://img.shields.io/badge/Semantic_Kernel-1.68.0-blue)](https://learn.microsoft.com/en-us/semantic-kernel/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## 📋 Overview

This project implements an **agentic AI system** for processing customer emails at scale. It demonstrates intelligent orchestration of multiple intents, secure authentication workflows, and production-ready NFRs (Non-Functional Requirements).

### **Key Features**

✅ **Multi-Intent Detection** - Processes multiple customer requests from a single email  
✅ **Smart Authentication** - 3-point verification only when required  
✅ **Parallel Processing** - Non-auth intents run immediately while auth completes  
✅ **Plausibility Checks** - Validates data for realistic values  
✅ **Multi-Turn Conversations** - Maintains session state across email exchanges  
✅ **PII Redaction** - Protects sensitive data before LLM processing  
✅ **Observability** - Structured logging for production monitoring  

---

## 🎯 Use Case: EnergyCo Customer Service

The system handles common energy provider customer requests:

| Intent | Authentication | Example |
|--------|----------------|---------|
| **Meter Reading Submission** | ✅ Required | "My meter shows 1438 kWh" |
| **Personal Data Change** | ✅ Required | "Update my address to..." |
| **Contract Issues** | ✅ Required | "Cancel my contract" |
| **Product Info Request** | ❌ Not Required | "How does dynamic pricing work?" |
| **General Feedback** | ❌ Not Required | "Great service, thanks!" |

### **Demo Scenario: Julia Meyer's Request**

**Email 1**: Julia submits meter reading (2438 kWh) + asks about dynamic tariff  
→ System detects 2 intents, answers product question immediately, requests auth for meter reading

**Email 2**: Julia provides installment amount ("50,-")  
→ Authentication successful, but plausibility check flags unrealistic reading

**Email 3**: Julia corrects reading (1438 kWh)  
→ System accepts corrected value, confirms success

**Result**: Multi-turn conversation, intelligent auth handling, automated validation

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    EmailOrchestrator                    │
│  - Intent Detection (AI-based)                          │
│  - Multi-Intent Routing                                 │
│  - Session State Management                             │
└────────────┬────────────────────────────┬───────────────┘
             │                            │
    ┌────────▼────────┐          ┌────────▼────────┐
    │ Authentication  │          │  Product Info   │
    │    Service      │          │     Agent       │
    │  (3-point auth) │          │  (No auth req.) │
    └────────┬────────┘          └─────────────────┘
             │
    ┌────────▼────────────────────┐
    │  Specialized Intent Services│
    │  - Meter Reading            │
    │  - Personal Data Change     │
    │  - Contract Management      │
    └─────────────────────────────┘
```

### **Components**

- **`EmailOrchestrator`**: Main coordinator for email processing workflow
- **`CustomerService`**: Customer data access and 3-point authentication
- **`MeterReadingService`**: Handles meter submissions with plausibility checks
- **`ProductInfoAgent`**: Answers product/tariff questions (no auth required)
- **`PiiRedactionService`**: Data protection layer
- **`SessionStateManager`**: Multi-turn conversation state
- **`ResultAggregator`**: Combines responses from multiple intents
- **`AgentLogger`**: Structured logging for observability

---

## 🚀 Getting Started

### **Prerequisites**

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- OpenAI API Key (or Azure OpenAI endpoint)

### **Installation**

```bash
# Clone the repository
git clone <repository-url>
cd CustomerContactCopilot

# Restore dependencies
dotnet restore

# Build the project
dotnet build
```

### **Configuration**

Set your OpenAI API key using one of these methods:

**Option 1: User Secrets (Recommended for Development)**
```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-api-key-here"
```

**Option 2: Environment Variable**
```bash
# Windows (PowerShell)
$env:OPENAI_API_KEY = "sk-your-api-key-here"

# Linux/Mac
export OPENAI_API_KEY="sk-your-api-key-here"
```

**Option 3: appsettings.json** (Not recommended - add to .gitignore!)
```json
{
  "OpenAI": {
    "ApiKey": "sk-your-api-key-here"
  }
}
```

### **Running the Application**

```bash
dotnet run
```

The application starts in **Interactive Demo Mode** with a menu:

```
  [1] 📧 Case Study (Julia Meyer) - 3 sequential emails
  [2] 🔄 Multiturn Session Demo - with session persistence
  [3] 📁 Individual Test Emails - select single file
  [4] 🗑️  Clear All Sessions - reset for fresh demo
  [5] ❌ Exit
```

**Recommended**: Start with **Option 1** (Case Study) to see the complete Julia Meyer workflow.

---

## 📁 Project Structure

```
CustomerContactCopilot/
├── Agents/
│   └── ProductInfoAgent.cs          # Non-auth product inquiries
├── Data/
│   └── mock_customers.json          # Test customer database
├── Models/
│   ├── AuthenticationResult.cs      # Auth workflow models
│   ├── ConversationSession.cs       # Multi-turn state
│   ├── CustomerData.cs              # Customer entity
│   ├── CustomerEmail.cs             # Email entity
│   ├── CustomerIntent.cs            # Intent enum
│   ├── MeterReadingValidation.cs    # Validation results
│   └── ProcessingState.cs           # Workflow state
├── Orchestrator/
│   └── EmailOrchestrator.cs         # Main orchestration logic
├── Services/
│   ├── AgentLogger.cs               # Structured logging
│   ├── CustomerService.cs           # Customer data & authentication
│   ├── MeterReadingService.cs       # Meter reading handler
│   ├── PiiRedactionService.cs       # Data protection
│   ├── ResultAggregator.cs          # Response aggregation
│   └── SessionStateManager.cs       # Multi-turn session state
├── UI/
│   └── InteractiveDemo.cs           # Console menu execution logic
├── Utils/
│   ├── ConsoleHelpers.cs            # CLI text formatting utilities
│   └── EmailParser.cs               # Email string parsing logic
├── TestEmails/
│   ├── 01_julia_meyer_meter_reading.txt
│   ├── 02_missing_auth_data.txt
│   ├── 03_unrealistic_meter_reading.txt
│   ├── 04_contract_termination.txt
│   ├── 05_product_inquiry.txt
│   ├── 06_high_consumption_plausibility.txt
│   ├── case_study/                  # Julia Meyer 3-email scenario
│   │   ├── 01_customer_initial.txt
│   │   ├── 02_customer_auth_response.txt
│   │   └── 03_customer_correction.txt
│   └── multiturn/                   # Session persistence tests
├── Program.cs                       # Clean application entry point
├── .gitignore                       # Git ignore rules
└── README.md                        # This file
```

---

## 🧪 Testing

### **Included Test Scenarios**

1. **Case Study (Julia Meyer)**: Full 3-email multi-turn workflow
2. **Missing Auth Data**: Tests authentication request flow
3. **Product-Only Inquiry**: Validates no-auth path
4. **Contract Termination**: Complex auth-required intent
5. **High Consumption Plausibility**: Data validation workflow

### **Running Tests**

```bash
# Run the application and use test emails from TestEmails/ directory
dotnet run

# Example test sequence:
# 1. TestEmails/case_study/01_customer_initial.txt
# 2. TestEmails/case_study/02_customer_auth_response.txt
# 3. TestEmails/case_study/03_customer_correction.txt
```

---

## 🔒 Security & Compliance

### **Data Protection**

- ✅ **PII Redaction**: 8 PII patterns detected and masked before LLM
- ✅ **No Hardcoded Secrets**: API keys via User Secrets or environment variables
- ✅ **Mock Data Only**: No real customer data in repository

### **Authentication**

- ✅ **3-Point Verification**: Requires ≥3 matching data points
- ✅ **Supported Auth Fields**:
  - Contract Number
  - Full Name
  - Birthday
  - Street Address
  - Postal Code
  - Current Installment Amount

---

## 📊 Technical Highlights

### **NFRs Implemented**

| NFR | Implementation |
|-----|----------------|
| **Observability** | Structured logging with context |
| **Reliability** | Error handling, retry logic ready |
| **Security** | PII redaction, auth before sensitive ops |
| **Data Protection** | Mock services, no real customer data |
| **Extensibility** | Service-oriented architecture |
| **Performance** | Parallel processing for independent intents |

### **Design Patterns**

- **Orchestrator Pattern**: Central coordination of complex workflows
- **Service Layer**: Separation of business logic
- **State Management**: Session-based multi-turn conversations
- **Strategy Pattern**: Different handling per intent type

---

## 🛠️ Technology Stack

- **Runtime**: .NET 8.0
- **AI Framework**: Microsoft Semantic Kernel 1.68.0
- **LLM Provider**: OpenAI (easily switchable to Azure OpenAI)
- **Configuration**: User Secrets, Environment Variables
- **Data Format**: JSON
- **Logging**: Console (production-ready for Serilog/App Insights)

---

## 📝 Requirements Fulfilled

This project fulfills all 8 core requirements for an automated customer service system:

1. ✅ **Complete Workflow**: Text extraction → Intent detection → Data extraction → Handling → Aggregation → Review
2. ✅ **Authentication**: 3-point verification with multi-turn support
3. ✅ **Case Study**: Julia Meyer scenario fully implemented
4. ✅ **Multi-Intent**: Parallel processing with shared auth state
5. ✅ **Tech Stack**: C#, .NET 8, Semantic Kernel
6. ✅ **Mocking**: Text files for emails, mock customer database
7. ✅ **NFRs**: Observability, reliability, data protection, security
8. ✅ **Tests**: Multiple scenarios in TestEmails/ directory

---

## 🚀 Future Enhancements

### **Production Readiness**
- [ ] Azure Functions deployment for scalable email processing
- [ ] Cosmos DB for session state persistence
- [ ] Azure Service Bus for queue-based email ingestion
- [ ] Application Insights for production monitoring
- [ ] Circuit breaker pattern for API resilience

### **Feature Additions**
- [ ] Email response generation (currently draft only)
- [ ] Sentiment analysis for feedback classification
- [ ] Multi-language support (currently German/English)
- [ ] Webhook integration for CRM systems

---

## 📄 License

This project was created as a demonstration of AI-powered customer service automation.

---

## 👤 Author

**Alexander Beck**  
**Customer Service Copilot Prototype**  
January 2026

---

## 🙏 Acknowledgments

- Built with [Microsoft Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/)
- Inspired by real-world customer service automation needs

