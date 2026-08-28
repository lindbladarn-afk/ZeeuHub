# ZeeU Intelligence (AI)

## Syfte
ZeeU Intelligence hjälper användaren att:
- förstå vad siffror betyder (förklaringar)
- få svar baserat på data (t.ex. order/faktura) när datakällan tillåter det
- få hjälp med hur moduler i portalen används

## Två typer av frågor
1) **Hjälp/guide-frågor** (ingen SQL körs)
   - Ex: “Hur funkar ZeeU Action Center?”
2) **Datafrågor** (AI kan generera SELECT och sammanfatta resultat)
   - Ex: “Hur många förfallna fakturor har vi?”

## Följdfrågor
Du kan skriva följdfrågor utan att upprepa alla detaljer. AI:n har ett kort kontext-minne per användare och datakälla.

## Begränsningar
- AI kan bara använda tabeller/kolumner som finns i den valda datakällan.
- För vissa datakällor kan det saknas tabeller för omsättning/ordrar/fakturor.
- AI ska inte hitta på adminfunktioner som användaren saknar behörighet till.

