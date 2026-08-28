# Fakturor (Invoices) - Portal Guide

## Syfte
- Ge användaren en snabb översikt över obetalda/betalda fakturor och vilka som kräver uppföljning.

## Viktiga begrepp
- Obetald: faktura som ännu inte registrerats som betald i systemet.
- Förfallen: faktura där förfallodatum passerats (just nu kan datum vara en approximation beroende på datakälla).
- "Viktigast att följa upp": visar en tydlig kandidat för åtgärd, t.ex. äldst förfallen eller störst belopp.

## Vanliga åtgärder
- Visa faktura: öppna detaljsida för en specifik faktura.
- Filtrera: sök på fakturanummer, kund eller säljare och filtrera på datumintervall eller år.

## Standardbeteende
- Fakturasidan laddar inte all historik direkt.
- Standardsidan visar innevarande år.
- Om innevarande år är tomt kan systemet fallbacka till senaste år med data.
- Listan är server-side paginerad per tab.

## Viktigt att skilja på
- Fakturasidan visar filtrerbar lista och summering för vald period.
- Fakturakortet på dashboarden är däremot en snapshot för aktivt bolag, inte en periodiserad fakturarapport.

## Datakällan (kort)
- I Jeeves-läget kan fakturor hämtas från tabellen `dbo.ft` och kopplas till kund via `dbo.fr`.
