# Excelimport

Den här modulen tar emot importfiler, validerar dem mot respektive importmall och skriver godkända rader till staging. Importen ska vara tydlig för användaren, begränsad i datalagring och säker nog för produktionsdata.

## Ansvar

Excelimporten ansvarar för:

- att kontrollera användarens tenant, aktivt bolag och modulbehörighet innan import.
- att begränsa filtyper och filstorlek innan filen köas.
- att läsa filen via rätt parser för filformatet.
- att validera rubriker, datatyper och obligatoriska värden.
- att visa importerade rader tillfälligt så användaren kan granska och återimportera.
- att rensa temporära uploadfiler efter bakgrundsjobbet.

Modulen ska inte användas som permanent lagring av kunddata. Summeringar och mindre förhandsvisningar hålls transient i minne. Fullständiga radresultat som behövs för paging och efterföljande redigering lagras tenantfiltrerat i staging med begränsad retention.

## Stödda filformat

Aktiva format:

- `.xls`
- `.xlsx`
- `.xlsm`
- `.csv`

Äldre binära `.xls` läses via `ExcelDataReader`. Vi läser inte `.xls` via ClosedXML eftersom det skapar opålitliga fel och falsk trygghet.

## Parserstruktur

Importläsningen går via `IExcelImportWorkbookReader`.

Den väljer en parser via `IExcelImportWorkbookFileParser`:

- `OpenXmlExcelImportWorkbookParser` läser `.xlsx` och `.xlsm` radvis via `ExcelDataReader`.
- `LegacyXlsExcelImportWorkbookParser` läser `.xls`.
- `CsvExcelImportWorkbookParser` läser `.csv` rad för rad.

Varje parser returnerar samma modell:

- `RowHeaders`
- `Rows`
- `Errors`

Det gör att budget, voucher, inköpspris och prisuppdatering kan dela samma läsflöde men ha egna regler.

## Valideringsregler

Varje importtyp definierar en `ExcelImportWorkbookDefinition` med:

- förväntade kolumnrubriker.
- rubrikvalidering.
- radmappning.
- kontroll för tomma rader.

Gemensamma regler:

- rubriker måste matcha mallen i rätt ordning.
- extra rubriker stoppas.
- tomma rader ignoreras.
- obligatoriska fält valideras per importtyp.
- numeriska värden valideras per importtyp.
- import stoppas om någon rad är ogiltig.

## Säkerhet

Kontroller i controller:

- `[Authorize]` på modulen.
- antiforgery på POST-actions.
- tenant guard.
- feature flag.
- bolagsbehörighet.
- filändelsekontroll.
- max filstorlek på 50 MB.

Parserkontroller:

- ingen formelutvärdering.
- celler läses som text.
- `.xlsx` och `.xlsm` läses radvis utan att hela arbetsboken materialiseras med ClosedXML.
- OpenXML-paket begränsas i antal interna delar och total expanderad storlek.
- standardmallar har max 10 000 datarader, 256 kolumner och 10 000 tecken per cell.
- `.xls` läses via `ExcelDataReader` som binärt BIFF-format.
- CSV läses streamande.
- CSV har max 10 000 rader.
- CSV har max 10 000 tecken per cell.
- `.xls` har max 10 000 rader.
- `.xls` har max 10 000 tecken per cell.
- tekniska parserfel returneras som säkra användarfel.

Datahantering:

- temporär uploadfil sparas bara för bakgrundsjobbet.
- temporär uploadfil tas bort i `finally`.
- sammanfattningar och mindre radförhandsvisningar visas via transient minnescache.
- större leverantörsprisimporter lagrar sidindelade radresultat i staging med sju dagars retention.
- transient status har 30 min sliding expiration och 2 h absolut expiration.
- endast de senaste 8 importhändelserna per bolag behålls i minne.

## Redigeringsläge

Voucher, budget, inköpspris, prisuppdatering, Trans Auto och Press Kogyo har redigeringsadapter. Ett nyligt resultat kan öppnas i redigeringsläge efter både lyckad import och stoppad validering när radresultat finns. Små förhandsvisningar kommer från runtime-status och fullständiga paginerade resultat hämtas från den tenantfiltrerade radresultattabellen.

Regler:

- åtkomst till radresultat kräver rätt tenant och bolagsbehörighet.
- radresultat och framtida drafts är temporär data med retention, inte permanent kunddatalagring.
- återimport skapar en ny importbatch och visar den uppdaterade versionen i importstatus.
- ogiltiga rader visar sin valideringsorsak och kan filtreras fram med "Visa endast rader med fel".
- användaren kan avbryta redigering utan att skriva något.

Känd begränsning:

- server-paginerad redigering postar i nuläget endast den laddade sidan. Stora korrigeringar över flera sidor kräver därför den beständiga redigeringssession som är P0 i `excel-import-production.md`.

## Prestanda

Nuvarande styrkor:

- import körs i bakgrundsjobb.
- status pollas separat från sidan.
- CSV, `.xls`, `.xlsx` och `.xlsm` läses radvis.
- tomma rader filtreras innan validering/import.
- standardmallarna begränsas till 10 000 rader och leverantörsprisimporterna till 100 000 rader.

Parsern läser filen strömmande, men standardflödet behåller validerade rader i minne fram till staging för att kunna stoppa hela importen om någon rad är felaktig. Radgränserna håller den minnesanvändningen förutsägbar.

Leverantörsprisimporterna använder två pass för större filer. Första passet validerar och skriver radresultat i mindre batcher. Om hela filen är giltig läser andra passet filen igen och strömmar stagingrader direkt genom en transaktionell `SqlBulkCopy`. Det ger allt-eller-inget utan att materialisera upp till 100 000 stagingrader i webbprocessens minne.

## `.xls`-säkerhet

`.xls` är ett äldre binärt Excel-format. Parsern ska därför hållas isolerad från OpenXML-flödet och endast läsa cellvärden.

Regler:

- läs endast första worksheeten.
- läs cellvärden som data.
- kör inte makron.
- utvärdera inte formler.
- stoppa korrupta eller ogiltiga filer med säkert felmeddelande.
- återanvänd samma rubrik- och radvalidering som övriga format.
- behåll samma filstorleksgräns och transient datalagring som övriga importer.
