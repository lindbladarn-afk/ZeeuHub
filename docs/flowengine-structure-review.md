FlowEngine – strukturgranskning och förenklingsriktning

Syfte

Det här dokumentet är en sanity review av den nuvarande FlowEngine-strukturen efter refaktoreringen.

Målet är inte att driva upp fler filer eller fler lager för sin egen skull.
Målet är att:

- minska verklig kodduplication
- göra ansvar tydligare
- hålla navigationen rimlig
- undvika att varje liten detalj blir en egen abstraktion
- skapa en struktur som är enkel att förstå för nästa utvecklare

Det här dokumentet kompletterar [flowengine-refactor-plan.md](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/docs/flowengine-refactor-plan.md).
Planfilen beskriver vad som brutits ut och i vilken ordning.
Det här dokumentet beskriver vad vi nu tycker är "lagom" struktur.

Bedömning i korthet

FlowEngine var redan avancerat innan vi började refaktorera.
Komplexiteten fanns då i några få mycket stora filer.

Refaktoreringen har gjort två bra saker:

- stor lokal komplexitet har minskat i flera nyckelklasser
- systemgränserna mellan UI, controller, execution och integration är tydligare

Men vi är också nära gränsen där ytterligare uppdelning kan bli kontraproduktiv.

Det betyder:

- vissa uppdelningar ska vi definitivt behålla
- vissa ska vi konsolidera igen
- framåt ska vi hellre slå ihop små hjälpfiler än fortsätta skapa nya

Grundprincip framåt

Vi ska sikta på "några tydliga lager", inte "en fil per tanke".

Bra uppdelning:

- connection/auth
- transport/client
- query catalog
- mapper
- result factory
- orchestration/workflow

Dålig uppdelning:

- en egen fil för varje liten helper om den bara används ett ställe
- interna små records/helpers som gör koden svårare att följa
- för många services i en kedja där varje service bara skickar vidare data

Övergripande rekommendation

Framåt ska vi använda den här tumregeln:

1. Bryt bara ut kod om den uppfyller minst ett av följande:
   - används av fler än en service
   - är tekniskt separat ansvar, till exempel auth eller GraphQL transport
   - är så stor att huvudklassen blir märkbart lättare att förstå utan den
   - behöver kunna testas eller bytas isolerat

2. Behåll hellre en något större service om alternativet är tre nästan tomma hjälpfiler.

3. Små helpers som bara används internt i en vertikal ska helst ligga:
   - som intern static helper i samma filområde
   - eller i en enda samlingsfil för den vertikalen

4. Nya abstraktioner måste betala sin kostnad i läsbarhet.

Vad vi ska behålla som egna lager

De här delarna är motiverade och bör behållas.

1. Gemensam operationkatalog

Behåll:

- [FlowEngineOperationCatalog.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineOperationCatalog.cs)
- [IFlowEngineOperationCatalog.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/IFlowEngineOperationCatalog.cs)

Varför:

- en sanningskälla för operationmetadata är rätt
- minskar drift mellan UI och execution

2. Request normalisering och execution-dispatch

Behåll:

- [FlowEngineRequestNormalizer.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineRequestNormalizer.cs)
- [FlowEngineOperationDispatcher.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineOperationDispatcher.cs)
- [FlowEngineExecutionService.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineExecutionService.cs)

Varför:

- här finns ett tydligt lageransvar
- det är bra att execution inte läser `HttpContext`

3. Connection/auth/client-lager per system

Behåll för Shopify och Centra:

- `ConnectionService`
- `GraphQlClient`
- Jeeves bridge där den verkligen delas

Varför:

- tydlig teknisk separation
- återanvänds mellan flera operationer

4. Result factories där output faktiskt är stor

Behåll:

- större result factories för read/complete/send där summary/json-output är mycket

Varför:

- de minskar brus i orchestrationsklasser
- de har tydlig nytta

5. UI-partials för verkligt återkommande mönster

Behåll:

- status pill
- history panel
- history detail
- scheduler popover

Varför:

- tydlig duplication i vyn
- bra UI-återanvändning

6. Jobbstore-status i modellen

Behåll:

- explicit storage-status på `FlowEngineJobSnapshot`
- kompakt degraded-varning i historikpanelen

Varför:

- DB-fallback fanns redan, men var tidigare osynlig i UI:t
- det här är en liten modellutökning med tydlig driftnytta
- det ger stöd för felsökning utan att skapa ett nytt stort lager

Vad vi ska ifrågasätta och sannolikt slå ihop igen

Det här är de tydligaste kandidaterna för konsolidering.

1. Centra småhelpers

Kandidater:

- [FlowEngineCentraCommonHelper.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineCentraCommonHelper.cs)
- [FlowEngineCentraOrderMetadataHelper.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineCentraOrderMetadataHelper.cs)
- [FlowEngineCentraReturnPaymentInfoHelper.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineCentraReturnPaymentInfoHelper.cs)

Rekommendation:

- behåll detta högst som 1-2 samlingsfiler
- exempel:
  - `FlowEngineCentraCommonHelper.cs`
  - `FlowEngineCentraMappingHelper.cs`

Varför:

- de är små
- de används inom samma vertikal
- de skapar onödigt många hopp i navigationen

2. Centra fetch-helpers för send orders/send returns

Kandidater:

- fetch by date / fetch by id i:
  - [FlowEngineCentraSendOrdersService.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineCentraSendOrdersService.cs)
  - [FlowEngineCentraSendReturnsService.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineCentraSendReturnsService.cs)

Rekommendation:

- behåll dem inne i respektive service
- skapa inte en separat fetch-fil igen om inte logiken börjar delas mellan flera operationer

Varför:

- de används bara av en service vardera
- de är begripliga även när de ligger lokalt
- det minskar navigation och filspridning

3. Shipment-status/workflow-splitten i Centra

Kandidater:

- [FlowEngineCentraShipmentJeevesStatusService.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineCentraShipmentJeevesStatusService.cs)
- [FlowEngineCentraShipmentWorkflowService.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineCentraShipmentWorkflowService.cs)

Rekommendation:

- behåll dem just nu under Centra-spåret
- men skapa inte fler små shipment-services
- om ytterligare split behövs: slå hellre ihop status + workflow till en gemensam shipment-orchestrationfil

Varför:

- `create shipments` var för stor
- den här splitten gav nytta
- men det är ungefär här vi ska stanna

4. Små model-filer med mycket få rader

Kandidater:

- [FlowEngineCentraReadModels.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Models/Integration/FlowEngineCentraReadModels.cs)
- [FlowEngineShopifyFulfillmentModels.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Models/Integration/FlowEngineShopifyFulfillmentModels.cs)
- [FlowEngineShopifyReadModels.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Models/Integration/FlowEngineShopifyReadModels.cs)

Rekommendation:

- slå ihop små model-filer in i närliggande servicefil eller större kontraktsfil
- behåll bara separata model-filer när de bär verkligt flera typer eller delas på riktigt
- samma princip gäller för Akeneo/Jeeves: bryt hellre ut en enda gemensam stödklass för faktisk återanvändning än flera små en-fil-per-metod helpers

Varför:

- 5-15-raders model-filer är sällan värda navigationen de kostar

Vad som fortfarande är för stort på riktigt

Det här är verkliga återstående problemområden, där uppdelning fortfarande är värd jobbet.

1. [FlowEngineShopifyReadService.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineShopifyReadService.cs)

Nuvarande storlek:

- cirka 1085 rader

Bedömning:

- fortfarande för stor
- här finns mer värde att ta ut

Rekommenderad riktning:

- dela bara där ansvar verkligen separeras
- fokusera på:
  - selection/query assembly
  - projection/result shaping
  - orchestration

Undvik:

- fler mikrofiler för små helpers

5. Jeeves och Akeneo efter saneringsrundan

Nuvarande riktning:

- Jeeves read/import/bridge delar en gemensam API-klient för config, auth, request-URI och authorized HTTP
- Akeneo återanvänder redan gemensam Shopify connection, scope-probe och GraphQL-transport
- Akeneo har en gemensam sync-helper för draft/diff/normalisering i stället för att bära hela regelmassan i serviceklassen

Rekommendation:

- behåll den här nivån
- gör bara fler ändringar när de tar bort verklig duplication mellan spår
- stoppa innan Akeneo eller Jeeves börjar få samma övermodularisering som Centra/Shopify var på väg mot

Varför:

- de största strukturella problemen i de två spåren är redan adresserade
- nyttan av ytterligare uppdelning avtar snabbt

2. [FlowEngineShopifyCompleteOrdersService.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineShopifyCompleteOrdersService.cs)

Nuvarande storlek:

- cirka 866 rader

Bedömning:

- fortfarande stor nog att motivera fortsatt förenkling

Rekommendation:

- behåll tydlig separation mellan:
  - fulfillment operations
  - result shaping
  - selection/query
- men skapa inte fler små "pass-through"-lager

3. [FlowEngineCommandLineBuilder.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineCommandLineBuilder.cs)

Nuvarande storlek:

- cirka 771 rader

Bedömning:

- stor nog att granskas
- troligen nästa generiska ställe där vi kan minska duplication utan att öka filantalet mycket

Rekommendation:

- undersök om den ska delas i:
  - command templates
  - argument normalization
  - environment setup

Men:

- högst 2-3 lager
- inte fler

4. [FlowEngineJeevesImportOrderService.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/FlowEngine/FlowEngineJeevesImportOrderService.cs)

Nuvarande storlek:

- cirka 586 rader

Bedömning:

- troligen fortfarande för stor
- särskilt om dokument/parsing/merge/logik fortfarande blandas

Rekommendation:

- använd samma princip som för Shopify/Centra
- men bara om det finns verkliga ansvar att dela

Konkreta förenklingsregler vi ska följa nu

Från och med nu ska vi använda dessa regler i FlowEngine.

1. Inga nya filer för helpers under 60 rader om de bara används på ett ställe.

2. Små interna helpers ska i första hand:
   - ligga i samma fil
   - eller i en gemensam `*CommonHelper.cs`

3. En ny servicefil måste kunna motiveras med ett tydligt ansvar, inte bara kodstorlek.

4. Om en service bara:
   - tar emot 2-3 beroenden
   - gör ett enda anrop
   - returnerar nästan samma data
   då ska den sannolikt inte vara egen service.

5. Vi ska föredra:
   - färre, tydliga filer med bra namn
   framför:
   - många små filer som kräver konstant navigation

Föreslagen målstruktur per integrationsspår

Det här är ungefär den nivå vi ska sikta på.

Per systemspår:

- `ConnectionService`
- `GraphQlClient` eller motsvarande transport
- `QueryCatalog`
- `ReadService`
- `Send/WriteService`
- `ResultFactory`
- `Mapper`
- `CommonHelper` eller `Contracts`

Eventuellt ytterligare:

- `WorkflowService` om en operation verkligen är stor, som shipment/create/fulfillment

Men inte mycket mer än så.

Vad vi bör göra härnäst

1. Pausa vidare uppdelning i nya småfiler.

2. Gör en konsolideringsrunda i Centra:
   - slå ihop småhelper-filer
   - slå ihop små fetch-helpers till gemensamma helperfiler
   - behåll bara de tydliga lager vi redan vet ger nytta

3. Gör sedan samma bedömning i Shopify innan fler filer bryts ut.

4. Ta därefter en riktad genomgång av:
   - `FlowEngineCommandLineBuilder`
   - `FlowEngineShopifyReadService`
   - `FlowEngineShopifyCompleteOrdersService`

Vad vi inte ska göra nu

- inte fortsätta dela upp Centra i allt mindre bitar
- inte skapa fler services för små specialfall
- inte flytta ännu fler små modeller till egna filer
- inte försöka abstrahera bort skillnader mellan Shopify, Centra och Jeeves för tidigt

Slutsats

Refaktoreringen har varit nyttig, men nästa bästa steg är inte mer uppdelning.

Nästa bästa steg är kontrollerad förenkling:

- behåll de stora, tydliga lagren
- slå ihop överdrivna småbitar
- fokusera på verkliga problemfiler

Det vi ska optimera för nu är:

- läsbarhet
- rimlig navigation
- tydliga ansvar
- mindre duplication

Inte maximal modularisering.
