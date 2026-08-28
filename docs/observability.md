# Observability i ZeeU Customer Portal

Det här dokumentet beskriver portalens gemensamma standard för teknisk telemetri,
driftmätning och affärshändelser. Målet är att ett fel ska kunna följas från en
supportreferens till berörd request, tenant, modul och dependency utan att känslig
kunddata lagras i loggar.

## Nuläge och avgränsning

Inventeringen den 24 juni 2026 visade följande:

- `ILogger<T>` används i 71 filer och är standard framåt.
- `ILoggerManager` finns kvar i 12 filer och fasas ut modulvis.
- 18 äldre anrop använder interpolerade loggsträngar och flera loggar endast
  `ex.Message`.
- NLog används av `ILoggerManager` och skriver lokala textfiler. Det behålls
  tillfälligt för bakåtkompatibilitet men är inte primär observability-lösning.
- Den tidigare `ExceptionLoggingMiddleware` var inte inkopplad.
- `EventLogs` används främst av Orders, Invoices, WebApproval och adminverktyg.
  Tabellen är inte ett generellt tekniskt logglager.
- Correlation-id fanns i vissa modeller men var ofta `null`.

## Aktuell status för loggningsförbättringen

Uppdaterat 2026-06-24.

Det här är redan gjort:

- BankReconciliation är sanerad på kundsidan. Rå `ex.Message` visas inte längre i
  bankflödet, interna fel loggas med SupportId och sanerad diagnostik, och
  banktestsviten gick grönt efter ändringarna.
- NotifyMe exponerar inte längre rå exceptiontext i användarvända fel för save
  och testkörning.
- Member-integrationernas hälsokontroller svarar nu med generiska felmeddelanden
  i stället för att läcka exceptiontext.
- Docker-starten är fixad genom att restore/build nu kan lösa paketkonflikten
  runt `System.Diagnostics.DiagnosticSource`, vilket gör att observability-
  beroenden faktiskt kan byggas lokalt.

Det som återstår i det här loggningsspåret:

- Övriga kundvända portalflöden som fortfarande skickar vidare `ex.Message` eller
  annan rå exceptiontext till UI eller API-svar ska gås igenom och saneras på
  samma sätt.
- Särskilt behöver vi kontrollera de flöden som redan hittades i sökningen,
  exempelvis WebApproval, Purchase, DocumentSigning, ExcelImport, AI och
  administrativa integrationsytor där fel fortfarande kan läcka för mycket
  teknisk detalj.
- Vissa interna/admin-resultat objekt returnerar fortfarande `Message`
  direkt från exceptionen. De är inte samma risk som kundvända fel, men de ska
  bedömas om de visas vidare i gränssnitt eller API.

Det här är uttryckligen utanför nuvarande scope:

- FlowEngine. Jag räknar inte in det i den här förbättringen just nu enligt
  avgränsningen du gav.

## Tre separata spår

1. **Teknisk telemetri:** Azure Monitor Application Insights via OpenTelemetry.
   Här finns requests, dependencies, exceptions, traces och tekniska loggar.
2. **Metrics och larm:** Azure Monitor/Application Insights. Metrics används för
   tillförlitliga trösklar och trendövervakning.
3. **Affärslogg och audit:** SQL `EventLogs` eller framtida dedikerad auditmodell.
   Endast händelser som ska visas i admin eller sparas som revisionshistorik hör
   hemma här.

Fulla stack traces, debugrader, HTTP-payloads och API-svar ska aldrig dupliceras
till `EventLogs`. En framtida ren auditmodell ska vara append-only och skiljas
från dagens raderbara felloggar innan raderingsfunktionen tas bort.

## Konfiguration

Azure-export aktiveras med någon av följande inställningar:

```text
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...;IngestionEndpoint=...
```

eller:

```json
{
  "AzureMonitor": {
    "ConnectionString": "..."
  }
}
```

Connection string ska sättas som App Service-miljövariabel eller via säker
konfigurationsprovider. Den får inte checkas in. Om den saknas startar portalen
utan Azure-export, vilket är avsiktligt för lokal utveckling.

## Request- och supportkontext

`RequestObservabilityMiddleware` sätter följande strukturerade egenskaper där
värden finns:

- `TraceId`, `CorrelationId`, `SupportId`
- `CompanyId`, `JeevesCompanyCode`, `UserId`
- `Module`, `Operation`
- `Environment`, `ReleaseVersion`
- `HttpMethod`, `RequestPath`

Klienten får `X-Correlation-ID` och `X-Support-ID` i svaret. Ett inkommande säkert
`X-Correlation-ID` återanvänds; tomma, för långa eller otillåtna värden ersätts.
Request bodies, cookies och authorization headers läses inte av middleware.

Bakgrundsjobb använder logging scope och en egen `Activity` med `JobId`,
`JobType`, `CompanyId` och eventuell `CorrelationId`.

ExcelImport, FlowEngine, CustomerSync och DocumentSigning kompletterar det generiska
jobbspåret med domänfält som importtyp, Jeeves-bolag, operation, extern tjänst och
behandlade poster. Feltexter som sparas i jobbresultat sanitiseras. ExcelImport
loggar filstorlek men inte filnamn eller filinnehåll.

`JeevesSqlExecutor` skapar ett dependency-span för varje central Jeeves-operation.
SQL-text, parametrar, faktisk serveradress och connection string läggs aldrig på
span eller loggrad. Normala start/slut-händelser ligger på debugnivå. Fel loggas
som strukturerade errors och operationer över två sekunder som warnings, med
duration, operation, failure kind och stabil error code.

## Felhantering

`PortalExceptionHandler` hanterar oväntade exceptions centralt. Exceptionobjektet
loggas med `UNHANDLED_EXCEPTION`, supportreferens och requestkontext. API-anrop får
säker `ProblemDetails`; webbanrop får en neutral HTML-sida. Stack trace och intern
feltext skickas aldrig till klienten.

Fånga endast exceptions lokalt när flödet kan återhämta sig, översätta felet till
ett domänresultat eller lägga till konkret information. Kasta annars vidare och
låt den centrala hanteraren logga felet en gång.

## Söka ett supportärende

Sök i Application Insights Logs på support-id:

```kusto
union AppTraces, AppExceptions, AppRequests, AppDependencies
| where tostring(Properties.SupportId) == "4f892abc"
   or tostring(Properties["portal.support_id"]) == "4f892abc"
| order by TimeGenerated asc
```

Sök på correlation-id:

```kusto
union AppTraces, AppExceptions, AppRequests, AppDependencies
| where tostring(Properties.CorrelationId) == "customer-request-42"
   or tostring(Properties["portal.correlation_id"]) == "customer-request-42"
| order by TimeGenerated asc
```

Tabell- och kolumnnamn kan visas med de klassiska namnen `traces`, `exceptions`,
`requests` och `dependencies` beroende på Application Insights workspace-läge.

## Logging i en ny modul

Använd `ILogger<T>`, message templates och stabila egenskapsnamn:

```csharp
_logger.LogInformation(
    "Customer sync completed. {CompanyId} {DurationMs} {UpdatedCount} {Result}",
    companyId,
    timer.ElapsedMilliseconds,
    updatedCount,
    "Succeeded");

_logger.LogError(
    exception,
    "Jeeves query failed. {ErrorCode} {CompanyId} {Operation}",
    PortalErrorCodes.JeevesQueryFailed,
    companyId,
    "LoadOrders");
```

Använd inte interpolerade loggsträngar eller enbart `exception.Message`:

```csharp
// Fel: tappar struktur och stack trace.
_logger.LogError($"Sync failed for {companyId}: {exception.Message}");
```

Logga start och slut för långvariga jobb och integrationsflöden. Ta med duration,
resultat, antal lyckade/misslyckade poster, tenant, external system och error code.
Logga inte varje post i stora batcher.

## Data som aldrig ska loggas

- access- eller refresh tokens, API-nycklar och lösenord
- connection strings och authorization/cookie headers
- personnummer, BankID-data eller personnamn när `UserId` räcker
- fullständigt mail-, dokument- eller requestinnehåll
- fullständiga externa API-svar
- SQL-parametrar som innehåller person-, kund- eller affärsdata

`IntegrationLogSanitizer` ska användas när en begränsad extern diagnostiktext
måste loggas. Sanitization är ett extra skydd och ersätter inte dataminimering.

## Rekommenderade dashboards och larm

Dashboard:

- error rate per bolag, modul, release och dependency
- misslyckade och långsamma Jeeves-/SQL-anrop
- misslyckade externa dependencies
- p95/p99 för requests och dependencies
- bakgrundsjobb som misslyckas, retryas eller saknar heartbeat
- CustomerSync partial failures och behandlade poster
- autentiserings- och behörighetsfel

Larm ska baseras på kritiska fel eller upprepning inom ett tidsfönster, exempelvis
fem fel för samma bolag/modul på tio minuter. Skicka inte e-post för varje enskilt
exception. Konfigurera sampling för lyckade traces samt budget- och volymlarm innan
produktion; errors och relevanta warnings ska behållas.

## Fortsatt migrationsordning

1. Övriga portalflöden som fortfarande läcker rå exceptiontext till kundvända
   svar, börja med WebApproval, Purchase, DocumentSigning och ExcelImport.
2. Admin- och integrationsytor där `Message` fortfarande byggs direkt från
   exceptioner och senare kan visas i UI.
3. Övriga Jeeves-repositories som fortfarande använder `ILoggerManager` lokalt.
4. Orders och Invoices; ersätt manuella `CorrelationId = null`.
5. HubSpot, Oneflow, OpenAI och övriga externa klienter.
6. Separera append-only audit events från dagens tekniska `EventLogs`.

Varje migrationssteg ska ha tester för exceptionobjekt, stabil error code,
strukturerade fält och redaction.
