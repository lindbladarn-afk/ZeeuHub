# Ordrar (Orders) - Portal Guide

## Syfte
Orders ger dig en översikt över inkommande/skapade ordrar, filtrering över tid och möjlighet att öppna orderdetaljer.

## Vanliga vyer
- Orderlista: sök, filtrera på datumintervall och sortera.
- Orderdetalj: visar rader, kund och orderrelaterade detaljer.

## Vanliga åtgärder
- Sök på ordernummer eller kund.
- Filtrera på datum (`from` / `to`) eller välj ett år för att hitta ordrar i en period.
- Öppna orderdetalj för en specifik order.

## Standardbeteende
- Orders laddar inte all historik direkt.
- Standardsidan visar innevarande år.
- Om innevarande år är tomt kan systemet fallbacka till senaste år med data för att undvika en tom sida.
- Listan är server-side paginerad.
- KPI:er och lista hämtas separat för bättre prestanda.

## Tips
- Om du får för många träffar: välj år eller smalna av datumintervallet och använd ett tydligt sökord.
- Sortering kan påverka vilka ordrar som visas först (t.ex. senaste först).
