# Glossary

## Allmänna begrepp
- Omsättning: total försäljning (ofta summerad order/faktura-belopp).
- Snittordervärde (AOV): genomsnittligt ordervärde för den period som kortet eller sidan använder. På dashboarden är standarden senaste 30 dagar.
- Rullande 12 månader: de senaste tolv månaderna räknat bakåt från den senaste relevanta datapunkten eller dagens datum beroende på vy.
- Snapshot: en nulägesbild av data just nu, inte en periodiserad trend.
- Fallback-period: en äldre period med data som visas när standardperioden saknar träffar.
- Förfallen faktura: faktura där `DueDate` passerats och som inte är betald.
- Attest: godkännande i web approval-flöde.
- Staging: en temporär tabell/yta där importerad data hamnar innan den används i “skarpa” flöden.
- Import-batch: ett importtillfälle (identifieras ofta med ett batch-id).

## Jeeves-begrepp (vanligt)
- ForetagKod: bolagskod som filtrerar data per bolag. Dashboard, orders, invoices och flera AI-frågor använder aktiv `ForetagKod` för att visa rätt bolagsdata.
- OrderNr / OrderNrAlfa: ordernummer (numeriskt respektive text).
- FtgNr: kundnummer.
