# Excelimport – arkitektur och produktionskrav

## Nuläge

Excelimporten använder två gemensamma motorer:

- `ExcelImportFixedTemplateEngine` för direktimport.
- `ExcelImportEditSessionEngine` för redigerbara importer.

Budget, prisuppdatering, inköpspris och voucher definierar endast mallens rubriker, normalisering, validering och mappning till stagingrad. Leverantörsprisimporterna behåller sin strömmande motor eftersom de hanterar betydligt större filer och leverantörsspecifika layouter. Trans Auto och Press Kogyo använder samma leverantörspris-adapter för redigering, men separata mallkonfigurationer och stagingtabeller.

Samtliga aktiva importtyper har nu redigeringsadapter:

- voucher.
- budget.
- inköpspris.
- prisuppdatering.
- Trans Auto.
- Press Kogyo.

Gemensamma motorer, tenantkontroll, defensiva filgränser, bakgrundsjobb, retry för transienta fel, radvis resultatlagring och retention finns på plats. Leverantörsprisimporterna läser stora filer strömmande och använder transaktionell bulk staging.

Radvisa valideringsorsaker följer nu med genom förhandsvisning, bakgrundsresultat och persistent paging. Redigeringsläget kan filtrera fram enbart felrader. Trans Autos Halyard- och OH-profiler skiljer dessutom fristående sektionsrubriker och juridiska sidfötter från produktdata utan att dölja rader som innehåller andra produktfält.

## Känd produktionsrisk

Redigering av ett server-paginerat resultat skickar i nuläget endast raderna på den laddade sidan till servern. Det är korrekt för en avgränsad delbatch, men inte för användarflödet "ändra och importera hela filen igen". En stor import med rader på flera sidor får därför inte betraktas som en sammanhängande redigeringssession förrän P0-arbetet nedan är klart.

Denna risk ska lösas med serverlagrad draftdata. Klienten ska aldrig behöva hålla eller skicka hela importen för att slutföra en stor korrigering.

## Produktionskonfiguration

`ExcelImport:BackgroundFileStore:StorageRoot` måste peka på beständig delad lagring som är tillgänglig för både webbinstanser och bakgrundsworkers. Lokal instansdisk accepteras bara utanför produktion. Katalogen ska vara privat, inte ligga under `wwwroot`, och tjänstekontot ska endast ha nödvändiga läs-, skriv- och borttagningsrättigheter.

Portalens Windows App Service använder `D:\home\data\ZeeUCustomerPortal\ExcelImportJobs`. `%HOME%` är App Services beständiga innehållsdelning och är tillgänglig för samtliga instanser. Lagringskravet valideras först när en filoperation startar, så en felaktig driftkonfiguration får inte göra Excelimportens startsida otillgänglig. Uppladdning ska då stoppas med ett säkert användarmeddelande och en supportreferens.

Bakgrundsfiler som blir kvar efter en hård processkrasch tas bort av retentionstjänsten efter `Retention:ExcelImportBackgroundFilesRetentionDays`. Standardvärdet är två dagar. Ett aktivt jobb bör därför aldrig ha en retryperiod som närmar sig retentionstiden.

`ExcelImport:SchemaInitialization:AllowRuntimeInitializationInProduction` är `false` som standard. Databastabeller och index ska installeras av releaseprocessen med samma DDL som finns i `ExcelImportTableInitializationService`. Det administrativa initieringsflödet är avsett för lokal utveckling och kontrollerade testmiljöer. Tillfällig aktivering i produktion kräver ett uttryckligt konfigurationsbeslut och ett databaskonto med DDL-rättigheter.

## Resultatkontrakt

- `ValidRows`: rader som klarade valideringen.
- `InvalidRows`: rader som inte klarade valideringen.
- `StagedRows`: rader som faktiskt skrevs till staging.

Staging är allt-eller-inget för en batch. En batch kan därför ha giltiga rader men `StagedRows = 0` om någon annan rad är felaktig.

Persistenta bakgrundsresultat innehåller summeringar och felmetadata men inte fullständiga radvärden. Rader som behövs för paging och efterföljande redigering lagras i den tenantfiltrerade `q_zu_StagingExcelImportRowResult` med separat retention. En begränsad radförhandsvisning skickas även i den kortlivade runtime-händelsen.

## Skyddsgränser

Standardimporter begränsas till 50 MB och 10 000 datarader. Leverantörspriser tillåter 100 000 datarader. Parsern begränsar även antal kolumner, cellängd samt antal och total expanderad storlek på poster i OpenXML-paket. Ändra gränserna centralt i `ExcelImportResourceLimits` och komplettera alltid med tester.

## Prioriterad förbättringsplan

### P0 – beständiga redigeringssessioner över flera sidor

Mål: användaren ska kunna rätta en stor import över flera sidvisningar och därefter validera och staging-skriva hela sessionen atomiskt.

Genomförande:

1. Inför en redigeringssessionsmodell med session-id, källbatch, importtyp, tenant, användare, status, versionsnummer och utgångstid.
2. Lagra redigerade rader eller rad-deltan server-side. Varje läsning och skrivning ska filtreras på tenant och sessionens ägare eller uttrycklig behörighet.
3. Spara en sida utan att staging-skriva. Sidbyte, filterbyte och omladdning ska behålla tidigare ändringar.
4. Använd optimistic concurrency så att en äldre webbsida inte skriver över nyare ändringar utan tydligt fel.
5. Lägg ett separat kommando för "Validera och importera hela sessionen". Kommandot ska läsa sessionens samtliga rader server-side och behålla allt-eller-inget-kontraktet.
6. Gör slutkommandot idempotent. Dubbelklick eller retry får inte skapa dubbla stagingbatcher.
7. Rensa övergivna sessioner och raddata med konfigurerbar retention. Aktiva sessioner får inte tas bort.
8. Dölj eller spärra helbatchimport för server-paginerade resultat tills det säkra sessionflödet är aktivt.

Acceptanskriterier:

- Ändringar på sida 1 finns kvar efter redigering på sida 2 och efter omladdning.
- En ogiltig rad på valfri sida stoppar hela sessionen och skriver noll stagingrader.
- En giltig session staging-skrivs exakt en gång, även vid upprepad submit eller transient retry.
- En användare eller tenant kan inte läsa eller ändra en annan sessions rader.
- Utgångna och redan importerade sessioner kan inte återanvändas.
- Tester täcker minst två sidor, samtidiga uppdateringar, tenantisolering, avbruten session, retry och dubbel submit.

### P1 – prestanda och kapacitetsgränser

Mål: verifiera att resursgränserna är realistiska och att importen degraderar kontrollerat under last.

Genomförande:

1. Skapa reproducerbara lastfiler för 10 000, 50 000 och 100 000 rader samt filer nära 50 MB.
2. Mät total tid, kötid, parserhastighet, peak memory, databasens bulk-tid och storleken på radresultat.
3. Testa samtidiga importer per tenant och över flera tenants.
4. Fastställ SLO och hårda gränser för kötid, exekveringstid, minnesanvändning och databasbelastning.
5. Verifiera cancellation, timeouts, connection pool och index för paging, retention och sessionuppslag.
6. Justera gränser först efter mätning och dokumentera varje tradeoff.

Acceptanskriterier:

- Ingen testad import överskrider överenskommen minnesbudget.
- En stor import blockerar inte webbrequests eller andra tenants.
- Avbrutna och timeoutade jobb lämnar inte låsta sessioner eller orphan-filer.
- Paging-tiden är stabil när radtabellen innehåller flera samtidiga stora batcher.

### P1 – driftbarhet och återställning

Mål: drift ska snabbt kunna upptäcka, förstå och återställa fel utan tillgång till känsliga radvärden.

Genomförande:

1. Mät antal importer, kötid, exekveringstid, rader per sekund, valideringsfel, stagingfel och retryutfall per importtyp.
2. Lägg larm för växande kö, upprepade stagingfel, hög retryfrekvens, retentionfel och saknad delad fillagring.
3. Använd job-id, importbatch-id, redigeringssession-id och support-id som korrelationsfält.
4. Skapa en administrativ, read-only felsökningsvy utan råa cellvärden eller secrets.
5. Dokumentera återställning för fastnade jobb, otillgänglig lagring, databasavbrott och misslyckad retention.

### P2 – säkerhet och datakvalitet

Mål: stäng återstående missbruks- och dataintegritetsrisker innan bred utrullning.

Genomförande:

1. Lägg integrationstester för feature flag, bolagsbehörighet, tenantbyte och sessionägarskap i samtliga write-flöden.
2. Verifiera filsignatur mot filändelse där formatet tillåter det och behåll skydd mot zip-bomber och överlånga celler.
3. Granska stagingtabellernas constraints, datatyper, index och batchunikhet mot applikationens validering.
4. Säkerställ att loggar, metrics och felmeddelanden aldrig innehåller råa cellvärden, tokens eller anslutningssträngar.
5. Kör dependency- och sårbarhetskontroll för Excel-, CSV- och SQL-bibliotek inför release.

### P2 – kontrollerad produktionssättning

Mål: lansera med tydlig verifiering och möjlighet att snabbt backa.

Genomförande:

1. Leverera databasschema och index som versionsstyrd release-migrering; runtime-DDL ska vara avstängt.
2. Verifiera delad privat fillagring, rättigheter, retention och backup-/restore-rutiner i produktionslik miljö.
3. Lägg det nya flersidiga redigeringsflödet bakom feature flag.
4. Kör smoke-test för alla sex mallar med både giltig och ogiltig fil.
5. Aktivera först för pilottenant, följ SLO och felutfall och bredda därefter stegvis.
6. Dokumentera rollback för feature flag, applikationsversion och databasmigrering.

## Definition of done för Excelimport

Excelimporten kan kallas produktionsredo när:

- samtliga sex mallar klarar sina kontrakts-, fel- och behörighetstester.
- stora redigeringssessioner fungerar över flera sidor utan klientmaterialisering av hela filen.
- helbatchimport är atomisk och idempotent.
- lastmål och minnesbudget är uppmätta och godkända.
- dashboards, larm, retention och återställningsrutiner är verifierade.
- release-migrering, pilot, smoke-test och rollback är genomförda i produktionslik miljö.
