# Excelimport

## Syfte
Excelimport används för att ladda upp Excel/CSV-filer som importeras till en “staging”-yta för vidare hantering.

## Filtyper
Stödda filändelser:
- `.xls`, `.xlsx`, `.xlsm`, `.csv`

## Flöde (översikt)
1) Gå till **Excelimport**
2) Välj fil
3) Välj importtyp (ex. voucher) om UI:t erbjuder det
4) Ladda upp
5) Systemet visar resultat: antal lästa rader, giltiga/ogiltiga rader och batch-id

## Vanliga problem
- Fel filtyp → bara Excel/CSV stöds.
- Tom fil → välj en fil med innehåll och försök igen.
- Ogiltiga rader → hela batchen stoppas före staging; öppna redigeringsläget, filtrera på fel och följ valideringsorsaken på respektive rad.

## Begrepp
- **Staging**: temporär tabell/yta där importerade rader hamnar innan de används vidare.
- **Batch**: ett import-tillfälle (import batch id) som gör att man kan spåra vad som importerades när.
