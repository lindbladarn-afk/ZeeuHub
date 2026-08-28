FlowEngine – plan för omstrukturering och modularisering
Syfte

Detta dokument beskriver hur vi stegvis strukturerar om FlowEngine i ZeeU.CustomerPortal för att göra lösningen tydligare, mindre kopplad och enklare att vidareutveckla.

Målet är inte att skriva om allt samtidigt. Målet är att steg för steg gå från en implementation där UI, controller, operationmetadata, request-byggande och exekvering delvis sitter ihop, till en struktur där ansvar är tydligare separerade och där samma operation definieras och hanteras konsekvent genom hela flödet.

Arbetet ska ge oss:

en tydlig och gemensam modell för FlowEngine-operationer
tunnare controller- och view-lager
mindre och mer testbara services
återanvändbara UI-byggblock
renare integrationslager per system
bättre kontroll över jobbstatus, historik och degraded mode
en stabil grund för fortsatt utveckling av FlowEngine och eventuellt delad infrastruktur med andra moduler

Detta dokument kompletterar flowengine-native-migration.md, som fokuserar på funktionell migrering och parity. Det här dokumentet fokuserar på intern struktur, ansvarsfördelning, återanvändning och underhållbarhet.

Målbild

När arbetet är klart ska FlowEngine ha en struktur där:

varje operation har en tydlig och gemensam definition
UI, routing och execution utgår från samma operationkatalog
HTTP- och MVC-specifik logik hålls i controller- och view-lager
execution-lagret arbetar med normaliserade requests och är oberoende av HttpContext
externa integrationer är uppdelade i tydliga lager för auth, klient, query/mutation, mapping och orchestration
stora vyer är uppdelade i sektioner och återanvändbara partials
historik, status och scheduler-relaterade byggblock följer samma struktur mellan sektioner
degraded mode och persistensproblem är explicita i stället för tysta
Arkitekturprinciper
1. En operation definieras en gång

Operationens identitet, label, section, summary, readiness, handlerkoppling och UI-metadata ska inte beskrivas i flera olika kartor eller switchar.

2. HTTP ska stanna i UI- och controllerlagret

Services i execution- och integrationslager ska inte läsa HttpContext, Request.Form eller annan MVC-specifik state. De ska få färdiga, normaliserade requests.

3. UI bryts ut efter mönster, inte efter slump

Återkommande delar som history-panel, status-pill, scheduler-popover, operation card och jobbdetalj ska vara egna byggblock. Sektioner ska byggas av dessa snarare än duplicerad markup.

4. Externa integrationer delas i ansvar

Auth, transport, queries, parsing, mapping, Jeeves-bridging och operation-orchestrering ska inte blandas i samma klass om det går att separera tydligt.

5. Vi refaktorerar där nyttan är störst först

De största filerna, de mest duplicerade mönstren och de mest ansvarstunga klasserna tas först. Vi undviker att generalisera för tidigt.

6. Ingen generell plattform byggs innan FlowEngine själv är ren

Vi delar bara kod med andra moduler när FlowEngine-strukturen först blivit stabil och nyttan är tydlig.

Nuvarande problemområden

Följande delar driver mest komplexitet i nuläget:

WebApp/Views/Integration/FlowEngine.cshtml
stor monolitisk vy
blandar state-beräkning, sektionstyrning och rendering
duplicerar samma UI-mönster för Centra, Shopify, Jeeves, Akeneo och historik
innehåller för mycket presentationsnära logik
WebApp/Controllers/IntegrationController.cs
stor controller med många nästan identiska POST-actions
upprepar permission checks, request-building, redirect-logik och messaging
binder controllern hårt till operationernas detaljer
FlowEngineExecutionService
har för många ansvar i en klass
blandar defaults, flaggnormalisering, command line-bygge, dispatch och jobbresultat
är för nära HTTP-specifik input
FlowEngineModuleService
beskriver operationer för UI separat från execution-lagret
riskerar drift mellan vad UI visar och vad runtime faktiskt stöder
systemservices som FlowEngineShopifyReadService och motsvarande
blandar auth, transport, validering, lookup, mapping och output
är svåra att testa och svåra att förändra isolerat
FlowEngineDbJobStore
fallback-beteende vid DB-fel är för implicit
riskerar att maskera riktiga driftproblem och skapa oklar historik
Vad omstruktureringen ska uppnå

Omstruktureringen ska framför allt lösa fyra typer av problem:

1. Duplicerad struktur

Samma operation eller UI-mönster ska inte beskrivas på flera ställen.

2. Otydliga ansvar

Controller, view, execution och integrationslager ska ha tydligare roller.

3. För stora enheter

Stora vyer och stora serviceklasser ska delas upp i mindre, tydligare byggblock.

4. Svårt att ändra säkert

Nya operationer, ändrade labels, ny routing eller justerad execution ska kunna införas utan att vi behöver röra flera separata kartor och switchar.

Omfång och avgränsningar

Det här arbetet handlar om struktur, inte om att ändra funktionell behavior i onödan.

Vi ska därför undvika att i samma steg:

skriva om all affärslogik samtidigt
byta integrationskontrakt för alla system på en gång
bygga ett generiskt ramverk för andra moduler innan FlowEngine är stabilt
kombinera stora UI-förändringar med djupa beteendeförändringar i samma leverans

Grundregeln är att varje etapp ska ge tydligare struktur utan att öka risken mer än nödvändigt.

Refaktoreringsordning

Arbetet delas upp i följande etapper:

gemensam operationmodell
modulärt UI och partials
tunnare controller och centraliserat request-byggande
renare execution-dispatch
uppdelat integrationslager per system
tydligare jobbstore och degraded mode
utvärdering av delad infrastruktur mellan moduler
Etapp 1 – Gemensam operationmodell
Mål

Införa en gemensam operationkatalog som blir sanningskälla för UI, routing och execution.

Varför

I dag definieras delar av operationerna i flera lager. Det ökar risken för drift mellan:

vad UI visar
vad controller routar
vad execution-lagret faktiskt kan köra
Resultat

Vi inför exempelvis:

FlowEngineOperationDefinition
IFlowEngineOperationCatalog
FlowEngineOperationCatalog
IFlowEngineOperationHandler

Operationdefinitionen ska minst kunna bära:

operation-id/key
label
summary
section
slice eller kategori
readiness
UI-typ
handlerkoppling
eventuella standardvärden eller metadata för historik/routing
Klart när
alla operationer finns i katalogen
UI läser operationmetadata från samma källa
execution dispatchar via katalog/handler i stället för stor switch
nya operationer kan läggas till utan att tre olika kartor behöver uppdateras
Etapp 2 – Modulärt UI och återanvändbara vybyggblock
Mål

Bryta upp FlowEngine.cshtml i sektioner och återanvändbara partials.

Varför

Nuvarande vy innehåller både state-beräkning och stora mängder markup med tydlig duplication.

Riktning

Vi bryter först ut byggblock som redan återkommer, till exempel:

workbench header
status pill
history panel
history detail
scheduler popover
operation card
section shell

Därefter kan sektioner som Centra, Shopify, Jeeves och Akeneo brytas ut till egna partials där det ger läsbarhet.

Viktig princip

Vi skapar inte specialkomponenter i onödan. Vi återanvänder portalens etablerade byggstenar där det går och bryter främst ut verklig duplication.

Klart när
huvudvyn främst komponerar partials
minst tre större duplicerade UI-mönster är brutna ut
history och status renderas konsekvent mellan sektioner
view state är tydligt reducerad i .cshtml
Etapp 3 – Tunnare controller
Mål

Flytta repetitiv FlowEngine-logik ut ur IntegrationController.

Varför

Controllern upprepar i dag samma slags arbete för många operationer:

accesskontroll
user/session-upplösning
request-building
redirect
success/error-messaging
Resultat

Vi centraliserar detta i exempelvis:

FlowEngineRequestFactory
FlowEngineSectionRouter
FlowEngineControllerRunner

Controllern ska främst:

validera åtkomst
översätta input till operation + payload
anropa runner
redirecta till rätt sektion
Klart när
duplicerad controllerkod har minskat tydligt
request-building sker centralt
redirect-logik är konsekvent
success/error-mönstret är enhetligt
Etapp 4 – Renare execution-lager
Mål

Göra execution-lagret testbart, mindre kopplat och tydligare uppdelat.

Varför

Nuvarande execution-service hanterar för många ansvar samtidigt.

Resultat

Vi bryter ut ansvar som till exempel:

FlowEngineRequestNormalizer
FlowEngineCommandLineBuilder
FlowEngineOperationDispatcher
FlowEngineJobRunner

Det gör att execution-lagret kan arbeta med normaliserade requests och fokusera på orchestration, inte på HTTP-formulär eller MVC-detaljer.

Klart när
execution-lagret är oberoende av HttpContext
globala flags och forminput normaliseras innan dispatch
command line-bygge är ett separat ansvar
handler-dispatch är isolerad från request-parsning
Etapp 5 – Uppdelning av integrationslager per system
Mål

Dela upp stora systemservices i tydligare lager.

Varför

Stora integrationsservices blandar i dag flera ansvar: auth, klient, queries, mapping, validering, Jeeves-lookup och outputformatering.

Riktning

Per system bryter vi ut sådant som naturligt hör ihop, till exempel:

Shopify
auth-klient
GraphQL-klient
scope-probe
query-katalog
ordermapper
bridge mot Jeeves
Centra
GraphQL-klient
query-katalog
ordermapper
returnmapper
bridge mot Jeeves
Jeeves
samla gemensamma read/write-mönster
göra lookup- och importflöden mer konsekventa
Akeneo
håll enklare tills behovet finns, men bryt ut klient/query/mapping när storleken motiverar det
Klart när
stora services har delats i tydligare lager
auth, transport, parsing, mapping och orchestration inte längre är hopblandade
handlers blir tunnare och mer fokuserade
Etapp 6 – Jobbstore, historik och degraded mode
Mål

Göra driftlägen och persistensproblem tydliga och kontrollerade.

Varför

Tyst fallback till in-memory vid DB-fel skapar osäkerhet kring historik, audit trail och felsökning.

Resultat

Vi gör fallback-policy explicit och miljöberoende där det behövs. UI eller loggning ska tydligt visa när historik körs i degraded mode.

Status nu

- `FlowEngineJobSnapshot` bär nu explicit storage-status för `Persistent` respektive `InMemoryFallback`
- historikpanelen visar en tydlig degraded-varning när fallback-data förekommer
- detaljvyn visar också om ett jobb ligger i temporärt in-memory-läge
- fallback-policyn är fortfarande samma tekniskt, men den är inte längre tyst i UI:t

Klart när
DB-fel inte längre sker tyst
degraded state syns i UI eller logg
produktionens fallback-policy är ett medvetet beslut
Etapp 7 – Delad infrastruktur mellan moduler
Mål

Identifiera vad som faktiskt bör delas mellan FlowEngine och andra moduler.

Varför

Det finns sannolikt byggblock som på sikt kan återanvändas, men det ska komma efter att FlowEngine blivit renare.

Kandidater
runtime context-resolution
scheduler metadata
history/status-paneler
status pills
degraded mode summary
retry/attempt-resultatmönster
Klart när
minst ett byggblock eller en abstraktion kan användas på minst två ställen
nyttan är konkret
vi inte har skapat ett övergeneriskt mellanlager utan tydligt syfte
Rekommenderad genomförandeordning
Fas 1
operationkatalog
header/history/status/scheduler som partials
Fas 2
tunnare controller
execution utan HttpContext
Fas 3
Shopify-uppdelning
jobbstore/degraded mode
Fas 4
Centra-uppdelning
utvärdera delning med andra moduler

Status just nu

Klart
operationkatalog införd och kopplad till FlowEngineModuleService
HistoryPanel, StatusPill och scheduler-popover brutna ut som partials
Centra- och Shopify-sektionerna brutna ut till egna partials
första tunnare controllersteget gjort via gemensam execute-helper i IntegrationController
request-normalisering för workbench-flags brutits ut från execution-lagret
FlowEngineExecutionService läser inte längre HttpContext eller Request.Form
import order-workflow brutet ut till egen service för session-state, normalisering och hjälplogik
leveransadresshämtning och import order använder samma normaliserade execute-väg som övriga FlowEngine-körningar
IntegrationController har nu bara ett enda direkt execution-entry kvar
rendering av import order-state läser från workflow-servicen i stället för controller-egna hjälpare
command line-bygget brutet ut till egen builder-service
execution-dispatch brutet ut till egen dispatcher-service
första Shopify-brytningen gjord via gemensam Shopify connection-service
Shopify read- och complete-services delar nu bootstrap för store-domain, endpoint och access token
Shopify GraphQL-transport brutet ut till gemensam client-service
Shopify read- och complete-services delar nu samma GraphQL-anrop och felhantering
Shopify querydokument centraliserade i egen query-katalog som delas mellan read och complete
Shopify-ordervalidering använder nu egen validator-service och platta valideringsmodeller
Shopify -> Jeeves-payloadmapping använder nu egen mapper-service och platta mappingmodeller
Shopify-read använder nu egen Jeeves bridge-service för lookup, exists-check och create-order i stället för att bära auth/HTTP själv
Shopify-read använder nu egen scope-probe-service för granted scopes och scope-kategorier
Shopify-read använder nu egen selection-service för date-window, querybyggande och selection-summary
Shopify-read använder nu egen result factory för validate/check/send-payloads och summary-output
Shopify-complete använder nu samma selection-service som Shopify-read för date-window, GID-normalisering och datumquery
Shopify-complete använder nu egen result factory för single/bulk payloads, counts och summary-output
Shopify-complete använder nu eget fulfillment-service-lager för create/capture/close/tag-flöden
Shopify read har fått response-shapes/projections flyttade till eget kontraktslager och använder result factory även för scopes/fetch/get-produkt-output
första Centra-brytningen gjord via gemensam Centra connection-service
Centra read/send orders/send returns/create shipments delar nu bootstrap för BaseUrl/token och HttpClient-uppsättning
Centra GraphQL-transport brutet ut till gemensam client-service och delas nu mellan read/send returns/send orders/create shipments
Centra read använder nu egen query-katalog för fetch order, fetch orders, fetch return och fetch returns
Centra send orders använder nu samma query-katalog för fetch-by-date och fetch-by-id
Centra send returns använder nu samma query-katalog för fetch-by-date och fetch-by-id
Centra create shipments använder nu samma query-katalog för shipment queries, mutationer, shipment lookup och cancel-lines-mutationer
Centra create shipments använder nu också gemensam GraphQL-klient för shipment mutationer, shipment lookup och cancellation-attempts i stället för lokal HTTP/bootstrap-logik
Centra read använder nu egen result factory för single/list-summary och payload-output för order- och returhämtningar
Centra send orders och send returns använder nu gemensam JSON-element-reader för nested property lookup, payment references och arrayfält i ParamsJson
Centra send orders använder nu egen result factory för bulk/single summary-output och payload-serialisering
Centra send returns använder nu egen result factory för bulk/single summary-output och payload-serialisering
Centra send orders och send returns har nu response-shapes och Jeeves-payloadkontrakt brutna ut till egna kontraktsfiler i stället för inbäddade privata klasser i servicarna
Centra send orders och send returns delar nu gemensam Jeeves bridge-service för config, auth, exists-check och create-order i stället för duplicerad authorized HTTP-logik
Centra send orders och send returns använder nu gemensamma helpers för adress-/delivery-name, payment metadata och return payment-info i stället för lokala hjälparmetoder per service
Centra send orders och send returns använder nu egna validatorhelpers för validation- och eligibility-regler i stället för att bära regeluppsättningarna i servicarna
Centra send orders och send returns använder nu egna Jeeves mapperhelpers för payload- och line-mappning i stället för att bära hela MapToJeeves-flöden lokalt
Centra send orders och send returns använder nu result factory-lagret även för row/copy-row-byggande, så servicarna tappar ytterligare outputansvar
Centra send orders och send returns använder nu egna fetch-helpers för fetch-by-date och fetch-by-id, så GraphQL-hämtningen ligger utanför orchestration-servicarna
Centra create shipments har nu egna interna shipment-kontrakt och mapper/helper-filer i stället för att bära modeller, preflight, warning-filter, shipment-state-mappning och failed-result helpers direkt i servicen
Centra create shipments använder nu ett separat shipment lookup-lager för fetch-by-date, fetch-by-id, fetch-by-status och existing-shipments-lookup i stället för att bära GraphQL-fetch/paginering/parsing lokalt
Centra create shipments använder nu en separat Jeeves-statusservice för single- och batch-preflight mot Jeeves i stället för lokal check/mapping-loop
Centra create shipments använder nu en separat workflow-service för single shipment och eligible-order workflow i stället för att bära hela create/cancel/capture/complete-flödet lokalt
Jeeves read/import/bridge delar nu en gemensam Jeeves API-klient för config, auth, request-URI och authorized HTTP i stället för duplicerad bootstrap i varje service
jobbstore-status syns nu explicit i FlowEngine-historiken med markering för sparad respektive temporär in-memory fallback
Akeneo send-to-shopify återanvänder nu den gemensamma Shopify connection-, scope-probe- och GraphQL-klienten i stället för eget Shopify-bootstraplager
Akeneo send-to-shopify har fått draft/diff/normaliseringslogik flyttad till en gemensam sync-helper, så serviceklassen bär orchestration och lookup i stället för hela synkregelkatalogen

Nästa steg
behålla controllern tunn och låta återstående specialfall följa samma entrypoint-mönster
fortsätta centralisera request-building där mönster upprepas
prioritera nu bara kvarvarande tydlig duplication i Jeeves- och Akeneo-spåren
undvik fler små servicefiler i Centra och Shopify om de inte tar bort ett verkligt dubblerat ansvar
ta nästa steg bara när det ger tydlig effekt på läsbarhet, testbarhet eller återanvändning

Första konkreta leverans

Den första leveransen bör vara tillräckligt liten för att minska risk, men tillräckligt tydlig för att skapa struktur.

Paket A
skapa FlowEngineOperationDefinition
skapa FlowEngineOperationCatalog
låt FlowEngineModuleService läsa operationer därifrån
bryt ut HistoryPanel och StatusPill
minska beroendet mellan storvyn och duplicerad markup
Ingår inte i första leveransen
större controlleromskrivning
systemspecifik service breakup
ändrad fallback-policy i jobbstore
Acceptanskriterier
operationerna renderas fortfarande korrekt
history fungerar fortsatt i relevanta sektioner
minst ett tydligt duplicerat UI-mönster är brutet ut
ingen funktionalitet tappar behavior i staging
Risker att bevaka
UI-regression i storvyn

Bryt ut en partial i taget och verifiera varje sektion separat.

Drift mellan katalog och handlers under övergången

Inför enkla guard checks för operationer som saknar handler eller metadata.

För generell controllerstruktur för tidigt

Centralisera gemensam logik, men behåll tydliga operation-specifika inputs tills strukturen satt sig.

För tidig generalisering mot andra moduler

Dela först när ett byggblock visat sig stabilt i FlowEngine.

Beslutspunkt efter varje etapp

Efter varje etapp ska vi utvärdera:

Har koden blivit mindre och tydligare?
Har duplicationen faktiskt minskat?
Har vi skapat något stabilt som är värt att återanvända?

Om svaret är nej på någon punkt justerar vi planen innan nästa etapp.

Praktisk kommentar om tonen i dokumentet

Det jag framför allt ändrade här jämfört med Codex-versionen är att jag hade velat att texten tydligare signalerar:

detta är en omstrukturering av FlowEngine som helhet, inte bara en refaktorering av några filer
ansvarsfördelning är huvudfrågan
operationmodellen är centrum
UI, controller och execution ska följa samma struktur
delning med andra moduler kommer senare, inte först

Det gör dokumentet bättre som styrdokument när fler ska läsa det.
