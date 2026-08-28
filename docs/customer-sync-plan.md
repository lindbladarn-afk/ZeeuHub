# Customer Sync plan

Detta dokument beskriver hur kundsynken mellan Jeeves och HubSpot ska byggas i portalen utan att blandas ihop med befintliga moduler.

## Mål

CustomerSync ska vara en egen integrationsmodul i `WebApp/Services/Integration/CustomerSync`. Modulen ska kunna:

- läsa nya eller ändrade kunder från Jeeves varje timme
- skapa eller uppdatera motsvarande kund i HubSpot
- ta emot HubSpot-webhooks för nya eller ändrade kunder
- skapa eller uppdatera motsvarande kund i Jeeves
- köra om misslyckade poster utan att skapa dubletter
- visa vad som hänt per körning, per kund och per riktning

Målet är inte att bygga en ny integrationsplattform. Målet är att använda portalens befintliga background-job-infrastruktur, men hålla kundsynken isolerad och enkel att följa.

## Rekommenderad struktur

```text
WebApp/Services/Integration/CustomerSync
  Application
    CustomerSyncFromJeevesHandler.cs
    CustomerSyncFromHubSpotHandler.cs
    CustomerSyncJobScheduler.cs
    CustomerSyncResult.cs
    CustomerSyncRunSummaryFactory.cs

  Background
    CustomerSyncBackgroundJobConstants.cs
    CustomerSyncBackgroundJobHandler.cs
    CustomerSyncBackgroundJobPayload.cs
    CustomerSyncHourlyWorker.cs
    CustomerSyncPresentationProvider.cs

  Domain
    CustomerSyncDirection.cs
    CustomerSyncExternalSystem.cs
    CustomerSyncMatchDecision.cs
    CustomerSyncPolicy.cs
    CustomerSyncStatus.cs
    SyncedCustomer.cs

  HubSpot
    HubSpotCustomerClient.cs
    HubSpotCustomerDto.cs
    HubSpotWebhookSignatureValidator.cs
    IHubSpotCustomerClient.cs
    IHubSpotWebhookSignatureValidator.cs

  Jeeves
    IJeevesCustomerSyncClient.cs
    JeevesCustomerDto.cs
    JeevesCustomerSyncClient.cs

  Mapping
    CustomerSyncMapper.cs
    CustomerSyncNormalizer.cs
    ICustomerSyncMapper.cs
    ICustomerSyncNormalizer.cs

  Persistence
    CustomerSyncCheckpointRepository.cs
    CustomerSyncEventRepository.cs
    CustomerSyncMappingRepository.cs
    CustomerSyncRunRepository.cs
    ICustomerSyncCheckpointRepository.cs
    ICustomerSyncEventRepository.cs
    ICustomerSyncMappingRepository.cs
    ICustomerSyncRunRepository.cs

  CustomerSyncServiceCollectionExtensions.cs
```

Controllers ska ligga utanför service-modulen, men vara tunna:

```text
WebApp/Controllers/Integration/CustomerSyncWebhookController.cs
WebApp/Controllers/Integration/CustomerSyncAdminController.cs
```

Admin-controller behövs bara om vi vill ha manuell trigger, hälsokontroll eller replay från UI/API.

## Ansvarsgränser

### `Application`

Orkestrerar use cases. Här får koden bestämma i vilken ordning saker sker, men inte innehålla HTTP-detaljer, SQL-detaljer eller HubSpot-signaturvalidering.

Exempel:

- hämta checkpoint
- läsa ändrade kunder från Jeeves
- normalisera kund
- hitta befintlig mapping
- skapa eller uppdatera i HubSpot
- spara mapping, run item och ny checkpoint

### `Domain`

Innehåller små beslut som ska vara enkla att testa. Här ska det inte finnas databas, HTTP eller konfiguration.

Exempel:

- om kund får skickas
- vilken identifierare som används för matchning
- hur konflikt mellan Jeeves och HubSpot ska bedömas
- om en körning ska markeras som `Skipped`, `Updated`, `Created` eller `Failed`

### `HubSpot` och `Jeeves`

Tunna adapters mot externa system. De ska returnera tydliga DTOs och kasta/returnera fel som applikationslagret kan klassificera.

Regel: ingen affärslogik här. Bara transport, auth, DTO-konvertering och systemnära felhantering.

### `Mapping`

All normalisering och fältmappning ligger samlat här. Det gör det enklare att se exakt hur en kund i Jeeves blir en kund i HubSpot och tvärtom.

### `Persistence`

All lagring av sync-state, mapping, events och körningshistorik ligger här. Application-lagret ska prata via interfaces.

## Datamodell

Följande tabeller bör läggas i portalens `Identity`-databas eftersom det är där background jobs och integrationshistorik redan bor.

### `CustomerSyncMappings`

Kopplar ihop samma kund mellan systemen.

Viktiga fält:

- `Id`
- `CompanyId`
- `JeevesCompanyCode`
- `JeevesCustomerNumber`
- `HubSpotCompanyId`
- `HubSpotContactId`
- `OrganizationNumber`
- `NormalizedName`
- `LastSyncedFromJeevesAtUtc`
- `LastSyncedFromHubSpotAtUtc`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Index:

- unikt på `CompanyId`, `JeevesCompanyCode`, `JeevesCustomerNumber`
- unikt på `CompanyId`, `HubSpotCompanyId` när värdet finns
- index på `CompanyId`, `OrganizationNumber`

### `CustomerSyncCheckpoints`

Sparar watermark per bolag och riktning.

Viktiga fält:

- `Id`
- `CompanyId`
- `JeevesCompanyCode`
- `Direction`
- `CheckpointValue`
- `CheckpointUtc`
- `UpdatedAtUtc`

Checkpoint ska bara flyttas fram när batchen är färdig och resultatet är säkert sparat.

### `CustomerSyncRuns`

En rad per körning.

Viktiga fält:

- `Id`
- `CompanyId`
- `JeevesCompanyCode`
- `Direction`
- `Trigger`
- `Status`
- `StartedAtUtc`
- `FinishedAtUtc`
- `CreatedCount`
- `UpdatedCount`
- `SkippedCount`
- `FailedCount`
- `CorrelationId`

### `CustomerSyncRunItems`

En rad per kund i en körning.

Viktiga fält:

- `Id`
- `RunId`
- `CompanyId`
- `ExternalKey`
- `JeevesCustomerNumber`
- `HubSpotObjectId`
- `Status`
- `ErrorCode`
- `ErrorMessage`
- `CreatedAtUtc`

Felmeddelanden ska vara felsökningsbara men inte innehålla tokens, secrets eller fulla payloads.

### `CustomerSyncEvents`

Tar emot HubSpot-webhooks idempotent.

Viktiga fält:

- `Id`
- `CompanyId`
- `HubSpotEventId`
- `HubSpotObjectId`
- `EventType`
- `PayloadHash`
- `ReceivedAtUtc`
- `ProcessedAtUtc`
- `Status`
- `ErrorMessage`

Unikt index på `CompanyId`, `HubSpotEventId`.

## Flöde: Jeeves till HubSpot

1. `CustomerSyncHourlyWorker` vaknar enligt konfiguration, normalt varje timme.
2. Workern frågar `CustomerSyncJobScheduler` om ett jobb redan är aktivt för samma bolag och riktning.
3. Om inget aktivt jobb finns köas ett background job med correlation key, till exempel `customersync:jeeves-to-hubspot:{companyId}:{jeevesCompanyCode}`.
4. `CustomerSyncBackgroundJobHandler` tar jobbet och skickar vidare till `CustomerSyncFromJeevesHandler`.
5. Handlern hämtar checkpoint.
6. Handlern läser nya eller ändrade kunder från Jeeves.
7. Varje kund normaliseras och matchas mot befintlig mapping.
8. Kunden skapas eller uppdateras i HubSpot.
9. Mapping och run item sparas per kund.
10. Checkpoint flyttas fram när batchen är klar.

Om en enskild kund får valideringsfel ska den markeras som failed/skipped utan att hela batchen behöver falla. Om Jeeves eller HubSpot är nere ska jobbet retryas.

## Flöde: HubSpot till Jeeves

1. HubSpot postar webhook till `CustomerSyncWebhookController`.
2. Controllern verifierar signaturen med `HubSpotWebhookSignatureValidator`.
3. Controllern sparar eventet via `CustomerSyncEventRepository`.
4. Om eventet redan finns returneras success direkt.
5. Ett background job köas med correlation key, till exempel `customersync:hubspot-to-jeeves:{companyId}:{hubSpotObjectId}`.
6. `CustomerSyncBackgroundJobHandler` skickar jobbet till `CustomerSyncFromHubSpotHandler`.
7. Handlern hämtar komplett kunddata från HubSpot.
8. Kunden normaliseras, valideras och matchas.
9. Kunden skapas eller uppdateras i Jeeves.
10. Mapping och eventstatus uppdateras.

Webhook-controller ska inte göra externa skrivningar. Den ska svara snabbt och låta kön hantera retries.

## Idempotensregler

Det här är den viktigaste robusthetsdelen.

- HubSpot webhook-event får bara behandlas en gång per `HubSpotEventId`.
- Jeeves-kund får bara skapa en HubSpot-kund om mapping saknas och matchning inte hittar säker träff.
- HubSpot-kund får bara skapa Jeeves-kund om mapping saknas och matchning inte hittar säker träff.
- Uppdatering ska kunna köras flera gånger med samma data utan ny effekt.
- Correlation key i background jobs ska hindra parallella jobb för samma kund eller batch.
- Mapping ska sparas direkt efter lyckad create i det externa systemet.

## Konflikter och matchning

Matchning ska ske i tydlig prioritetsordning:

1. Befintlig mapping.
2. Organisationsnummer om det finns och är normaliserat.
3. Extern referens om ett system redan lagrar det andra systemets id.
4. Namn och adress endast som osäker kandidat.

Osäkra kandidater ska inte auto-merge:as. De ska markeras som `NeedsReview` eller `SkippedAmbiguousMatch`.

## Konfiguration

Lägg en egen options-klass:

```text
WebApp/Models/Integration/CustomerSync/CustomerSyncOptions.cs
```

Förslag på options:

```json
{
  "CustomerSync": {
    "Enabled": true,
    "PollIntervalMinutes": 60,
    "BatchSize": 100,
    "MaxAttempts": 5,
    "WebhookToleranceMinutes": 5,
    "Companies": [
      {
        "CompanyId": "00000000-0000-0000-0000-000000000000",
        "JeevesCompanyCode": 1,
        "Enabled": true,
        "HubSpot": {
          "BaseUrl": "https://api.hubapi.com",
          "Token": "",
          "WebhookSecret": ""
        }
      }
    ]
  }
}
```

Secrets ska ligga i miljövariabler, Key Vault eller motsvarande. De ska inte checkas in.

## Kommentarstandard i modulen

Varje ny fil ska ha en kort kommentar som säger ansvar och syfte. Kommentarerna ska vara praktiska, inte förklarande på låg nivå.

Exempel:

```csharp
// Orchestrates one Jeeves-to-HubSpot customer sync batch for a single company.
public sealed class CustomerSyncFromJeevesHandler
{
}
```

```csharp
// Stores stable cross-system customer identifiers so retries never need to guess.
public sealed class CustomerSyncMappingRepository : ICustomerSyncMappingRepository
{
}
```

```csharp
// Validates HubSpot webhook authenticity before events enter the background queue.
public sealed class HubSpotWebhookSignatureValidator : IHubSpotWebhookSignatureValidator
{
}
```

Undvik kommentarer som bara upprepar koden, till exempel `// Sets the status`.

## Testplan

Tester bör läggas i `WebApp.Tests/CustomerSync`.

Prioriterade tester:

- normalisering av organisationsnummer, namn, e-post och telefon
- matchningspolicy för mapping, organisationsnummer och osäkra träffar
- idempotent webhook-hantering
- checkpoint flyttas bara efter lyckad batch
- HubSpot-signaturvalidering accepterar giltig signatur och nekar felaktig
- `CustomerSyncJobScheduler` köar inte dubletter när aktivt jobb finns
- Jeeves-to-HubSpot handler skapar, uppdaterar och skippar korrekt
- HubSpot-to-Jeeves handler skapar, uppdaterar och skippar korrekt

Integrationstester mot riktiga externa system ska inte vara standard i CI. Använd fake-klienter för unit tests och separata manuella stagingtester för riktiga API:er.

## Föreslagen implementation i faser

### Fas 1: Grund och state

- skapa optionsmodell
- skapa EF records och `ApplicationDbContext`-mapping
- skapa repository-interfaces och repository-klasser
- skapa domänmodeller, status-enums och normalizer
- lägga tester för normalisering och matchning

Resultat: ingen extern skrivning än, men stabil datagrund.

### Fas 2: Background jobs

- skapa `CustomerSyncBackgroundJobPayload`
- skapa `CustomerSyncBackgroundJobHandler`
- skapa `CustomerSyncJobScheduler`
- registrera modulen i DI
- lägga tester för scheduler och payload

Resultat: modulen kan köa och köra tomma/testade jobb.

### Fas 3: Jeeves till HubSpot

- skapa Jeeves-klient för ändrade kunder
- skapa HubSpot-klient för create/update
- skapa `CustomerSyncFromJeevesHandler`
- spara run, run items, mapping och checkpoint
- börja med dry-run-läge innan skarp skrivning

Resultat: timsynk från Jeeves till HubSpot fungerar kontrollerat.

### Fas 4: HubSpot till Jeeves

- skapa webhook-controller
- skapa signaturvalidering
- skapa event-repository med idempotens
- skapa `CustomerSyncFromHubSpotHandler`
- skapa eller uppdatera kund i Jeeves via tydlig adapter

Resultat: HubSpot-events blir robusta background jobs.

### Fas 5: Driftvy och hårdning

- enkel adminvy eller admin-endpoints för senaste körningar
- replay för en kund eller ett event
- dead-letter-hantering för manuella fel
- rate-limit-hantering mot HubSpot
- tydligare larm på upprepade fel

Resultat: lösningen går att drifta utan att läsa loggar för varje fråga.

## Viktiga tradeoffs

Den här lösningen använder portalens befintliga driftmodell. Det minskar antal deployables och gör att vi återanvänder auth, bolagskontext, logging och background jobs.

Tradeoffen är att portalen får mer ansvar. Därför är isoleringen viktig: inga CustomerSync-regler i FlowEngine-klasser, inga externa skrivningar i controllers och inga stora generella integrationsklasser.

Om volymen eller SLA-kraven växer kan modulen senare lyftas ut till en separat worker/API eftersom den redan har tydliga interfaces runt HubSpot, Jeeves och persistence.
