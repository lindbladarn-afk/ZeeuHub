# Main Dashboard

## Syfte
Dashboarden ger en snabb översikt över det aktiva bolagets affärsläge, med fokus på pengar, ordertrend, fakturastatus och åtgärder som kräver uppföljning.

## Viktig grundregel
Dashboarden visar data för **aktivt bolag** (`ForetagKod`) och använder inte alltid all historik.

När du förklarar dashboarden för en användare ska du alltid vara tydlig med:
- vilket bolag som visas
- vilken period ett kort bygger på
- om systemet har fallbackat till en äldre period med data

## Vad innehåller dashboarden?
- KPI-kort för omsättning och snittordervärde
- Fakturasammanfattning
- Omsättningsgraf
- Toppsäljande produkter
- Senaste kundaktivitet
- ZeeU Action Center

## KPI-kort

### Omsättning (senaste 12 mån)
- Visar omsättningen för rullande 12 månader.
- Underlaget bygger på den laddade analysperioden, inte all historisk data i databasen.
- Kortet har `Visa underlag`, där användaren kan se vilka orderdata KPI:n bygger på.

### Snittordervärde (30 dagar)
- Visar genomsnittligt ordervärde för de senaste 30 dagarna.
- Det är avsiktligt stabilare än en 7-dagarsvy och passar bättre för B2B-data.
- Kortet har `Visa underlag`, där användaren kan se vilka ordrar som ingår i beräkningen.

### Fakturor
- Fakturakortet är en **snapshot**, inte en periodiserad trend.
- Det visar nuvarande status för aktivt bolag, till exempel:
  - antal obetalda
  - obetald totalsumma
  - betald totalsumma
  - senaste eller viktigaste förfallna fakturor
- Kortet ska därför förklaras som en nulägesbild, inte som ett tidsseriekort.

## Omsättningsgraf
Grafen bygger på samma huvudanalys som omsättningskortet: rullande 12 månader.

### Periodlägen
- **Vecka**: senaste 10 veckorna inom rullande 12 månader
- **Månad**: 12 månader månad för månad
- **Kvartal**: senaste 4 kvartalen inom rullande 12 månader
- **12 mån**: summerad översikt för rullande 12 månader

### Viktigt
- Knappen `12 mån` betyder inte all historik.
- Den betyder en summerad vy för den laddade 12-månadersperioden.

## Fallback till äldre data
Om det inte finns orderdata för senaste 12 månaderna kan dashboarden fallbacka till den senaste period där det faktiskt finns data.

Detta ska förklaras tydligt:
- siffrorna är då riktiga
- men de gäller en äldre period än standardperioden
- dashboarden visar då en varning eller informationsrad om att en äldre period används

## ZeeU Intelligence i dashboarden
I toppen finns en fråga-ruta “Fråga ZeeU Intelligence…”.

Bra användning:
- fråga vad ett kort betyder
- be AI:n förklara vilken period siffran bygger på
- be AI:n förklara varför ett kort visar äldre data
- fråga varför fakturor och omsättning inte ser ut att följa exakt samma trend

## Hur AI:n bör förklara vanliga frågor

### “Varför matchar inte siffrorna mellan kort och graf?”
Det kan bero på att korten använder olika perioddefinitioner:
- omsättning: senaste 12 månaderna
- snittordervärde: senaste 30 dagarna
- fakturor: aktuell snapshot

### “Varför ser jag äldre data?”
Om inga ordrar finns för senaste 12 månaderna kan dashboarden visa den senaste period där det finns data. Det är en fallback för att undvika tomma kort.

### “Vad betyder 12 mån?”
Det är den summerade vyn för rullande 12 månader, inte all historik som finns i databasen.

### “Varför ser jag inte en modul?”
Moduler styrs av roll, feature flags och företagsbehörighet.
