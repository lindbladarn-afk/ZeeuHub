# Lokal utvecklingsmiljö för ZeeU Hub

Den här guiden beskriver hur en utvecklare klonar `version-two`, återställer lokala kopior av `CustomerPortal` och `Jeeves6` och startar Hubben utan att dela databasfiler eller hemligheter genom Git.

## Vad som finns i repot

Repot innehåller applikationskod, Docker-konfiguration och verktyg för backup och återställning. Följande ingår avsiktligt inte:

- `.env`, lösenord, tokens eller andra hemligheter
- SQL Server-datafiler
- backupfiler för `CustomerPortal` eller `Jeeves6`

`CustomerPortal` innehåller Hubens identitet, användare och portalkonfiguration. `Jeeves6` innehåller Jeeves-data som används av funktionerna i Hubben. Den helt lokala miljön behöver därför en separat backup av varje databas. Databaserna ligger i samma SQL Server-container men ingår inte i samma `.bak`-fil.

## Förutsättningar

- Git
- Docker Desktop med Docker Compose
- åtkomst till projektet i Azure DevOps
- en sanerad `CustomerPortal-*.bak` och `Jeeves6-*.bak` som har delats via en godkänd, krypterad kanal

Apple Silicon stöds genom projektets befintliga `linux/amd64`-konfiguration. Den första starten kan därför ta längre tid.

## 1. Klona rätt gren

Kopiera repots Clone-URL från Azure DevOps och kör:

```bash
git clone <clone-url>
cd ZeeU.CustomerPortal
git switch version-two
git pull --ff-only origin version-two
```

All utveckling för den här versionen ska utgå från `version-two`, inte `main`.

## 2. Skapa lokal konfiguration

```bash
cp .env.example .env
```

Öppna `.env` och konfigurera minst:

```dotenv
MSSQL_SA_PASSWORD=<ett-starkt-unikt-lokalt-lösenord>
PORTAL_IDENTITY_CONNECTION_STRING=
```

En tom `PORTAL_IDENTITY_CONNECTION_STRING` gör att Docker Compose ansluter Hubben till den lokala databasen `CustomerPortal`. Lösenordet är lokalt för utvecklarens SQL Server-container och behöver inte vara samma som backupskaparen använde. Lämna integrationer inaktiverade tills deras uppgifter faktiskt behövs.

Om en befintlig `JeevesDatabase/DatabaseFiles` återanvänds måste `MSSQL_SA_PASSWORD` vara databasinstansens befintliga `sa`-lösenord. Att ändra `.env` byter inte lösenordet i en redan skapad SQL Server-volym.

`.env` är Git-ignorerad. Lägg aldrig anslutningssträngar eller hemligheter i spårade inställningsfiler.

## 3. Lägg backupen på rätt plats

Skapa mappen om den saknas:

```bash
mkdir -p JeevesDatabase/DatabaseBackup
```

Placera de två mottagna filerna där, exempelvis:

```text
JeevesDatabase/DatabaseBackup/CustomerPortal-local-20260811-120000.bak
JeevesDatabase/DatabaseBackup/Jeeves6-local-20260811-120000.bak
```

Backupmappen är Git-ignorerad och skickas inte tillbaka till Azure DevOps.

## 4. Återställ båda databaserna

Gör verktyget körbart om filrättigheten har tappats vid överföring:

```bash
chmod +x scripts/jeeves-db.sh
```

Återställ en ny helt lokal Hub-miljö:

```bash
./scripts/jeeves-db.sh restore all \
  JeevesDatabase/DatabaseBackup/CustomerPortal-local-20260811-120000.bak \
  JeevesDatabase/DatabaseBackup/Jeeves6-local-20260811-120000.bak
```

Verktyget:

1. validerar Docker och `.env`
2. startar endast `jeevesdb`
3. verifierar båda backupernas checksummor
4. kontrollerar att varje backup tillhör rätt databas
5. stoppar om någon av databaserna redan finns
6. återställer `CustomerPortal` och `Jeeves6` och kontrollerar att båda är online

Om en befintlig lokal Hub-miljö uttryckligen ska ersättas används:

```bash
./scripts/jeeves-db.sh restore all \
  JeevesDatabase/DatabaseBackup/CustomerPortal-local-20260811-120000.bak \
  JeevesDatabase/DatabaseBackup/Jeeves6-local-20260811-120000.bak \
  --replace
```

`--replace` skriver över innehållet i både lokala `CustomerPortal` och `Jeeves6`. Använd flaggan endast när båda databaserna får ersättas.

En enskild databas kan återställas vid behov:

```bash
./scripts/jeeves-db.sh restore customerportal JeevesDatabase/DatabaseBackup/CustomerPortal-local-20260811-120000.bak
./scripts/jeeves-db.sh restore jeeves6 JeevesDatabase/DatabaseBackup/Jeeves6-local-20260811-120000.bak
```

Kontrollera status efter återställning:

```bash
./scripts/jeeves-db.sh status
```

## 5. Starta Hubben

Starta utvecklingscontainern med hot reload:

```bash
docker compose up --build webapp-dev
```

Hubben blir tillgänglig på:

```text
http://localhost:5080
```

Öppna en ny terminal för status eller loggar:

```bash
docker compose ps
docker compose logs -f webapp-dev
```

Stoppa miljön utan att radera databasfilerna:

```bash
docker compose down
```

Kör inte `docker compose down -v` eller radera `JeevesDatabase/DatabaseFiles` om den lokala databasen ska behållas.

## Skapa en ny backup

Den utvecklare som äger den lokala källmiljön skapar båda backuperna med ett kommando:

```bash
./scripts/jeeves-db.sh backup all
```

Verktyget använder samma tidsstämpel för filerna, skapar separata `COPY_ONLY`-backupset, aktiverar komprimering och checksumma och verifierar databasidentiteten innan filerna rapporteras som klara:

```text
CustomerPortal-local-<tidsstämpel>.bak
Jeeves6-local-<tidsstämpel>.bak
```

En enskild databas kan också säkerhetskopieras med ett eget filnamn:

```bash
./scripts/jeeves-db.sh backup customerportal CustomerPortal-local-test.bak
./scripts/jeeves-db.sh backup jeeves6 Jeeves6-local-test.bak
```

En befintlig backup skrivs inte över utan ett uttryckligt val:

```bash
./scripts/jeeves-db.sh backup customerportal CustomerPortal-local-test.bak --overwrite
```

Verifiera en mottagen backup utan att återställa den:

```bash
./scripts/jeeves-db.sh verify JeevesDatabase/DatabaseBackup/CustomerPortal-local-20260811-120000.bak
./scripts/jeeves-db.sh verify JeevesDatabase/DatabaseBackup/Jeeves6-local-20260811-120000.bak
```

## Säker hantering

- Använd endast sanerad utvecklingsdata.
- Kontrollera att backupen inte innehåller verkliga personuppgifter, kunduppgifter, tokens eller lösenord.
- Dela backupen genom en godkänd krypterad kanal, aldrig genom Git.
- Använd separata lokala SQL-lösenord för varje utvecklare.

## Vanliga problem

### Docker Desktop is not running

Starta Docker Desktop och kör kommandot igen.

### MSSQL_SA_PASSWORD saknas

Kontrollera att `.env` finns och att `CHANGE_ME` har ersatts med ett starkt lokalt lösenord.
Docker Compose stoppar innan containrar ändras om variabeln saknas.

### En lokal databas finns redan

`CustomerPortal` eller `Jeeves6` finns redan och skyddas mot oavsiktlig överskrivning. Behåll databaserna eller kör restore med `--replace` om båda uttryckligen får ersättas.

### Hubben kan inte logga in

Kontrollera att `PORTAL_IDENTITY_CONNECTION_STRING` är tom och att `CustomerPortal` visas som `ONLINE` av `./scripts/jeeves-db.sh status`. Hubens användare och identitetskonfiguration ligger i den lokala `CustomerPortal`-databasen.
