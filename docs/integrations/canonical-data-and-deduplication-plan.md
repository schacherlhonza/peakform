# Canonical Data & Deduplication Architecture

Stav: implementováno (backend + minimální frontend), 2026-09-17.

## 1. Cíl a princip

TrainCoach se napojuje na řadu externích zdrojů dat (Strava, intervals.icu; architektonicky připraveno pro Oura, WHOOP, Garmin, HealthKit, Health Connect). Kritický požadavek: stejná reálná aktivita, spánek nebo jiná metrika nesmí být uložena vícekrát jako nezávislé záznamy, ani se nesmí tiše zprůměrovat/přepsat, když si zdroje neshodují.

```
Raw source record  ≠  Canonical activity / canonical wellness observation  ≠  Selected daily metric
```

Vlastní PostgreSQL databáze je canonical store. Žádný konektor nezapisuje přímo do finálních doménových tabulek — vše prochází `SyncOrchestrator` → matcher → connector policy → canonical entity.

## 2. Audit původního stavu (před touto změnou)

- Dedup byl **jen exact-match**: `(Source, ExternalId)` pro aktivity (DB unique, správně), `(AthleteUserId, Date, Source)` pro wellness (DB unique, správně — dobrý základ, wellness už byl "jeden řádek per zdroj per den").
- `docs/data-model.md` §5.1/5.2 popisoval fuzzy matching a `SourcePrecedence` — **nikdy neimplementováno**. Enum `SourcePrecedence` existoval, ale nikdy nebyl čten.
- intervals.icu's `device_name` (např. "Garmin Forerunner 965") se parsoval, ale zahazoval — nikdy persistován strukturovaně.
- **Žádný cross-provider identity koncept** — aktivita ze Stravy a "stejná" aktivita přes intervals.icu se nedaly propojit. Systém se dosud viditelným duplicitám vyhnul jen díky tomu, že intervals.icu API samo skrývá Strava-sourced aktivity (platformní omezení, ne app-level ochrana).
- Žádné webhooky pro žádného poskytovatele; sync jen on-connect + manuální tlačítko; `SyncTrigger.Scheduled` nevyužitý mrtvý kód.
- `ActivityService.CreateManualAsync` — žádná dedup kontrola (zůstává tak, viz §11 Known limitations).
- Nulové testy na sync/dedup/upsert cestu.
- Tokeny správně šifrované (ASP.NET Data Protection) — beze změny.
- Žádná role Admin (`AppRole` = `Athlete | Coach`) — proto je celé řešení self-service, ne admin panel.

## 3. Cílová architektura

### 3.1 Přejmenování a rozšíření `DataProvenance` → `ActivitySourceRecord`

Rename + rozšíření z existující tabulky (ne nová paralelní schéma) — 1:1 vztah s `CompletedActivity` se stal many:1, takže více zdrojů může odkazovat na jednu kanonickou aktivitu. Existující unique index `(Source, ExternalId)` zůstal beze změny — to je level-1 idempotence, nikdy nenahrazená fuzzy logikou.

`CompletedActivity` získala `PrimarySourceRecordId` (Guid?, **bez EF FK** — viz níže), `MatchStatus`, `NormalizedFingerprint`.

**Poznámka k implementaci:** `PrimarySourceRecordId` byl původně navržen jako reálná FK relace (`HasOne(...).WithMany()`), ale to vytváří vzájemný cyklus s `ActivitySourceRecord.CompletedActivityId` (required FK zpět), který EF Core's `SaveChanges` batching nedokáže rozřešit v jednom volání (`DbUpdateConcurrencyException`, "circular dependency"). Řešení: `PrimarySourceRecordId` je plain indexovaný sloupec bez EF-modelovaného vztahu, resolvovaný přes již načtenou `SourceRecords` kolekci (`activity.SourceRecords.FirstOrDefault(sr => sr.Id == activity.PrimarySourceRecordId)`).

### 3.2 Wellness — beze změny schématu, jen nová selection vrstva

5 existujících tabulek (`HrvMeasurement`, `RecoveryMetric`, `SleepRecord`, `WeightMeasurement`, `TrainingLoadSnapshot`) zůstávají přesně jak jsou — jejich `(AthleteUserId, Date, Source)` unique index už splňuje "nikdy neprůměrovat". Nové: `DailyMetricSelection` (cache/audit toho, který zdroj "vyhrává"), `AthleteMetricSourcePrecedence` (per-athlete override), `MeasurementContext` enum na `HrvMeasurement`/`SleepRecord` (popisný, ne dedup klíč).

### 3.3 Nové entity

- **`ConnectorDomainPolicy`** (`AthleteUserId, Provider, Domain, Mode, IsAthleteOverride`) — vždy athlete-scoped (žádná globální politika), protože správný default inherentně závisí na tom, co daný sportovec má připojené.
- **`MergeDecision`** — permanentní, revertibilní audit trail. Nikdy se nemaže ani neupravuje po faktu; revert zapisuje nový řádek.
- **`DuplicateCandidate`** — pending work-queue (60–84 confidence pásmo), odlišná od `MergeDecision` (ta je trvalý záznam rozhodnutí, i automatických, které do queue nikdy nešly).
- **`DuplicateDryRunReport`** — snapshot dry-run běhu (vlastní malá entita, ne přetížení `GeneratedReport`, které má jiný tvar/účel — denní narativní report per athlete/day).

### 3.4 Matching cascade (`IActivityMatchingService`)

Level 1 (idempotentní upsert) zůstává před matcherem, nikdy nahrazená — dnes v `ActivityIngestionService` (vyčleněno ze `SyncOrchestrator`, sdílí ho živý sync i import Strava archivu, viz `docs/integrations/strava-archive-import.md`). Matcher se volá jen na miss:

- **Level 2** (shared external identity cross-provider): no-op dnes (žádný poskytovatel nevystavuje cizí ID), reálný extension point pro budoucnost.
- **Level 3** (FIT file identity): `FitFileUuid` match → okamžité `AutoMerge`, confidence 100. Plní ho import Strava archivu (`fit:{manufacturer}:{serial}:{time_created}` z FIT `file_id`); žádný live adaptér zatím ne.
- **Stejný zdroj se neslučuje**: kandidát, který už má `ActivitySourceRecord` se stejným `Source` (a jiným `ExternalId` — shodu vyloučil level 1), je vyřazen ze skórování. Jeden poskytovatel nehlásí tutéž aktivitu dvakrát (např. dvě posilování téhož odpoledne na Stravě).
- **Level 4** (deterministický fingerprint, jen kandidátní index): `{Sport}|{StartUtc/5min}|{Duration/60s}|{Distance/100m nebo NODIST}|{DeviceName nebo NODEVICE}`.
- **Level 5** (confidence scoring 0–100, viz `docs/integrations/activity-matching.md` pro přesný algoritmus a defaultní váhy).

Skóre ≥85 → `AutoMerge`. 60–84 → `FlagForReview` (aktivita se vytvoří, ale je oznakovaná `PendingReview` a zapíše se `DuplicateCandidate`). <60 → `TreatAsSeparate`. Dva kandidáti se stejným top skóre v review pásmu → vždy `FlagForReview`, nikdy automatické sloučení (kryje "warm-up + závod ve stejném okně").

### 3.5 Connector policy engine (`IConnectorPolicyService`)

`ConnectorMode`: `Primary | Secondary | EnrichmentOnly | FallbackOnly | Disabled`, samostatně per `DataDomain` (`Activities, PlannedWorkouts, Sleep, Hrv, RestingHeartRate, DailyWellness, BodyComposition, VendorScores`). Výchozí seed tabulka (v `ConnectorPolicyService.DefaultMode`):

| Provider | Domain | Default |
|---|---|---|
| Strava | Activities | `FallbackOnly` pokud je připojeno intervals.icu, jinak `Primary` |
| Strava | ostatní | `Disabled` |
| IntervalsIcu | Activities/PlannedWorkouts/Sleep/Hrv/RestingHeartRate/DailyWellness/VendorScores | `Primary` |
| IntervalsIcu | BodyComposition | `Secondary` |
| Garmin/MySASY demo | Activities | `Secondary` |
| Oura/WHOOP (připraveno) | Activities | `EnrichmentOnly`; Sleep/Hrv/RestingHeartRate | `Secondary`; VendorScores | `Primary` |

**Zamčené rozhodnutí (na žádost architekta):** `EnsureDefaultsAsync` **retroaktivně přepočítává** non-override řádky při každé změně stavu připojení — takže připojení intervals.icu po Strava automaticky přepne Strava na `FallbackOnly`. Athlete override se nikdy nepřepočítává.

Politika ovlivňuje přesně dva body: zda `NoCandidate` výsledek vytvoří novou kanonickou aktivitu (`EnrichmentOnly` nikdy), a zda se zdroj po `AutoMerge` stane primárním (`Primary` vždy povyšuje, `Secondary` jen když ještě nic není primární, `EnrichmentOnly`/`FallbackOnly` nikdy).

## 4. Migrace existujících dat (non-destructive)

1. **Additivní EF migrace** `AddActivitySourceRecordsAndMatching` — `RenameTable`/`RenameColumn`/`RenameIndex` pro `DataProvenances → ActivitySourceRecords` (**ne** drop+recreate — EF by to jinak scaffoldovalo jako ztrátu dat; ověřeno a opraveno ručně), nové nullable sloupce, nové tabulky. Ověřeno na reálné dev databázi: **143/143 řádků zachováno** (viz §9).
2. **Idempotentní backfill** (`IBackfillActivitySourceRecordsCommand`) — dopočítá `PrimarySourceRecordId`/`NormalizedFingerprint` pro existující aktivity s právě jedním zdrojovým záznamem. Bezpečné opakované spuštění.
3. **Dry-run report** (`IDuplicateDryRunReportService`) — čistě informační, nikdy nezapisuje `MergeDecision`/`DuplicateCandidate`. Reálný výstup na dev datech: viz §9.
4. Auto-merge se aplikuje jen pro `≥85` tier; `60–84` tier čeká v review queue; `<60` beze změny.
5. Ops nástroj: `dotnet run --project src/TrainCoach.Api -- --backfill-and-dry-run` (spustí migrace + backfill + dry-run, vypíše výsledky, neotevře Kestrel).

## 5. API (self-service, žádná admin role)

```
GET/PUT /api/athletes/{athleteUserId}/connector-policies
GET     /api/athletes/{athleteUserId}/duplicate-candidates
POST    /api/duplicate-candidates/{id}/merge
POST    /api/duplicate-candidates/{id}/dismiss
POST    /api/merge-decisions/{id}/revert
GET     /api/athletes/{athleteUserId}/merge-decisions
POST    /api/athletes/{athleteUserId}/duplicate-dry-run-reports
```

Autorizace: stejný self-service model jako `IntegrationConnectionsController` — jen vlastník účtu, nikdy trenér (žádná `Admin` role v appce dnes existuje).

## 6. Frontend

`frontend/src/integrations/IntegrationsPage.tsx` — nová collapsed sekce "Pokročilá nastavení" per provider karta s dropdowny `ConnectorMode` per `DataDomain`, a banner "Ke kontrole: N možných duplicit" když existují pending kandidáti.

`frontend/src/integrations/DuplicateReviewPage.tsx` (nová stránka, route `/integrations/duplicates`) — side-by-side srovnání kandidátů, tlačítka merge/dismiss, historie sloučení s možností revertu.

## 7. Bezpečnost

Beze změny existujícího modelu (token encryption, GDPR export/erasure, audit log — viz `docs/security.md`). Nové endpointy dodržují stejný self-service trust model. Žádná nová citlivá data se neukládá — `MergeDecision`/`DuplicateCandidate` jen odkazují na existující aktivity přes ID.

## 8. Testovací strategie

- **Pure unit testy** (`TrainCoach.Application.Tests`): scoring matrix (`ActivityMatchingServiceScoringTests`), fingerprint bucketing (`ActivityFingerprintTests`), connector policy defaults (`ConnectorPolicyServiceTests`).
- **DB-backed integration testy** (`TrainCoach.Api.IntegrationTests`, SQLite in-memory přes `WebApplicationFactory`): end-to-end sync s fake providery (`SyncOrchestratorMatchingTests` — Garmin-via-Strava-a-IntervalsIcu auto-merge, dvě různé aktivity se neslučují, wellness ze dvou zdrojů se nikdy neprůměruje), HTTP-level duplicate review (`DuplicateReviewApiTests` — merge/dismiss/revert round-trip, cross-athlete izolace, soft-delete nikdy fyzické mazání).
- Viz §9 pro skutečné výsledky test runu.

## 9. Reálné výsledky (ne odhad — spuštěno na této repo)

- **Build**: `dotnet build`/`dotnet build -c Release` — 0 chyb. Frontend `npm run typecheck`/`lint`/`test`/`build` — vše zelené.
- **Testy**: 70/70 passed (`TrainCoach.Domain.Tests` 5, `TrainCoach.Application.Tests` 35, `TrainCoach.Api.IntegrationTests` 30) v Debug i Release.
- **Migrace na dev DB**: `20260917120114_AddActivitySourceRecordsAndMatching` a `20260917122927_AddDuplicateDryRunReports` úspěšně aplikovány. Ověřeno: 143 `DataProvenances` → 143 `ActivitySourceRecords` (žádná ztráta), unique indexy/constraints korektně přejmenované.
- **Backfill**: `scanned=143 backfilled=143 alreadyDone=0 skippedMultipleSources=0`.
- **Dry-run** (nad demo daty, všechny `Source=Manual`, žádný reálný cross-provider duplicate): `scanned=143 exact=0 highConfidence=0 uncertain=37`. Očekávaný výsledek — demo data jsou opakující se podobné tréninky (ne skutečné duplicity), takže je správné, že **nic nedosáhlo auto-merge práh (85)** a 37 párů skončilo v review pásmu k lidskému posouzení.

## 10. Nasazení a rollback

- Migrace je striktně additive/rename — rollback (`dotnet ef database update <předchozí>`) je bezpečný, protože `Down()` je napsán symetricky (rename zpět, ne drop+recreate).
- Před nasazením do produkčního-podobného prostředí: spustit `--backfill-and-dry-run`, zkontrolovat tier counts, až poté zvážit spuštění skutečného auto-merge nad `≥85` tier (dnes jen přes live sync cestu — dedikovaný "spusť auto-merge z dry-run reportu" příkaz nebyl v tomto MVP implementován, viz §11).
- Zálohovat databázi (`pg_dump`) před první migrací v produkci — proveden reálný test na dev DB s předchozím `pg_dump` backupem.

## 11. Známá omezení a doporučené další kroky

- **Manuální zápis aktivit** (`ActivityService.CreateManualAsync`) stále nemá dedup kontrolu — vědomé rozhodnutí ponechat mimo tento MVP (matcher je navržen pro cross-source sync, ne pro manuální vstup), fast-follow kandidát.
- **Dry-run → skutečný auto-merge nad historickými daty** je implementován jako služba/CLI report, ale samotné hromadné spuštění merge nad `≥85` tier z uloženého reportu nebylo v tomto MVP zapojeno do CLI (`--backfill-and-dry-run` jen reportuje) — na demo datech nebylo potřeba, protože tier byl 0. Přidat jako `--apply-auto-merge` flag, až bude reálná potřeba.
- **Webhooky** (Strava, intervals.icu) — nadále nejsou implementovány, sync je jen on-connect/manuální. `SyncTrigger.Scheduled` zůstává nevyužitý.
- **Oura/WHOOP** — architektonicky připraveno (viz `docs/integrations/oura-whoop-activation.md`), žádné reálné volání API.
- **Garmin přímé napojení, HealthKit, Health Connect** — mimo rozsah, intervals.icu zůstává praktickou cestou k Garmin datům.
- **Revert mechanika** rekonstruuje odpojenou aktivitu z *aktuálních* fixed-column hodnot přeživší aktivity, ne z původních hodnot absorbovaného zdroje (ten je nezachovává samostatně, jen přes `RawPayloadJson`, pokud retained) — plně přesný revert by vyžadoval re-parse raw payloadu, což je provider-specific a mimo rozsah MVP.
- **Per-athlete tuning matcher prahů** — dnes globální (`appsettings.json`), ne per-athlete override (analogicky k `ConnectorDomainPolicy`), pokud se ukáže potřebné.
- **Vícenásobný kontext HRV za den ze stejného zdroje** (např. ranní i noční z jednoho zařízení) — schéma unikátního klíče `(AthleteUserId, Date, Source)` to nepodporuje; `MeasurementContext` je jen popisný. Řešit až bude reálná potřeba (Oura/WHOOP aktivace).

## 12. Kritické soubory

- `backend/src/TrainCoach.Domain/Execution/{ActivitySourceRecord,CompletedActivity,MergeDecision,DuplicateCandidate,DuplicateDryRunReport}.cs`
- `backend/src/TrainCoach.Application/Integrations/Matching/{IActivityMatchingService,ActivityMatchingService,ActivityFingerprint,ActivityMatchingOptions,BackfillActivitySourceRecordsCommand,DuplicateDryRunReportService}.cs`
- `backend/src/TrainCoach.Application/Integrations/{ConnectorPolicyService,SyncOrchestrator,IntegrationConnectionService}.cs`
- `backend/src/TrainCoach.Application/Wellness/DailyMetricSelectionService.cs`
- `backend/src/TrainCoach.Application/Execution/DuplicateReviewService.cs`
- `backend/src/TrainCoach.Api/Controllers/{ConnectorPolicyController,DuplicateReviewController}.cs`
- `backend/src/TrainCoach.Infrastructure/Persistence/Migrations/20260917120114_AddActivitySourceRecordsAndMatching.cs` (ručně opraveno na non-destruktivní rename)
- `frontend/src/integrations/{IntegrationsPage,DuplicateReviewPage}.tsx`
