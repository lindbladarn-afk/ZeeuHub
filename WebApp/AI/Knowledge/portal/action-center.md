# ZeeU Action Center

ZeeU Action Center är en samlingsplats för insikter/notiser som systemet upptäcker och som kräver åtgärd eller extra kontroll från användaren.

## Var hittar jag Action Center?
- På dashboarden finns ett kort “ZeeU Action Center” med en lista av insikter.
- I headern finns en badge/notis (vid språkvalet) som visar antal aktuella insikter. Klick på badgen tar dig till Action Center-sidan.

## Hur funkar en insikt?
En insikt består typiskt av:
- Titel (vad som behöver uppmärksammas)
- Prioritet (t.ex. Hög/Medel/Info)
- Upptäckt (tidpunkt då insikten skapades)
- Obehandlad (hur länge den legat utan åtgärd)
- En tydlig knapp/åtgärd, t.ex. “Hantera fakturor” eller “Öppna attester”

## Prioriteter (exempel)
- Hög prioritet: kräver snabb åtgärd (t.ex. obetalda/förfallna fakturor, stopp i flöde)
- Medel prioritet: viktigt men inte akut (t.ex. väntande attester som blockerar inköp)
- Info: bra att känna till (t.ex. trend/avvikelse som kan följas upp)

## Per användare
Insikter kan vara:
- Globala (gäller verksamheten)
- Per användare (t.ex. uppgifter/attester för en specifik attestant)

Server-side bör insikter som är “per användare” filtreras på inloggad användare (t.ex. UserId/PersSign), så att badgen och listan visar rätt antal för just den användaren.

## Vanliga frågor
- “Varför ser jag en badge?” → Det finns en eller flera insikter som behöver kollas.
- “Varför ser jag ingen data?” → Antingen finns inga aktiva insikter, eller så saknas koppling till datakälla/behörighet för just den användaren.
