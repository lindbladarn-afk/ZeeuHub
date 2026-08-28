# FlowEngine Native Migration

## Target architecture

FlowEngine ska inte langre hostas som en separat Swift/Vapor-applikation med egen frontend. Malbilden i `ZeeU.CustomerPortal` ar:

- en portal-native UI i Razor som foljer samma navigation, auth och tenantmodell som ovriga integrationsmoduler
- en C#-back-end som ager jobbko, historik, typed commands och integrationsadapters
- inga beroenden till extern FlowEngine-URL eller iframe-inbaddning

## Mapping fran originalrepo

Original FlowEngine delas idag upp i Swift-lager. Portalens motsvarighet blir:

- `FlowEngineWebAPI`
  - flyttas till portalens controllers, application services och framtida background jobs
- `FlowEngineServiceModels` och typed DTOs
  - mappas till C#-kontrakt under `WebApp/Models/Integration`
- `FlowEngineInfrastructure`
  - byggs om som portalens integrationsservices och repositories mot Jeeves, Centra, Shopify och Akeneo
- Svelte-webben
  - ersatts med Razor-vyer och portalens befintliga modulskal

## Fasad migreringsplan

## Status och avprickning

### Klart i portalen

- [x] Jobbsubstrat med historik, status och typed execution contracts
- [x] Persistent jobbhistorik via `Identity.FlowEngineJobs`
- [x] `flowengine config validate`
- [x] Jeeves `get-customer-addresses`
- [x] Jeeves `get-orders`
- [x] Jeeves `order-exists`
- [x] Jeeves `get-product`
- [x] Jeeves `get-art-status`
- [x] Jeeves `import-order`
- [x] PDF-upload och parser-review for import-order
- [x] Centra `check-orders` for en dag
- [x] Centra `fetch-order`
- [x] Centra `fetch-orders`
- [x] Centra `fetch-return`
- [x] Centra `fetch-returns`
- [x] Centra `send-order` single
- [x] Centra `send-orders` for en dag
- [x] Centra `send-return` single
- [x] Centra `send-returns` for en dag
- [x] Centra `create-shipment` single
- [x] Centra `create-shipments` for en dag
- [x] Centra `create-shipments-pending`
- [x] Shopify `complete-order`
- [x] Shopify `complete-orders`
- [x] Shopify `complete-orders-pending`
- [x] Shopify `scopes-check`
- [x] Shopify `get-products`
- [x] Shopify `fetch-order`
- [x] Shopify `fetch-orders`
- [x] Shopify `validate-order`
- [x] Shopify `validate-orders`
- [x] Shopify `check-orders`
- [x] Shopify `send-order`
- [x] Shopify `send-orders`
- [x] Akeneo `--get-products`
- [x] Akeneo `--get-all-products`
- [x] Akeneo `send-to-shopify` dry run

### Delvis klart

- [ ] Centra range-korningar `--since/--until --force`
  - datumstyrda singeldagsfloden finns, men inte full fler-dagars parity
- [ ] Riktig background queue, schedule, cancel och adminfloden
  - jobb kor och sparas i historik, men ar inte ännu full separat batchmotor
- [ ] Paritetstester och golden verification mot originalrepo
- [ ] Skarp end-to-end-verifiering mot riktiga stagingmiljoer

### Inte portat an

- [ ] `Raw`/diagnostiska passthrough-kommandon i portalmodulen

### Fas 1: Jobbsubstrat och typed commands

- etablera portalens kontrakt for jobbstatus, command payloads och execution flags
- lagga grund for queue, cancel, historik och output-hantering
- gora det mojligt att porta varje operation utan att forst skriva om hela UI:t

### Config validate

Portal-worktreen har nu ocksa en native `config validate`:

- validerar aktivt bolags integration-config for Jeeves, Centra och Shopify
- validerar AkeneoOptions for de Akeneo-floden som redan ar portade
- returnerar `valid` eller `invalid` plus full issue-lista i jobbhistoriken
- gor det enklare att se alla saknade falt pa en gang innan stagingtester startar

### Fas 2: Jeeves lasfloden

- flytta `get customer addresses`, `get product`, `get art status` och narliggande lookup-floden
- anvanda portalens befintliga auth-, tenant- och Jeeves-context-tjanster
- verifiera att typed contracts och felhantering fungerar pa lasvagar med lag risk

Portal-worktreen har nu aven native parity for de narliggande Jeeves-orderuppslagen:

- `get-orders` via exakt ett lookupfalt: `c_extordernr` eller `c_ordernr`
- `order-exists` som snabb found/missing-kontroll mot samma Jeeves-orderendpoint
- samma authstack, jobbmodell och pretty-printed JSON-output som ovriga Jeeves-lasningar

### Fas 3: Jeeves import order

- bygga native importform, validering och torrkorning
- porta samma decimal-/EAN-normalisering och externa ordernummerstrategi som originalet
- skicka till Jeeves `/ordersedi` via portalens egen authstack, med exists-check fore skrivning
- spara historik och status pa samma satt som framtida batchjobb
- hamta och valja leveransadresser i samma portalform, med tenant-/kundkontext som valideringssteg
- lagga till dokumentupload, deterministisk parser-review och manuell apply till importtabellen

### Dokumentextraktion

Originalets PDF-extraktion bygger pa deterministiska parserregler ovanpa:

- PDFKit nar det finns tillgangligt
- annars `pdftotext` som fallback i runtime-miljon

Portal-worktreen har nu en forsta native dokumentextraktion som:

- accepterar endast PDF
- anvander en ren .NET-parser via `PdfPig`
- parser flera kanda layoutfamiljer deterministiskt:
  - `Artnr / Ant/bas`
  - `Ert Art. Nr / Antal (pcs)`
  - `Lev. art.nr / Antal`
  - `Benamning / Lev. Art. Nr.`
  - `PO Quantity / Pcs/PO UoM`
  - `Quantity UoM / Manufacturing Part Number`
  - `Dessin / Quantity`
- failar stangt om ingen kand parser matchar
- visar review i portalens import-order-modul innan rader laggs in i importtabellen

Parseruppsattningen motsvarar nu de dokumentregler som finns i originalrepo. Kvar ar att verifiera fler verkliga leverantors-PDF:er mot den nya .NET-tolkningen och justera radrekonstruktionen om PdfPig renderar vissa layouter annorlunda an Swift/PDFKit gjorde.

### Fas 4: Centra, Shopify och Akeneo skrivfloden

- porta batchjobb, single-order actions, shipmentfloden och policy/mappning
- ateranvanda jobbsubstratet fran fas 1 sa att skrivvagarna far samma audit trail
- avsluta med de mest komplexa och mest riskfyllda skrivoperationerna

### Akeneo export

Portal-worktreen har nu en forsta native Akeneo-slice i FlowEngine-modulen:

- exporterar valda SKU:er via portalens befintliga Akeneo-klient och XML-exporttjanst
- ateranvander samma Akeneo-auth och XML-format som den separata portalexporten
- sparar antal exporterade produkter, saknade SKU:er och XML-resultatet i jobbhistoriken

### Akeneo all products

Portal-worktreen har nu ocksa en native `all products`-korning:

- kor full Akeneo-export via samma portal-native klient
- respekterar limit i FlowEngine-formularet
- sparar XML och summering i samma jobbmodell som ovriga FlowEngine-operationer

### Akeneo send to Shopify

Portal-worktreen har nu ocksa en forsta native parity-slice for `send-to-shopify`:

- kor som dry run precis som originalkommandot i det har steget
- hamtar Akeneo-produkter direkt i portalen, utan mellanliggande Swift-tjanst eller extern FlowEngine-webb
- bygger onskat Shopify-utkast, slar upp nuvarande Shopify-produkt via SKU och raknar fram diff/warnings i jobbhistoriken
- verifierar `read_products` innan korningen startar

### Centra check orders

Portal-worktreen har nu en forsta native Centra-slice for `check orders`:

- kor mot portalens befintliga Centra- och Jeeves-klienter
- anvander `c_extordernr == Centra order.id`
- klassificerar `found`, `missing`, `deleted` och `error`
- sparar resultatet i samma FlowEngine-jobbhistorik som Jeeves-operationerna

Paritet som fortfarande ar kvar for den operationen ar framfor allt utokad range-korning, output-filer pa disk och full regression mot originalets dag-filer.

### Centra fetch order och returns

Portal-worktreen har nu ocksa native lasparitet for Centra fetch-kommandona:

- `fetch-order` hamtar en specifik order som raw GraphQL payload
- `fetch-return` hamtar en specifik retur som raw GraphQL payload
- `fetch-orders` och `fetch-returns` kor dag-for-dag over ett valt UTC-intervall
- rangekorningar har samma 7-dagarsguard som ovriga ranged operationer och kan overskridas med `force`
- resultat sparas i samma jobbhistorik som resten av portalens native FlowEngine

Skillnaden mot original-CLI:t ar att portalen inte skriver dagfiler till disk; i stallet lagras raw payload och per-dag-fel i jobbhistoriken.

### Centra send orders

Portal-worktreen har nu en forsta native Centra-slice for `send orders`:

- hamtar fulla Centra-ordrar for en UTC-dag med paginering
- validerar och eligibility-bedomer med samma huvudregler som originalet
- mappar store-specifikt till Jeeves-orderpayload for store `1`, `2` och `4`
- kor som `dry run` eller skarp sendning mot Jeeves `/ordersedi`
- anvander `c_extordernr == order.id` for exists-check och markerar Jeeves-duplicat som `skipped_existing`
- sparar batchresultatet i samma FlowEngine-jobbhistorik som ovriga native operationer

Paritet som fortfarande ar kvar for den operationen ar framfor allt range-korning over flera dagar, output-filer pa disk och originalets deadlock-retrystrategi for skarpa batchskrivningar.

Portal-worktreen har nu ocksa single `send-order`:

- hamtar en specifik order via `order-id`
- kor samma validering, eligibility och mapping som batchflodet
- stoder `dry run` och `skip Jeeves check`
- sparar resultatet i samma jobbmodell som batchkorningen

### Centra send returns

Portal-worktreen har nu en forsta native Centra-slice for `send returns`:

- hamtar fulla Centra-returer for en UTC-dag med paginering
- validerar returer med samma huvudregler som originalet, inklusive `COMPLETED`-status och krav pa retur- eller avgiftsrader
- mappar returer till Jeeves-orderpayload med negativa artikelrader och returens extra avgiftsrader
- anvander ext order number `C{returnId}` och markerar duplicat som `already_exists`
- kor som `dry run` eller skarp sendning mot Jeeves `/ordersedi`
- sparar resultatet i samma FlowEngine-jobbhistorik som ovriga native operationer

Paritet som fortfarande ar kvar for den operationen ar framfor allt fler-dagars range-korning, diskfiler som i original-CLI:t och bredare regression mot riktiga returfall fran Centra.

Portal-worktreen har nu ocksa single `send-return`:

- hamtar en specifik retur via `return-id`
- kor samma validering, duplicate-check och mapping som returbatchen
- stoder `dry run` och skarp sandning till Jeeves
- sparar resultatet i samma jobbmodell som batchkorningen

### Centra create shipments

Portal-worktreen har nu en forsta native shipment-slice for `create shipments`:

- hamtar Centra-ordrar for en UTC-dag med paginering
- gor samma batch-preflight som originalet for `CONFIRMED`/`PROCESSING` samt skip av redan fullt processade `SHIPPED`
- kor Jeeves preflight med kontroll av `c_ordstat >= 50`
- planerar shipment-rader och eventuella cancellations utifran allocationer och shipped quantity
- kor Centra shipment-mutationer samt store-specifika workflowsteg for store `1`, `2` och `4`
- sparar resultatet i samma FlowEngine-jobbhistorik som ovriga native operationer

### Centra create shipments pending

Portal-worktreen har nu ocksa en forsta native shipment-slice for `create shipments pending`:

- hamtar Centra-ordrar med status `CONFIRMED` och `PROCESSING` utan datumfilter
- kor samma batch-preflight, Jeeves gate och allocation-baserad shipment planning som datumstyrda `create shipments`
- ateranvander samma Centra-mutationer, cancellation-strategier och store-specifika workflowsteg for store `1`, `2` och `4`
- sparar resultatet i samma FlowEngine-jobbhistorik som ovriga native operationer

### Centra create shipment

Portal-worktreen har nu ocksa en native slice for single `create shipment`:

- hamtar en specifik Centra-order via `order id`
- gor samma skip-regler for redan fullt processade shipments
- kor Jeeves gate med kontroll av `c_ordstat >= 50`
- ateranvander samma allocation-baserade planning och store-specifika Centra workflow som batchflodena
- sparar resultatet i samma FlowEngine-jobbhistorik som ovriga native operationer

Paritet som fortfarande ar kvar for shipment-sparet ar framfor allt bred regression mot riktiga orderfall och senare Shopify-kompletteringarna ovanpa shipping-resultaten.

### Shopify complete orders pending

Portal-worktreen har nu en forsta native Shopify-slice for `complete orders pending`:

- letar upp Shopify-order med taggen `SentToJeeves` men utan taggen `Shipped`
- verifierar att ordern fortfarande ar `UNFULFILLED` i Shopify
- verifierar Jeeves-status innan completion med krav pa `c_ordstat >= 50`
- hamtar fulfillment orders och skapar Shopify fulfillment direkt fran portalen
- kan valfritt stanga ordern efter fulfillment

### Shopify complete orders

Portal-worktreen har nu ocksa en datumstyrd native Shopify-slice for `complete orders`:

- kor mot ett specifikt UTC-datum, `latest-day` eller ett `since/until`-intervall
- hamtar Shopify-order via `created_at`-query for dagen
- kor samma Jeeves gate och fulfillment-orderfiltrering innan completion
- loopar dag-for-dag i intervallet och blockerar range storre an 7 dagar om inte `force range` ar valt
- kan koras som dry run eller skarp fulfillment med valfri `close order`

### Shopify complete order

Portal-worktreen har nu ocksa en enskild native Shopify-slice for `complete order`:

- accepterar `gid://shopify/Order/<id>` eller ett numeriskt order-id i portalens formularyta
- hamtar exakt en order fran Shopify och kor samma completion-gate som batchoperationerna
- kan koras som dry run eller skarp fulfillment med valfri `close order`

### Shopify scopes, read, validate och check

Portal-worktreen har nu ocksa de ursprungliga Shopify-las- och kontrollflodena:

- `scopes-check` visar granted scopes, butik och kategoristatus for products, fetch, validate, send, check och complete
- `get-products` kor fri query, `updated-since` och limit mot Shopify GraphQL
- `fetch-order` och `fetch-orders` hamtar single- respektive datumstyrda/ranged orderpayloads
- `validate-order` och `validate-orders` kor samma eligibilityregler som originalet med typed decisions per order
- `check-orders` jamfor Shopify-order mot Jeeves via ext ordernummer och markerar `found`, `missing`, `failed_validation` och `error`

### Shopify send orders

Portal-worktreen har nu ocksa en native Shopify-slice for `send-order` och `send-orders`:

- kor single-order eller dag/range/latest-day batch direkt i portalen
- ateranvander samma validation- och scope-gate som originalet innan mappning
- mappar Shopify-order till Jeeves-orderpayload med fast `c_foretagkod=5`, `c_OrdTyp=8`, svensk `ftgnr`-mapping och shippingrad
- gor optional Jeeves exists-check via `c_extordernr`, markerar duplicat som `skipped_existing` och skickar annars till `/ordersedi`
- satter `SentToJeeves`-tagg efter `sent` eller `skipped_existing`, utan att en taggmiss far hela jobbet att faila

Paritet som fortfarande ar kvar for Shopify-sparet ar framfor allt bred regression mot riktiga Shopify/Jeeves-miljoer och eventuell hardning av fler lands-/kundmappningar om butiken har fler `ftgnr`-regler an originalets nuvarande svenska mapping.

## Forsta vertikalen

Forsta vertikalen ar jobbsubstrat plus Jeeves-lasfloden. Det valet minimerar risk eftersom samma jobbmodell sedan ateranvands for `import order`, `send orders`, `send returns` och andra batchkoringar. Samtidigt ger det tidig verifiering av att portalens auth, tenant-context och integrationsservices fungerar for FlowEngine-scenarion.

## Verifiering

Kors i portalrepo:

```bash
dotnet restore WebApp/WebApp.csproj --disable-parallel
dotnet build WebApp/WebApp.csproj -p:UseSharedCompilation=false -nr:false -m:1 -v minimal
```

Det som ska verifieras:

- `FlowEngine`-modulen laddar utan externa URL-beroenden
- portalens `IntegrationController` returnerar den nya native vyn
- jobb- och operationskontrakten bygger utan att andra integrationsmoduler paverkas
- import-order kan koras som dry run eller skarp Jeeves-sendning i samma portalmodul
- PDF-upload visar review och lagger sedan till extraherade rader i importtabellen
- SQL-skriptet `SQL/AzureDb/Tables/Identity.FlowEngineJobs.sql` ar kort mot malmiljons databas innan DB-baserad jobbhistorik forvantas vara persistent
