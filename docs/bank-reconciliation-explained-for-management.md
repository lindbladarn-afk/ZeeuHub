Bankavstämning - förklarad för ledning
=====================================

Syfte
-----

Det här dokumentet är skrivet för att förklara hur bankavstämningen fungerar i dag, vad vi bygger vidare mot och varför vi har valt den struktur vi har.

Målet är att hålla flödet enkelt för användaren, men ändå kunna hantera flera typer av bankhändelser på ett kontrollerat sätt.

Grundidé
--------

Bankavstämningen är inte bara "matcha bank mot faktura".
Den måste kunna hantera tre olika saker:

1. Klassificera bankhändelsen
2. Föreslå eller sätta kontering
3. Matcha mot faktura när det faktiskt är en kundinbetalning

Det är därför vi har delat upp lösningen i olika lägen i samma modul.

Vad användaren ser
------------------

Användaren möter fortfarande en enda modul i menyn.

I modulen kan innehållet växla mellan:

- översikt
- klassificering
- kontering
- avstämning
- detaljvy

Det viktiga är att användaren alltid är kvar i samma arbetsyta.
Vi bygger alltså inte flera olika system, utan ett arbetsflöde med olika steg.

Hur det fungerar i praktiken
----------------------------

1. Import

Användaren laddar upp en CAMT-fil från banken.

Vi accepterar även `.nda` eftersom vissa kunder levererar CAMT-innehåll med det filnamnet, även om innehållet är XML.

2. Tolkning

Systemet läser ut transaktionerna och identifierar signaler som:

- domän/familj i CAMT
- referenser
- betalningsriktning
- avsändare/mottagare
- text i meddelandet
- vilket bankkonto filen hör till

3. Klassificering

Varje transaktion får en typ, till exempel:

- bankinbetalning
- räntekonto
- intern överföring
- leverantörsbetalning
- bankavgift
- skattebetalning
- autogiro
- kontantuttag
- `DEF` som standard

4. Kontering

För varje typ kan vi föreslå:

- konto
- kostnadsställe

Vi kan också spara dessa regler per bolag och bankkonto.

5. Avstämning

Kundinbetalningar och leverantörsbetalningar kan visas i fakturalistan, men de hämtas från olika källor och används i olika delar av flödet.

Kundinbetalningar går in i fakturamatchningen.

Leverantörsbetalningar visas som leverantörsfakturor i bankavstämningen, men de matchas inte som kundinbetalningar.

Det innebär att systemet inte ska försöka matcha exempelvis:

- interna överföringar
- bankavgifter
- ränta
- skatt

De ska istället konteras, inte fakturamatchas.

Regellogiken
------------

Vi har byggt en tydlig prioritet så att systemet inte gissar olika saker i olika delar av UI:t.

Den effektiva ordningen är:

1. bankkonto-specifik typregel
2. bankkonto-specifik `DEF`
3. bolagets typregel
4. bolagets `DEF`

Det betyder:

- ett bankkonto kan ha egna undantag
- om ett bankkonto saknar egen rad faller det tillbaka på bolagets standard
- `DEF` är alltid sista fallback

Varför det här är bra
---------------------

Det här upplägget ger oss tre saker samtidigt:

1. Enkelt gränssnitt

Användaren ser bara ett tydligt arbetsflöde.

2. Stark kontroll

Vi kan styra hur olika transaktioner ska behandlas utan att bygga om hela sidan.

3. Bra förvaltning

Regler går att ändra per bolag och bankkonto utan att röra kärnlogiken.

Vad som redan är på plats
-------------------------

Vi har redan:

- egen bankavstämningsmodul i menyn
- egen controller
- CAMT-import
- stöd för `.nda`
- klassificering av transaktioner, där typkorten och den manuella granskningskön visar hela underlaget men listan filtreras på vald typ
- konteringsmatris med sparade regler
- tydlig fallback med `DEF`
- samma prioritet i kodningsvy, transaktionsvy, rekommendationer, auto-match och AI-stöd
- banktransaktionslistan är paginerad med 20 rader per sida
- paginerad fakturalista med 20 rader per sida
- kundfakturor hämtas via befintlig invoices-tjänst med page/pageSize
- leverantörsfakturor hämtas via hub-ägd SQL med samma invoice-kontrakt
- demo-läget följer samma fakturafilter som live-läget

Vad som återstår
----------------

Nästa steg är främst förfining:

- fler regler
- bättre undantag per bolag och konto
- tydligare rapportering
- bättre heuristik för specialfall
- eventuell flytt av regelstorage till databas om modellen stabiliseras
- fullt databaspaginerad sammanslagen `Alla typer`-vy, om vi vill ta bort den sista in-memory-sammanslagningen

Kort förklaring att använda internt
-----------------------------------

Om du vill beskriva det enkelt för en chef:

"Bankavstämningen är uppdelad i steg. Först tolkar vi bankhändelsen, sedan bestämmer vi vad den är för typ, därefter föreslår vi kontering och först när det är en kundinbetalning försöker vi matcha mot faktura. Leverantörsbetalningar hämtas också in i samma modul, men från en hub-ägd SQL-källa och med samma paginerade fakturalista. Vi har också byggt så att regler kan sparas per bolag och bankkonto, med `DEF` som tydlig standard."

Det som är viktigt
------------------

Vi bygger inte ett stort regelmonster i UI:t.

Vi bygger ett enkelt arbetsflöde där:

- användaren ser en sak i taget
- systemet gör mer i bakgrunden
- samma regelordning används i hela flödet
