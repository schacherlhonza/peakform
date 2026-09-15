# TrainCoach

Webová aplikace pro spolupráci trenéra a sportovce — náhrada trenérské tabulky v Google Sheets.
Propojuje tréninkové plány, skutečně odtrénované aktivity, ranní/večerní check-iny, spánek/HRV/
klidovou tepovou frekvenci, jídlo a pitný režim, závody a cíle, komentáře mezi trenérem a
sportovcem a automaticky generované (pravidly řízené, ne AI-diagnostické) denní reporty.

## Obsah

- [Stack](#stack)
- [Architektura](#architektura)
- [Požadavky](#požadavky)
- [Rychlý start (Docker Compose)](#rychlý-start-docker-compose)
- [Lokální vývoj bez Dockeru](#lokální-vývoj-bez-dockeru)
- [Databázové migrace](#databázové-migrace)
- [Demo účty](#demo-účty)
- [Testy](#testy)
- [Konfigurace integrací](#konfigurace-integrací)
- [Co je plně funkční vs. mock/import](#co-je-plně-funkční-vs-mockimport)
- [Známá omezení](#known-limitations-known-omezení)
- [Struktura projektu](#struktura-projektu)

## Stack

- **Backend:** .NET 10, ASP.NET Core Web API (C#), Entity Framework Core, ASP.NET Core Identity,
  JWT access tokeny + rotující refresh tokeny, FluentValidation, Serilog, Swagger/OpenAPI.
- **Frontend:** React 19, TypeScript, Vite, React Router, TanStack Query, React Hook Form + Zod,
  Mantine UI, react-i18next (čeština), API klient generovaný z OpenAPI pomocí orval.
- **Databáze:** PostgreSQL 17.
- **Lokální běh:** Docker Compose (backend, frontend, PostgreSQL).

Podrobné zdůvodnění architektonických rozhodnutí je v [`docs/architecture.md`](docs/architecture.md)
a v [`docs/decisions/`](docs/decisions/) (ADR).

## Architektura

Modulární monolit (ne microservices — zdůvodnění v `docs/decisions/0001-modular-monolith.md`):

```
backend/
  src/
    TrainCoach.Domain           doménové entity, enumy — bez závislostí na frameworku
    TrainCoach.Application      use-case služby, DTO, validátory, rozhraní
    TrainCoach.Infrastructure   EF Core, Identity, JWT, background joby, DB konfigurace
    TrainCoach.Integrations     Strava adaptér, Garmin/MySASY mock providery, CSV import
    TrainCoach.Api               kontrolery, autentizace/autorizace, Program.cs
  tests/
    TrainCoach.Domain.Tests
    TrainCoach.Application.Tests
    TrainCoach.Api.IntegrationTests
frontend/
  src/
    api/generated/              TanStack Query klient generovaný z backend OpenAPI spec (orval)
    auth/  dashboard/  calendar/  workouts/  checkins/  reports/
    athletes/  races/  nutrition/  wellness/  settings/  integrations/  import/
    layout/  i18n/
docs/                            produktová a technická dokumentace, ADR
```

## Požadavky

- .NET SDK 10.0+
- Node.js 22+ a npm
- Docker Desktop (pro `docker compose`) — nebo lokálně nainstalovaný PostgreSQL 17, pokud chcete
  spouštět backend/frontend přímo bez Dockeru

## Rychlý start (Docker Compose)

```bash
git clone <repo-url> traincoach
cd traincoach
cp .env.example .env
# otevřete .env a nastavte POSTGRES_PASSWORD a JWT_SIGNING_KEY na vlastní silné hodnoty
docker compose up --build
```

Po naběhnutí:

- Frontend: http://localhost:5173
- Backend API + Swagger: http://localhost:5158/swagger
- PostgreSQL: localhost:5432 (přihlašovací údaje dle `.env`)

Při prvním startu backend automaticky spustí EF Core migrace a v prostředí `Development` naseeduje
demo data (viz [Demo účty](#demo-účty)). `ASPNETCORE_ENVIRONMENT` v `docker-compose.yml` je ve
výchozím stavu `Production` — pro demo data při Docker běhu nastavte v `.env`:
`ASPNETCORE_ENVIRONMENT=Development`.

Zastavení: `docker compose down` (data v PostgreSQL zůstanou v pojmenovaném volume
`traincoach_postgres_data`; pro úplný reset: `docker compose down -v`).

## Lokální vývoj bez Dockeru

### Backend

```bash
cd backend
dotnet restore TrainCoach.slnx
# spusťte PostgreSQL 17 lokálně (vlastní instalace) a upravte
# src/TrainCoach.Api/appsettings.Development.json, pokud se liší connection string
dotnet run --project src/TrainCoach.Api
```

Backend poběží na `http://localhost:5158` (viz `src/TrainCoach.Api/Properties/launchSettings.json`),
Swagger UI na `http://localhost:5158/swagger`. V prostředí `Development` se při startu automaticky
aplikují migrace a naseedují demo data.

### Frontend

```bash
cd frontend
cp .env.example .env   # VITE_API_BASE_URL již ukazuje na http://localhost:5158
npm install
npm run dev
```

Frontend poběží na `http://localhost:5173`.

### Regenerace API klienta

Backend OpenAPI spec je uložen jako `backend/openapi.json` (commitnutý snapshot) a frontend z něj
generuje typovaný TanStack Query klient. Po změně backend API:

```bash
# 1. spusťte backend v prostředí Testing, aby se vygenerovala aktuální swagger.json bez nutnosti DB:
cd backend/src/TrainCoach.Api
ASPNETCORE_ENVIRONMENT=Testing dotnet run --no-launch-profile --urls http://localhost:5299 &
curl http://localhost:5299/swagger/v1/swagger.json -o ../../openapi.json
kill %1

# 2. přegenerujte frontend klienta:
cd ../../../frontend
npm run generate:api
```

## Databázové migrace

```bash
cd backend
dotnet tool install --global dotnet-ef --version 10.*   # pokud ještě není nainstalováno
dotnet ef migrations add NazevMigrace --project src/TrainCoach.Infrastructure --startup-project src/TrainCoach.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/TrainCoach.Infrastructure --startup-project src/TrainCoach.Api
```

Migrace se v `Development`/`Production` prostředí aplikují automaticky při startu backendu
(`Database.MigrateAsync()` v `Program.cs`) — ruční `database update` je potřeba jen pro explicitní
kontrolu nebo CI.

## Demo účty

Po prvním spuštění backendu v prostředí `Development` jsou k dispozici (heslo pro všechny:
**`Demo1234`**):

| Role      | E-mail                   | Jméno              |
|-----------|---------------------------|---------------------|
| Trenérka  | `kouc@demo.traincoach.cz` | Jana Procházková    |
| Sportovec | `jakub@demo.traincoach.cz` | Jakub Dvořák (aktivní 4týdenní blok přípravy na půlmaraton) |
| Sportovec | `tereza@demo.traincoach.cz` | Tereza Svobodová |

Demo data zahrnují: aktivní spolupráci trenérky s oběma sportovci, sezónu, cíl a závody (včetně
jednoho s already zapsaným výsledkem), čtyřtýdenní tréninkový blok (běh, intervaly se strukturou,
posilovna, OCR, odpočinek), odtrénované aktivity, zpětnou vazbu, ranní/večerní check-iny za
uplynulé dny, spánek/HRV/klidovou TF, jídlo a pitný režim, komentáře trenéra a sportovce, tepové
zóny a slovník trenérských zkratek. Žádná skutečná osobní ani zdravotní data nejsou v repozitáři
uložena — vše je fiktivní.

Seed je idempotentní (přeskočí se, pokud demo trenérský účet už existuje) a nikdy neběží v
`Production` ani v testovacím hostu.

## Testy

### Backend

```bash
cd backend
dotnet test TrainCoach.slnx
```

Zahrnuje: unit testy (pravidlový engine reportů, CSV import parser, doménová logika) a plné HTTP
integrační testy (`WebApplicationFactory` + izolovaná in-memory SQLite databáze na test — Docker/
Testcontainers nejsou v tomto vývojovém sandboxu k dispozici, viz [Známá omezení](#known-limitations-known-omezení)).
Integrační testy pokrývají mimo jiné: registraci/přihlášení/refresh, roli a autorizaci, **explicitní
test, že trenér nemůže číst data sportovce bez aktivního vztahu (IDOR)**, izolaci dat mezi sportovci,
celý vertikální scénář pozvánka→přijetí→plán→check-in→revoke, import s náhledem/idempotencí/
deduplikací, generování reportu a validaci API. CI navíc spouští stejnou sadu proti reálné
PostgreSQL (viz `.github/workflows/ci.yml`).

### Frontend

```bash
cd frontend
npm run typecheck
npm run lint
npm run test
npm run build
```

## Konfigurace integrací

Výzkum všech čtyř poskytovatelů (datum ověření, oficiální zdroje, doporučená strategie) je v
[`docs/integrations-research.md`](docs/integrations-research.md). Shrnutí:

- **Strava** — reálná OAuth2 integrace je hotová (`TrainCoach.Integrations/Strava/`). Pro aktivaci:
  1. vytvořte API aplikaci na https://www.strava.com/settings/api,
  2. do `.env` doplňte `STRAVA_CLIENT_ID`, `STRAVA_CLIENT_SECRET`, `STRAVA_REDIRECT_URI`,
  3. restartujte backend.
  Bez těchto hodnot zůstane propojení ve stavu „nepřipojeno" — zbytek aplikace funguje beze změny.
- **Garmin** — Connect Developer Program vyžaduje schválení pro firemní/enterprise použití (viz
  `docs/integrations-research.md`), není reálně dostupný pro tento projekt. Aktivní je
  `GarminDemoProvider` generující realistická fiktivní data přes stejné rozhraní
  (`IIntegrationProvider`), takže po případném budoucím schválení stačí přidat reálný adaptér beze
  změny zbytku pipeline.
- **MySASY** — obdobně mock provider (`MySasyDemoProvider`, včetně simulace HRV/klidové TF).
  Reálné „myAPI" existuje, ale vyžaduje kontaktování poskytovatele (viz výzkum).
- **Import z Google Sheets / CSV** — funkční import na `/import`: nahrání souboru → náhled s
  validací a označením chybných/nejednoznačných řádků → potvrzení. Do potvrzení se nic neukládá
  jako tréninková data. Doporučený postup: exportujte tabulku z Google Sheets jako CSV a nahrajte
  ji přes tento formulář (živé OAuth napojení na Google Sheets API není v MVP implementováno,
  zdůvodnění v `docs/integrations-research.md`).

## Co je plně funkční vs. mock/import

**Plně funkční (reálná data přes API):** registrace/přihlášení/role, pozvání a správa vztahu
trenér–sportovec včetně granulárních oprávnění a auditní stopy, tréninkové plány/týdny/tréninky
včetně strukturovaných úseků a šablon, tepové zóny, slovník zkratek, sezóny/cíle/závody, ruční
zápis odtrénované aktivity a zpětné vazby, komentáře, ranní/večerní check-in, jídlo a pitný režim,
hlášení bolesti/nemoci, osobní rekordy, pravidly řízené ranní/večerní reporty, CSV import s
náhledem a deduplikací, Strava OAuth (po doplnění vlastních API credentials).

**Mock/demo (funkční pipeline, fiktivní data):** Garmin a MySASY synchronizace aktivit/wellness dat
přes `IIntegrationProvider`/`IWellnessDataProvider` mock adaptéry — architektonicky identické s
reálnou integrací, jen bez skutečného volání externího API (viz `docs/integrations-research.md` pro
podmínky případné budoucí aktivace).

## Known limitations (known omezení)

- **Sandbox, ve kterém byl projekt vyvíjen, neměl Docker ani lokální PostgreSQL k dispozici** — plný
  `docker compose up` a testy proti reálné Postgres proto v tomto vývojovém prostředí nebyly
  spuštěny přímo; backend byl ověřen kompilací, spuštěním DI kontejneru a generováním EF Core
  migrace/OpenAPI spec, a celá aplikační logika (včetně IDOR testu) byla ověřena přes 40+
  integračních testů proti izolované SQLite databázi přes stejný ASP.NET Core hostitelský pipeline.
  CI workflow navíc spouští stejné testy proti reálné PostgreSQL. Uživatel by měl po naklonování
  spustit `docker compose up` sám a případné drobné odchylky (např. přesné chování Npgsql
  migrací) ověřit — kód pro to je hotový a zdokumentovaný.
- Kalendář je týdenní list/grid pohled bez drag-and-drop přeřazování — přesun tréninku se dělá přes
  kopírování (`POST /api/workouts/{id}/copy`) nebo úpravou data.
- Doručování reportů je připraveno jako rozhraní pro e-mail/push/Telegram, ale reálně implementován
  je jen in-app kanál (viz `docs/architecture.md`).
- Vygenerovaný JS bundle frontendu přesahuje doporučenou velikost pro code-splitting (viz `npm run
  build` výstup) — funkčně to nevadí, jde o budoucí optimalizaci.

## Struktura projektu

```
.
├── backend/                  ASP.NET Core řešení (viz výše)
├── frontend/                 React + Vite aplikace
├── docs/                     product-requirements, architecture, data-model, security,
│                             mvp-scope, integrations-research, decisions/ (ADR)
├── docker-compose.yml
├── .env.example
└── .github/workflows/ci.yml
```
