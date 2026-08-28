Bankavstämning – arbetsfil för fortsatt utveckling
Syfte

Det här dokumentet beskriver hur vi utvecklar bankavstämningen vidare utan att tappa det rena och enkla arbetsflödet som finns i dag.

Målet är att behålla samma övergripande UI-känsla:

- en tydlig modul
- ett tydligt arbetsläge åt gången
- enkel navigation tillbaka till översikten
- få val i taget för användaren
- tydliga defaults när användaren inte väljer något själv

Vi ska alltså inte bygga om allt till en komplex regelmotor i UI:t. I stället behåller vi nuvarande modulstruktur och låter sidan växla innehåll beroende på vilket läge användaren arbetar i.

Målbild

Bankavstämningen ska kunna hantera flera typer av bankhändelser och konteringsbehov:

- kundinbetalningar
- interna överföringar
- räntekonto eller räntetransaktioner
- manuell kontering
- leverantörsbetalningar
- autogiro och andra återkommande dragningar
- övriga poster som ska få defaultkontering

Samtidigt ska användaren uppleva flödet som enkelt:

- öppna modulen
- ladda eller välja underlag
- se en tydlig typning av transaktionerna
- arbeta i ett läge i taget
- spara eller gå tillbaka

Principer

1. UI ska förbli rent

Vi bygger inte fler klicklager än nödvändigt. Sidan ska fortfarande kännas som samma modul, men innehållet kan byta skepnad beroende på arbetsläge.

2. Arbetslägen ska vara tydliga

Varje läge ska ha ett tydligt syfte:

- översikt
- klassificering
- kontering
- manuell justering
- avstämning mot fakturor

3. Default ska vara enkelt

`DEF` ska vara standardläget/raden när inget mer specifikt matchar. Det gör att systemet alltid kan ge ett förutsägbart resultat.

4. Regler ska vara läsbara och prioriterade

Regler ska gå att förstå utan att behöva öppna kod eller SQL.

5. Koden ska stödja flera flöden men samma modul

Vi ska inte skapa nya moduler för varje banktyp. Det är samma bankavstämningsmodul, men med olika vyer eller steg inom samma modul.

Förslag på arbetsflöde

Flödet bör delas upp i dessa huvudsteg:

1. Import och identifiering

- användaren laddar upp CAMT-filen eller väljer en befintlig källa
- systemet läser ut transaktioner
- transaktionerna klassificeras grovt redan vid import

2. Klassificering

- transaktionerna får en typ
- systemet föreslår default-kategori när ingen regel träffar
- användaren kan ändra typ på en rad eller grupp

3. Kontering

- transaktioner kopplas till konto
- transaktioner kan också kopplas till kostnadsställe
- manuella undantag kan sparas per bolag eller bankkonto

4. Avstämning

- kundinbetalningar kan matchas mot fakturor
- leverantörsbetalningar kan hämtas från hub-ägd SQL och visas som leverantörsfakturor i samma modul
- delbetalningar ska stödjas
- flera betalningar mot samma faktura ska kunna hanteras
- en betalning ska kunna fördelas över flera fakturor om det behövs

5. Uppföljning

- användaren ska se vad som är klart, vad som behöver granskning och vad som saknar regel
- det ska vara lätt att gå tillbaka till en tidigare rad och ändra typ eller kontering

Föreslagen modell

Vi bör skilja mellan tre saker:

1. Transaktionstyp

Exempel:

- `Default`
- `CustomerPayment`
- `Interest`
- `InternalTransfer`
- `CashWithdrawal`
- `SupplierPayment`
- `Autogiro`
- `Manual`
- `Other`

2. Konteringsregel

Regeln säger hur en transaktion ska bokföras eller föreslås:

- typ
- konto
- kostnadsställe
- eventuell motpart
- eventuellt bankkonto
- prioritet
- giltighetstid

3. Avstämningsregel

Regeln säger hur en transaktion får matchas:

- mot faktura
- mot kund
- mot referens
- mot belopp
- mot delbelopp
- mot flera poster

Whiteboard-tolkning

Det vita tavlan antyder ungefär följande upplägg:

- `DEF` är en standardrad
- sedan finns specialrader per transaktionstyp
- varje rad kan bära konto och kostnadsställe
- vissa typer ska gå mot kundinbetalning, andra mot räntekonto eller överföringskonto
- vissa typer ska inte matchas mot faktura alls utan bara konteras

Det betyder att vi sannolikt behöver en matris där typ + eventuell underkod styr kontering och matchningsbeteende.

Exempel på prioriteringsordning

Regler ska träffa i följande ordning:

1. Bolagsspecifik manuell regel
2. Bankkonto-specifik regel
3. Motpartsspecifik regel
4. Typregel
5. Default `DEF`

Det gör att vi kan hålla modellen enkel och ändå få bra precision.

Hur vi behåller UI:t rent

Vi ska inte låta användaren mötas av alla val samtidigt.

I stället bör sidan växla mellan tydliga vyer eller paneler:

- översikt av uppladdad fil och status
- klassificeringsvy
- konteringsvy
- avstämningsvy
- detaljvy för en enskild transaktion

Det betyder att samma modul kan ha flera vyer, men användaren ska alltid känna att det är samma arbetsyta.

Praktiskt innebär det:

- samma modul i menyn
- samma återgång till huvudvyn
- samma bolags- och underlagsbundna state
- samma dataunderlag
- olika innehåll beroende på aktivt läge

Den nuvarande implementationen följer redan den riktningen:

- arbetsflödet visas som fyra steg: Underlag, Granska, Matcha och Klart
- Underlag visar fil, saldon, transaktioner och fakturor utan att blanda in matchningspanelen
- Granska samlar typöversikt och konteringsmatris i samma steg
- Matcha fokuserar på säker auto-match och den manuella granskningskön
- Klart validerar resultatet på servern och låser avstämningen; återöppning kräver en orsak
- live-läget visar samma statuskort för matchat, granskning och omatchat som demo-läget
- osparade konteringsändringar markeras och skyddas när användaren lämnar granskningssteget
- uppladdade live-underlag visar att bankkontot ännu inte kan verifieras automatiskt mot valt bolag
- klassificeringsläget visar klassificeringskort för hela underlaget och filtrerar sedan transaktionslistan till vald typ
- klassificeringsläget visar nu också föreslaget konto och kostnadsställe per typ
- konteringsläget kan spara en regelmatris per bolag och bankkonto med versionshantering
- regelordningen för kontering är nu tydlig: bankkonto-specifik typregel, bankkonto-DEF, bolags-specifik typregel och bolagets DEF
- vald transaktion visar samma effektiva konteringsförslag som kodningsvyn
- rekommendationer, auto-match och AI använder samma kodningsstyrda gate för fakturabetalningar
- `.nda`-filer accepteras i importen när innehållet är camt.053 XML
- fakturakortet visar 20 poster per sida och använder paginering i UI:t
- banktransaktionskortet visar också 20 poster per sida och använder paginering i UI:t, men summeringar, klassificeringskort och manuell granskningskö bygger på hela underlaget för valt filter
- kundinbetalningar hämtas via befintlig invoices-tjänst med page/pageSize
- leverantörsbetalningar hämtas via hub-ägd SQL och sidindelas innan de skickas till UI
- Alla typer använder en begränsad, stabil sammanslagning av de två paginerade datakällorna
- matchningar, importhistorik, audit och konteringsregler sparas i CustomerPortal SQL
- äldre JSON-state migreras idempotent vid uppstart och lämnas kvar som säkerhetskopia
- demo-läget följer samma klassificeringsfilter för fakturalistan som live-läget

Teknisk riktning

1. Behåll nuvarande controller och service-struktur

Vi ska vidareutveckla det som redan finns i bankreconciliation-lagret i stället för att börja om.

2. Behåll ett tydligt flödestillstånd

Sidan behöver veta om användaren arbetar med underlag, granskning, matchning eller resultatuppföljning. Klassificering och kontering hör ihop i granskningssteget och ska inte presenteras som två konkurrerande huvudflöden.

3. Lägg till en regelmodell

Regler ska kunna lagras och utvärderas konsekvent.
Vi är nu förbi enbart förslag och har en första sparbar matris för kontering, men den ska fortfarande hållas enkel och tydlig.
Den effektiva ordningen vi kör på är: aktuell bankkonto-rad, aktuell bankkonto-DEF, bolagets rad och bolagets DEF.

4. Behåll enkel vylogik

Views ska få färdig data från controllern eller en service/facade, inte bygga affärslogik själva.

5. Testa varje steg

Varje nytt läge ska ha tester för:

- korrekt klassificering
- korrekt default
- rätt prioritet
- rätt matchningsbeteende
- rätt fallback när ingen regel träffar

Fasindelning

Fas 1 – Begrepp och regler

- definiera transaktionstyper
- definiera default `DEF`
- definiera prioriteringsordning
- definiera vilka CAMT-koder som ska mappa till vilka typer

Fas 2 – Datamodell

- spara konteringsregler
- spara eventuella bankkonto-specifika regler
- spara kostnadsställe och konto

Fas 3 – Klassificeringsmotor

- bygg en enkel regelmotor som föreslår typ och kontering
- se till att fallback alltid blir `DEF`

Fas 4 – UI-lägen

- behåll modulens översikt
- lägg till vyväxling per läge
- håll navigeringen enkel

Fas 5 – Avancerad avstämning

- kundinbetalningar mot faktura
- delbetalningar
- flera fakturor på samma betalning
- manuella konton och kostnadsställen

Fas 6 – Förfining

- bättre heuristik
- fler regler
- bättre filtrering
- bättre rapportering

Definition of Done

Vi är klara med ett steg när:

- användaren kan förstå vad modulen gör utan utbildning
- default alltid fungerar
- kontering går att följa i ett enkelt flöde
- klassificering och avstämning inte blandas ihop i UI:t
- vi har tester för nya regler och nya arbetslägen
- vi inte har behövt göra flödet mer rörigt för att få fler funktioner

Öppen fråga

Den viktigaste kvarvarande designfrågan är om vi ska låta:

- en transaktion först klassificeras och sedan konteras

eller

- samma vy samtidigt visa klassificering, kontering och avstämning

Min rekommendation är att separera dem i olika lägen men behålla samma modul och samma dataflöde.
