# NotifyMe Hub Status

Kort statusunderlag för vad som redan är flyttat från originalets Jeeves-baserade NotifyMe och vad som återstår innan NotifyMe kan köras skarpt i Hub för kunder som Beulco, Cidan och CTT.

## Sammanfattning

NotifyMe finns nu som portalmodul i Hub. Portalen har sidor för översikt, lista, detalj, historik, statistik, mallbibliotek, editor och testkörning. Konfiguration och körhistorik ligger i portalens Azure DB, medan själva bevakningsfrågan fortfarande körs mot kundens Jeeves-databas via sparad SQL.

Det som främst återstår är inte en helt ny Hub-modul, utan kundinförande: migrering av befintliga NotifyMe-regler, verifierad Jeeves-anslutning, SQL-rättigheter, genomgång av kundspecifika frågor och beslut om huruvida körning ska fortsätta via SQL-anslutning eller byggas om mot Jeeves API.

## Gjort i Hub

### Portalmodul och användarytor

Följande ytor finns i Hub:

- Översikt över NotifyMe-status, aktiva notifieringar, kommande körningar och senaste historik.
- Separat lista med filtrering på status, typ och prioritet.
- Detaljsida per notifiering.
- Historiksida.
- Statistikvy.
- Mallbibliotek baserat på befintliga notifieringar.
- Editor för att skapa och uppdatera NotifyMe-regler.
- Testkörning med valfri mottagare.
- Dashboardkort och Action Center-insikter för notifieringar som förfaller eller kräver manuell åtgärd.

Viktig kod:

- `WebApp/Controllers/NotifyMe/NotifyMeController.cs`
- `WebApp/Views/NotifyMe/*.cshtml`
- `WebApp/Views/Member/Dashboard/Cards/_NotifyMeCard.cshtml`
- `WebApp/Services/ActionCenter/Providers/NotifyMeInsightProvider.cs`

### Portalägd lagring

Hub har egna Azure DB-tabeller för NotifyMe:

- `dbo.q_zu_notcenter`
- `dbo.q_zu_notcenter_log`
- `dbo.q_zu_notcenter_varningstyp`
- `dbo.q_zu_notcenter_varningskat`

Det innebär att NotifyMe-regler, mottagare, schemainställningar, SQL-underlag och körhistorik kan leva i portalens databas i stället för att vara beroende av att originaltabellerna ligger kvar som aktiv konfigurationskälla i Jeeves.

Viktig SQL:

- `SQL/AzureDb/Tables/dbo.q_zu_notcenter.sql`
- `SQL/AzureDb/Tables/dbo.q_zu_notcenter_log.sql`
- `SQL/AzureDb/Tables/dbo.q_zu_notcenter_varningstyp.sql`
- `SQL/AzureDb/Tables/dbo.q_zu_notcenter_varningskat.sql`

### Migreringsstöd från originalet

Det finns script för att exportera originaldata från Jeeves och importera den till portalens Azure DB.

- `SQL/AzureDb/Scripts/NotifyMe.ExportFromJeeves.sql` exporterar NotifyMe-regler, typer, prioriteringar och logg från Jeeves.
- `SQL/AzureDb/Scripts/NotifyMe.ImportToPortal.sql` importerar via staging-tabeller till portalens tabeller.
- `SQL/AzureDb/Scripts/NotifyMe.ImportFromCsv.sql` innehåller exempel/importdata från CSV-flöde.

Det här täcker grundvägen för att flytta befintliga NotifyMe-regler från originalet till Hub.

### Körmotor i Hub

Hub har en egen körmotor för NotifyMe:

- `NotifyMeAutomationWorker` hittar aktiva regler vars `q_zu_notcenter_execdat` har passerat.
- Workern löser kundens aktiva Jeeves-connection via portalens bolags-/connection mapping.
- `PortalNotifyMeExecutionService` hämtar regelns konfiguration från portalens DB.
- Den sparade SQL-frågan i `q_zu_notcenter_select2` körs mot Jeeves-databasen.
- Om frågan ger träff byggs mailinnehåll och notifieringen skickas.
- Körningen loggas i portalens `q_zu_notcenter_log`.
- Nästa körning räknas fram och skrivs tillbaka till portalens `q_zu_notcenter`.

Viktig kod:

- `WebApp/Services/NotifyMe/Background/NotifyMeAutomationWorker.cs`
- `WebApp/Services/NotifyMe/Execution/PortalNotifyMeExecutionService.cs`
- `WebApp/Services/NotifyMe/Scheduling/NotifyMeScheduleCalculator.cs`
- `WebApp/Models/NotifyMe/NotifyMeAutomationOptions.cs`

### Felhantering och retries

Körmotorn har grundläggande felhantering:

- Testkörningar loggar misslyckanden och returnerar fel till användaren.
- Schemalagda körningar gör automatiska retries vid tekniska fel.
- Fel som saknad mottagare, saknat SQL-underlag, saknad notifiering och ej stödda dynamiska mottagare behandlas som konfigurationsfel.
- Efter uttömda retries markeras körningen som manuell åtgärd i historiken.

### Konfiguration

NotifyMe worker är konfigurerad i `WebApp/appsettings.json`:

```json
"NotifyMe": {
  "Automation": {
    "PollIntervalMinutes": 5,
    "BatchSize": 25
  }
}
```

## Skillnad mot originalet

Originalet utgår från NotifyMe-tabeller och logik i Jeeves-miljön. Hub-lösningen har flyttat applikationsytan och konfigurationen till portalen:

- Originalets data exporteras från Jeeves och importeras till portalens Azure DB.
- Hub läser och skriver NotifyMe-regler i `PortalNotifyMeRepository`.
- Den äldre `JeevesNotifyMeRepository` finns kvar som kodspår, men DI registrerar `PortalNotifyMeRepository`.
- Save/edit är portalägt. Jeeves-backed save är uttryckligen inte längre stödd i `JeevesNotifyMeRepository`.
- Själva dataträffen är fortfarande Jeeves-baserad eftersom varje regel innehåller fri eller kundspecifik SQL.

Den viktiga gränsen är alltså: Hub äger NotifyMe-konfiguration, UI, schema, mail, logg och retries. Jeeves är fortfarande datakälla för den SQL som avgör om en notifiering ska skickas.

## Kvarstår i Hub inför kunddrift

### 1. Kundmigrering

För varje kund behöver vi:

- Exportera NotifyMe-regler från kundens Jeeves-miljö.
- Ladda exporten till staging-tabeller i portalens Azure DB.
- Köra importscriptet till portalens NotifyMe-tabeller.
- Kontrollera att `foretagkod` matchar portalens bolagsmapping.
- Kontrollera att typ- och prioritetslistor följde med.
- Bestämma om historisk logg ska importeras eller om kunden startar med tom Hub-historik.

### 2. Connection mapping och runtime

Workern kräver att portalens bolag kan mappas till rätt Jeeves-connection:

- `Identity.Companies.DefaultJeevesCompanyCode` eller `Identity.CompanyJeevesCompanies.CompanyCode` måste matcha `foretagkod`.
- Det måste finnas en aktiv connection mapping för bolaget.
- Connection string måste kunna lösas av `IConnectionStringResolver`.
- Hub-miljön måste ha nätverksåtkomst till kundens Jeeves SQL Server.

Detta bör verifieras tidigt för Beulco, eftersom Beulco troligen är först ut i augusti.

### 3. SQL-underlag per notifiering

Varje regel behöver granskas:

- `q_zu_notcenter_select2` måste vara en giltig SELECT-fråga i kundens Jeeves-miljö.
- Frågan måste fungera med den SQL-user som Hub använder.
- Frågan bör inte kräva temporär sessionstate eller Jeeves-klientkontext som inte finns från Hub.
- Tunga frågor behöver prestandagranskas så att workern inte belastar kundens Jeeves onödigt.
- Resultatkolumnerna bör vara begripliga i mailtabellen som Hub genererar.

### 4. Begränsningar som måste beslutas

Kända begränsningar i portalmotorn:

- Dynamiska mottagare stöds inte ännu. Om `q_zu_notcenter_dyn_adress = '1'` stoppar körningen med konfigurationsfel.
- Körmotorn bygger mailinnehåll generiskt från resultatet av SQL-frågan. Om originalet hade specialformaterade mail per regel behöver det jämföras.
- Schemalogiken i Hub stödjer daglig, veckovis, månadsvis och timvis körning baserat på befintliga schema-/schedule-koder. Kundernas faktiska kodvärden behöver stämmas av mot originalet.
- Hub kör sparad SQL med timeout 60 sekunder.

### 5. Behörigheter och åtkomst

Inför drift behöver vi stämma av:

- Vilka roller som får se NotifyMe.
- Vilka roller som får skapa, ändra och testköra regler.
- Om kunderna själva ska kunna editera SQL-underlag eller om det ska vara ZeeU-administrerat.
- Om alla mailmottagare ska vara tillåtna eller om vi ska ha domän-/kundbegränsning.

I nuläget kräver sparning och testkörning `Administrator` eller `SuperUser`.

### 6. Mail och övervakning

Före go-live behöver vi:

- Verifiera att mail skickas korrekt från aktuell Hub-miljö.
- Säkerställa att tekniska fel loggas och fångas upp.
- Gå igenom hur Action Center-insikter ska användas av kund.
- Bestämma vem som ansvarar för regler som hamnar i manuell åtgärd.

### 7. Testplan per kund

Minsta testpaket per kund:

- Läs in migrerade regler och jämför antal mot originalet.
- Öppna översikt, lista, detalj, historik och editor.
- Testkör 2-3 regler med override-mottagare.
- Kör en regel som ger noll träff och kontrollera att ingen varning skickas.
- Kör en regel som ger träff och kontrollera mail, logg och nästa körning.
- Testa fel på SQL/mottagare i testmiljö och kontrollera att felet visas säkert.
- Låt workern köra schemalagt i testmiljö och kontrollera att `q_zu_notcenter_execdat` flyttas fram.

## Jeeves/SQL som behövs

Minimikrav för SQL-spåret:

- Läsrättigheter till tabeller/vyer/SP:ar som varje NotifyMe-regel använder.
- Eventuella kundspecifika vyer eller stödtabeller måste finnas i kundens Jeeves-databas.
- Stabil SQL-anslutning från Hub till kundens Jeeves SQL Server.
- En SQL-user med minsta rimliga behörighet, helst read-only mot källdata.
- Beslut om vem som äger ändringar i de sparade SQL-frågorna.

Om reglerna bygger på originalets NotifyMe-tabeller i Jeeves räcker det inte att bara installera Hub-tabeller. Då behöver själva bevakningsfrågorna också kunna köras mot kundens Jeeves-data.

## Jeeves API-frågan

Dagens Hub-lösning kör inte NotifyMe via Jeeves API. Den kör sparad SQL mot Jeeves.

Att köra via Jeeves API är möjligt först om vi väljer ett av dessa spår:

1. Jeeves tillhandahåller en säker, tenantstyrd query-endpoint för NotifyMe-läsningar.
2. Varje NotifyMe-regel paketeras som godkänd vy/stored procedure/API-resurs.
3. Vi bygger om NotifyMe från fri SQL till fasta domänhändelser och API-kontrakt.

Alternativ 1 är flexibelt men säkerhetskänsligt, eftersom fri SQL via API måste begränsas hårt. Alternativ 2 är säkrare men kräver mer kundspecifik installation. Alternativ 3 är mest långsiktigt men störst omskrivning och passar sämre om målet är Beulco i augusti.

Rekommendation inför första kund är att fortsätta med SQL-anslutning för NotifyMe, men ta Jeeves API-frågan med Björn/Jeeves innan vi lovar en API-baserad väg.

## Förslag på mötesagenda

1. Bekräfta vilka kunder som ska in först: Beulco, Cidan, CTT.
2. Gå igenom om respektive kund har befintliga NotifyMe-regler i Jeeves.
3. Bestäm om historik ska migreras.
4. Kontrollera bolagskoder och connection mapping i Hub.
5. Välj 2-3 Beulco-regler för första tekniska test.
6. Kontrollera om någon regel använder dynamiska mottagare.
7. Bestäm ansvar för SQL-granskning och eventuella kundspecifika vyer/SP:ar.
8. Ta beslut om första release körs via SQL-anslutning.
9. Skicka API-frågan till Björn/Jeeves: finns en säker väg att köra NotifyMe-läsningar via Jeeves API när reglerna går mot många olika tabeller?

## Öppna frågor

- Vilka NotifyMe-regler finns hos Beulco i dag och vilka måste vara med i första drift?
- Använder någon kund dynamiska mottagare?
- Finns specialformaterade mail i originalet som Hub måste matcha?
- Ska kunder kunna skapa/ändra SQL själva eller bara ZeeU?
- Ska SQL-reglerna versionshanteras eller räcker portalens tabellhistorik/logg?
- Behöver vi kundspecifika allowlists för tabeller, vyer eller mottagardomäner?
- Är Jeeves API ett krav för första release, eller en framtida målbild?
