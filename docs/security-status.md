# Säkerhetsstatus

Intern status för säkerhetshårdning i ZeeU Customer Portal.

_Uppdaterad: 2026-05-02_

## Vad vi har hårdat

- State-changing actions som tidigare gick via `GET` är nu skyddade med `POST` och antiforgery.
- `ChangeActiveCompany` kräver nu antiforgery och skickas via formulär i stället för länk.
- `SetLanguage` kräver antiforgery och accepterar bara kända språk från lokaliseringskonfigurationen.
- Admin-flöden som återutskick av verifieringsmail och testloggning är nu också skyddade med antiforgery.
- Produktionsdashboardens uppdateringsflöde är nu `POST`-baserat med antiforgery.
- Action Center-statusuppdateringar kräver antiforgery.
- NotifyMe-flöden faller nu closed när runtime-context inte kan verifieras i stället för att använda gammal sessiondata som fallback.
- Regressionstester finns för de viktigaste säkerhetsytorna så att skydden inte lätt försvinner igen.

## Vad som återstår

- Session används fortfarande som cache för användar- och tenantkontext i flera delar av appen.
- Flera read-flöden bygger fortfarande på att sessionen redan är korrekt uppsatt.
- Tenant- och behörighetskontroller är bra på många ställen, men de är inte helt centraliserade.
- Det finns fortfarande områden där vi bör minska beroendet av sessiondata som beslutsunderlag.

## Hur nära zero trust vi faktiskt är

Vi är närmare zero trust än tidigare, men vi är inte fullt där ännu.

Det som redan stämmer bra:

- explicit autentisering och auktorisering på många controllers
- principen om minsta privilegium i flera flöden
- antiforgery på muterande requests
- tenant- och bolagskontroller innan känsliga data hämtas eller skrivs

Det som fortfarande saknas för att kalla lösningen strikt zero trust:

- konsekvent verifiering per request i stället för att lita på sessioncache
- mer centraliserad policyhantering för tenant och submodules
- tydligare separation mellan identitet, session och affärskritisk auktorisation

## Vad vi behöver göra framåt för ökad säkerhet till produktion

1. Minska beroendet av session som sanningskälla.
   - Använd session som cache, inte som primär auktoritet.
   - Fail closed när runtime-context inte kan verifieras.

2. Centralisera tenant- och accesskontroller.
   - Lägg mer av logiken bakom tydliga policies eller guards.
   - Undvik att varje controller själv tolkar samma regler på olika sätt.

3. Behåll antiforgery på alla muterande webbytor.
   - Standardisera mönstret för formulär och JSON-baserade anrop.
   - Se till att nya actions inte läggs till utan skydd.

4. Granska fler muterande ytor.
   - Kontrollera särskilt admin-, dashboard- och integrationsflöden.
   - Säkerställ att `GET` endast används för läsning.

5. Utöka testskyddet.
   - Lägg fler regressionstester för auth, antiforgery och tenantgränser.
   - Testa både tillåtet och otillåtet beteende.

6. Säkerställ konsekvent loggning och spårbarhet.
   - Logga behörighetsavslag och kritiska säkerhetshändelser utan att exponera känslig data.
   - Håll felmeddelanden användbara men inte avslöjande.

## Rekommenderad formulering i extern kommunikation

Om ni vill beskriva detta utåt är en försiktig formulering bättre än att säga full zero trust:

`ZeeU Hub bygger på Zero Trust-principer med explicit autentisering, policybaserad auktorisering, antiforgery och strikt kontroll av tenantkontext.`
