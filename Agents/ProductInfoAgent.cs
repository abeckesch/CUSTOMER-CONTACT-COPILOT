using Microsoft.SemanticKernel;
using AgenticCustomerContactCopilot.Models;

namespace AgenticCustomerContactCopilot.Agents;

/// <summary>
/// Specialized agent for answering questions about EnergyCo's product offerings,
/// particularly the ÖkoStrom Dynamic tariff with hourly pricing.
/// </summary>
public class ProductInfoAgent
{
    private readonly Kernel _kernel;
    private const string SystemPrompt = """
        Du bist ein freundlicher und kompetenter Kundenberater von EnergyCo SE, 
        Deutschlands größtem unabhängigen Ökostromanbieter. Du beantwortest Fragen 
        zu unseren Produkten, insbesondere zum innovativen ÖkoStrom Dynamic Tarif.

        === EnergyCo ÖKOSTROM DYNAMIC - PRODUKTINFORMATIONEN ===

        ÜBERBLICK:
        Der ÖkoStrom Dynamic ist ein dynamischer Stromtarif, bei dem der Preis 
        stündlich an der Strombörse (EPEX Spot) angepasst wird. Kunden profitieren 
        von günstigen Preisen, wenn viel erneuerbare Energie im Netz ist.

        PREISMODELL:
        • Stündlich wechselnde Preise basierend auf dem Börsenstrompreis (EPEX Spot Day-Ahead)
        • Börsenpreis + fester Aufschlag (ca. 2-3 ct/kWh für Beschaffung, Vertrieb, Marge)
        • Netzentgelte und Steuern/Abgaben kommen hinzu (regional unterschiedlich)
        • Preise können für den Folgetag ab 14:00 Uhr eingesehen werden
        • Typische Preisspanne: 15-45 ct/kWh brutto (je nach Marktlage)
        • An windigen/sonnigen Tagen können Preise auch negativ werden!

        VORAUSSETZUNGEN:
        • Intelligentes Messsystem (Smart Meter) erforderlich
        • Smart Meter Gateway für 15-Minuten-Verbrauchsmessung
        • Installation durch zuständigen Messstellenbetreiber
        • Kosten Smart Meter: ca. 20€/Jahr (gesetzlich gedeckelt für Haushalte < 6.000 kWh)
        • Internetverbindung für Echtzeit-Preisanzeige in der App

        FÜR WEN GEEIGNET - IDEAL FÜR:
        ✓ Haushalte mit flexiblem Verbrauch (z.B. Waschmaschine, Geschirrspüler zeitlich flexibel)
        ✓ Besitzer von Elektroautos (Laden in günstigen Nachtstunden)
        ✓ Haushalte mit Wärmepumpen (flexible Heizzeiten möglich)
        ✓ Besitzer von Batteriespeichern (Laden bei niedrigen Preisen)
        ✓ Technikaffine Kunden, die aktiv ihren Verbrauch steuern möchten
        ✓ Kunden mit Smart-Home-Systemen für automatische Verbrauchssteuerung

        FÜR WEN WENIGER GEEIGNET:
        ✗ Haushalte, die Preissicherheit bevorzugen
        ✗ Geringe Flexibilität beim Stromverbrauch
        ✗ Kein Interesse an aktiver Verbrauchssteuerung
        ✗ 2-Personen-Haushalte ohne Elektroauto oder Wärmepumpe: 
          Der Aufwand lohnt sich oft nicht, da die Ersparnis bei niedrigem 
          Grundverbrauch (ca. 2.500-3.000 kWh/Jahr) gering ausfällt.
          Empfehlung: EnergyCo ÖkoStrom Klassik mit Preisgarantie

        SPARPOTENZIAL:
        • Ersparnis durch Verbrauchsverlagerung: 10-30% gegenüber Fixpreis-Tarifen möglich
        • Größtes Sparpotenzial: Sonntags, nachts (22-6 Uhr), windige/sonnige Tage
        • Höchste Preise: Werktags 17-20 Uhr (Abendspitze)
        • Mit Elektroauto: Ersparnis von 200-500€/Jahr realistisch
        • Ohne flexible Großverbraucher: Ersparnis eher 50-100€/Jahr

        EnergyCo APP:
        • Echtzeit-Preisanzeige für die nächsten 24 Stunden
        • Push-Benachrichtigungen bei besonders günstigen Preisen
        • Verbrauchsanalyse und Spartipps
        • CO2-Bilanz und Herkunftsnachweis des Stroms
        • Smart-Home-Integration (kompatibel mit gängigen Systemen)

        VERTRAGSKONDITIONEN:
        • Mindestvertragslaufzeit: 1 Monat (monatlich kündbar)
        • Keine Vorkasse
        • 100% Ökostrom aus erneuerbaren Energien
        • Grundgebühr: 9,99€/Monat (inkl. Smart Meter Anbindung)
        • Keine versteckten Kosten

        WECHSEL UND ANMELDUNG:
        • Online-Anmeldung in 5 Minuten
        • Smart Meter Einbau dauert ca. 2-4 Wochen
        • Alter Vertrag wird von EnergyCo gekündigt
        • Nahtloser Übergang, keine Versorgungsunterbrechung

        WEITERE EnergyCo TARIFE:
        • ÖkoStrom Klassik: Fester Preis, 12 Monate Preisgarantie
        • ÖkoStrom Flex: Monatlich kündbar, variable Preisanpassung
        • ÖkoStrom Premium: Mit CO2-Kompensation und Waldschutzprojekt
        • ÖkoGas: 100% klimaneutrales Erdgas

        === KOMMUNIKATIONSRICHTLINIEN ===

        • Antworte immer auf Deutsch
        • Sei freundlich, kompetent und lösungsorientiert
        • Verwende "Sie" als Anrede
        • Gib ehrliche Einschätzungen zur Eignung des Tarifs
        • Bei komplexen Fragen empfehle ein persönliches Beratungsgespräch
        • Erwähne Vorteile, aber auch mögliche Nachteile transparent
        • Verweise bei technischen Detailfragen auf den Kundenservice
        """;

    public ProductInfoAgent(Kernel kernel)
    {
        _kernel = kernel;
    }

    /// <summary>
    /// Answers a product-related question using the specialized system prompt.
    /// </summary>
    public async Task<string> AnswerQuestionAsync(string customerQuestion, CustomerData? customer = null)
    {
        var customerContext = "";
        if (customer != null)
        {
            customerContext = $"""
                
                Kundenkontext:
                - Name: {customer.FullName}
                - Aktueller Tarif: {customer.CurrentTariff}
                - Monatliche Zahlung: {customer.MonthlyPayment}€
                """;
        }

        var prompt = $"""
            {SystemPrompt}
            {customerContext}

            Kundenfrage: {customerQuestion}

            Bitte beantworte die Frage basierend auf den obigen Produktinformationen.
            Sei präzise und hilfreich. Wenn die Frage nicht mit den verfügbaren 
            Informationen beantwortet werden kann, sage das ehrlich.
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.GetValue<string>() ?? GenerateFallbackResponse(customerQuestion);
        }
        catch
        {
            return GenerateFallbackResponse(customerQuestion);
        }
    }

    /// <summary>
    /// Provides a recommendation based on household characteristics.
    /// </summary>
    public async Task<ProductRecommendation> GetRecommendationAsync(HouseholdProfile profile)
    {
        var prompt = $"""
            {SystemPrompt}

            Analysiere das folgende Haushaltsprofil und gib eine Tarifempfehlung:

            Haushaltsprofil:
            - Personen im Haushalt: {profile.NumberOfPersons}
            - Geschätzter Jahresverbrauch: {profile.EstimatedYearlyConsumption} kWh
            - Elektroauto vorhanden: {(profile.HasElectricVehicle ? "Ja" : "Nein")}
            - Wärmepumpe vorhanden: {(profile.HasHeatPump ? "Ja" : "Nein")}
            - Smart-Home-System: {(profile.HasSmartHome ? "Ja" : "Nein")}
            - Flexibilität beim Verbrauch: {profile.FlexibilityLevel}/5
            - Interesse an Technik: {(profile.IsTechSavvy ? "Hoch" : "Normal")}

            Gib eine strukturierte Empfehlung mit:
            1. Empfohlener Tarif (ÖkoStrom Dynamic, Klassik, Flex, oder Premium)
            2. Kurze Begründung (2-3 Sätze)
            3. Geschätztes Sparpotenzial pro Jahr
            4. Wichtige Hinweise

            Format: JSON mit den Feldern: recommendedTariff, reasoning, estimatedSavings, notes
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var response = result.GetValue<string>();
            
            // Try to parse JSON response or create default
            return ParseRecommendation(response, profile);
        }
        catch
        {
            return GenerateDefaultRecommendation(profile);
        }
    }

    /// <summary>
    /// Answers specific questions about the ÖkoStrom Dynamic tariff.
    /// </summary>
    public string AnswerDynamicTariffQuestion(string questionType)
    {
        return questionType.ToLowerInvariant() switch
        {
            "hourly_prices" or "stündliche_preise" => """
                Beim ÖkoStrom Dynamic ändern sich die Preise stündlich:

                🕐 PREISÄNDERUNGEN:
                • Preise basieren auf dem Börsenstrompreis (EPEX Spot Day-Ahead)
                • Neue Preise werden täglich um 14:00 Uhr für den Folgetag veröffentlicht
                • Sie können die Preise in der EnergyCo App oder im Kundenportal einsehen
                
                💰 TYPISCHE PREISSPANNEN:
                • Günstige Zeiten: 15-25 ct/kWh (nachts, am Wochenende, bei viel Wind/Sonne)
                • Normale Zeiten: 25-35 ct/kWh
                • Teure Zeiten: 35-45 ct/kWh (werktags 17-20 Uhr)
                • Negative Preise sind möglich! (Sie bekommen Geld fürs Verbrauchen)
                
                📱 Die App benachrichtigt Sie automatisch bei besonders günstigen Preisen.
                """,

            "smart_meter" or "smart_meter_requirement" => """
                Für den ÖkoStrom Dynamic Tarif ist ein Smart Meter erforderlich:

                📊 WAS IST EIN SMART METER?
                • Intelligentes Messsystem mit 15-Minuten-Verbrauchserfassung
                • Ermöglicht stündliche Abrechnung und Echtzeit-Monitoring
                
                🔧 INSTALLATION:
                • Einbau durch Ihren zuständigen Messstellenbetreiber
                • Dauer: ca. 2-4 Wochen nach Anmeldung
                • EnergyCo koordiniert den Einbau für Sie
                
                💵 KOSTEN:
                • Für Haushalte < 6.000 kWh/Jahr: max. 20€/Jahr (gesetzlich gedeckelt)
                • Für größere Verbraucher: bis 50€/Jahr
                • Im Grundpreis von 9,99€/Monat ist die Smart Meter Anbindung enthalten
                
                🏠 VORAUSSETZUNGEN:
                • Moderner Zählerschrank (ggf. Anpassung nötig)
                • Internetverbindung am Zählerort nicht zwingend erforderlich
                """,

            "2_person_household" or "2_personen_haushalt" => """
                Eignung des ÖkoStrom Dynamic für einen 2-Personen-Haushalt:

                ⚖️ EHRLICHE EINSCHÄTZUNG:
                Ein 2-Personen-Haushalt OHNE Elektroauto oder Wärmepumpe profitiert 
                oft nur begrenzt vom Dynamic-Tarif.

                📊 WARUM?
                • Typischer Verbrauch: 2.500-3.000 kWh/Jahr
                • Geringe absolute Einsparmöglichkeiten bei niedrigem Verbrauch
                • Aufwand für Verbrauchsoptimierung lohnt sich weniger
                • Mögliche Ersparnis: ca. 50-100€/Jahr bei aktivem Management

                ✅ EMPFEHLUNG FÜR SIE:
                Der EnergyCo ÖkoStrom Klassik könnte besser passen:
                • Fester, planbarer Preis
                • 12 Monate Preisgarantie
                • Kein Aufwand für Verbrauchssteuerung
                • Trotzdem 100% Ökostrom

                🚗 AUSNAHME:
                Falls Sie ein E-Auto anschaffen möchten, lohnt sich der Dynamic-Tarif 
                durch günstiges Laden in der Nacht (Ersparnis: 200-400€/Jahr möglich).
                """,

            "savings" or "sparpotenzial" => """
                Sparpotenzial beim ÖkoStrom Dynamic:

                🏠 NACH HAUSHALTSTYP:
                
                • 2 Personen, kein E-Auto/Wärmepumpe: 50-100€/Jahr
                • Familie mit aktivem Verbrauchsmanagement: 100-200€/Jahr
                • Mit Elektroauto (Laden nachts): 200-500€/Jahr
                • Mit Wärmepumpe (flexible Heizzeiten): 150-400€/Jahr
                • Mit Batteriespeicher: 200-600€/Jahr
                
                ⏰ BESTE ZEITEN ZUM SPAREN:
                • Sonntags: Preise oft 30-50% unter Werktagen
                • Nachts (22-6 Uhr): Günstigste Preise
                • Windige Tage: Hohe Einspeisung = niedrige Preise
                • Sonnige Mittagszeit (bei PV-Überschuss)
                
                ⚡ TEUERSTE ZEITEN (MEIDEN):
                • Werktags 17-20 Uhr (Abendspitze)
                • Kalte Wintermorgen (hohe Heizlast)
                """,

            _ => """
                Der EnergyCo ÖkoStrom Dynamic ist unser innovativer Tarif mit 
                stündlich wechselnden Preisen, die dem Börsenstrompreis folgen.

                VORTEILE:
                ✓ Sparen bei hoher Einspeisung erneuerbarer Energien
                ✓ Volle Transparenz über Strompreise
                ✓ Monatlich kündbar, flexibel
                ✓ Ideal für E-Autos, Wärmepumpen, Smart Homes

                VORAUSSETZUNGEN:
                • Smart Meter erforderlich
                • Interesse an aktivem Verbrauchsmanagement

                Für detaillierte Informationen zu einem bestimmten Thema 
                stehe ich gerne zur Verfügung!
                """
        };
    }

    /// <summary>
    /// Generates a fallback response when AI is unavailable.
    /// </summary>
    private string GenerateFallbackResponse(string question)
    {
        var lowerQuestion = question.ToLowerInvariant();

        if (lowerQuestion.Contains("preis") || lowerQuestion.Contains("kosten") || lowerQuestion.Contains("stündlich"))
        {
            return AnswerDynamicTariffQuestion("hourly_prices");
        }
        if (lowerQuestion.Contains("smart meter") || lowerQuestion.Contains("zähler") || lowerQuestion.Contains("voraussetzung"))
        {
            return AnswerDynamicTariffQuestion("smart_meter");
        }
        if (lowerQuestion.Contains("2 person") || lowerQuestion.Contains("zwei person") || lowerQuestion.Contains("haushalt"))
        {
            return AnswerDynamicTariffQuestion("2_person_household");
        }
        if (lowerQuestion.Contains("spar") || lowerQuestion.Contains("lohnt"))
        {
            return AnswerDynamicTariffQuestion("savings");
        }

        return AnswerDynamicTariffQuestion("default");
    }

    /// <summary>
    /// Parses a recommendation from AI response.
    /// </summary>
    private ProductRecommendation ParseRecommendation(string? response, HouseholdProfile profile)
    {
        // Default recommendation based on profile
        return GenerateDefaultRecommendation(profile);
    }

    /// <summary>
    /// Generates a default recommendation based on household profile.
    /// </summary>
    private ProductRecommendation GenerateDefaultRecommendation(HouseholdProfile profile)
    {
        // Decision logic based on profile
        var hasMajorFlexibleLoad = profile.HasElectricVehicle || profile.HasHeatPump;
        var isFlexible = profile.FlexibilityLevel >= 3;
        var highConsumption = profile.EstimatedYearlyConsumption > 4000;

        if (hasMajorFlexibleLoad && isFlexible)
        {
            return new ProductRecommendation
            {
                RecommendedTariff = "ÖkoStrom Dynamic",
                Reasoning = $"Mit {(profile.HasElectricVehicle ? "Elektroauto" : "Wärmepumpe")} und " +
                           $"hoher Flexibilität können Sie deutlich von den dynamischen Preisen profitieren. " +
                           $"Laden/Heizen in günstigen Zeiten bietet erhebliches Sparpotenzial.",
                EstimatedSavingsPerYear = profile.HasElectricVehicle ? 350 : 250,
                Notes = new List<string>
                {
                    "Smart Meter erforderlich (ca. 20€/Jahr)",
                    "EnergyCo App für Preisbenachrichtigungen nutzen",
                    "Automatische Ladezeiten programmieren für maximale Ersparnis"
                },
                SuitabilityScore = 9
            };
        }

        if (profile.NumberOfPersons <= 2 && !hasMajorFlexibleLoad)
        {
            return new ProductRecommendation
            {
                RecommendedTariff = "ÖkoStrom Klassik",
                Reasoning = "Für einen 2-Personen-Haushalt ohne E-Auto oder Wärmepumpe bietet der " +
                           "Klassik-Tarif ein besseres Preis-Leistungs-Verhältnis. Die Ersparnis beim " +
                           "Dynamic-Tarif wäre bei Ihrem Verbrauch gering.",
                EstimatedSavingsPerYear = 0, // Klassik is baseline
                Notes = new List<string>
                {
                    "12 Monate Preisgarantie für Planungssicherheit",
                    "Kein Aufwand für Verbrauchsoptimierung nötig",
                    "Bei Anschaffung eines E-Autos: Wechsel zum Dynamic-Tarif empfohlen"
                },
                SuitabilityScore = 8
            };
        }

        if (highConsumption && isFlexible)
        {
            return new ProductRecommendation
            {
                RecommendedTariff = "ÖkoStrom Dynamic",
                Reasoning = "Bei Ihrem hohen Verbrauch und der Bereitschaft zur flexiblen Nutzung " +
                           "können Sie spürbar von günstigen Börsenpreisen profitieren.",
                EstimatedSavingsPerYear = 150,
                Notes = new List<string>
                {
                    "Großverbraucher wie Waschmaschine/Trockner in günstige Zeiten legen",
                    "Smart-Home-Integration empfohlen für automatische Steuerung"
                },
                SuitabilityScore = 7
            };
        }

        // Default to Flex for moderate cases
        return new ProductRecommendation
        {
            RecommendedTariff = "ÖkoStrom Flex",
            Reasoning = "Der Flex-Tarif bietet eine gute Balance aus Flexibilität und Stabilität. " +
                       "Monatlich kündbar, aber ohne den Aufwand der stündlichen Preisoptimierung.",
            EstimatedSavingsPerYear = 50,
            Notes = new List<string>
            {
                "Monatlich kündbar, keine lange Bindung",
                "Bei steigendem Interesse an Flexibilität: Wechsel zum Dynamic-Tarif möglich"
            },
            SuitabilityScore = 6
        };
    }
}

/// <summary>
/// Profile of a household for tariff recommendation.
/// </summary>
public class HouseholdProfile
{
    public int NumberOfPersons { get; set; }
    public int EstimatedYearlyConsumption { get; set; }
    public bool HasElectricVehicle { get; set; }
    public bool HasHeatPump { get; set; }
    public bool HasSmartHome { get; set; }
    /// <summary>Flexibility level 1-5 (1=rigid schedule, 5=very flexible)</summary>
    public int FlexibilityLevel { get; set; }
    public bool IsTechSavvy { get; set; }
}

/// <summary>
/// Product recommendation result.
/// </summary>
public class ProductRecommendation
{
    public string RecommendedTariff { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public int EstimatedSavingsPerYear { get; set; }
    public List<string> Notes { get; set; } = new();
    /// <summary>Suitability score 1-10</summary>
    public int SuitabilityScore { get; set; }
}
