# Push naplánovaných tréninků na Garmin přes intervals.icu — návrh implementace

Stav: **implementováno 2026-10-06**, čeká na živý end-to-end test (PeakForm → intervals.icu → Garmin Connect → hodinky). Mechanismus ověřen živým experimentem 2026-09-21.

**Jak je to postavené (stručně):**
- `PlannedWorkoutPushRecord` (tabulka): stav jednoho tréninku v jednom externím kalendáři (Pushed / Removed / Failed, chyba, varování). Odpovídá variantě z §6.
- `IPlannedWorkoutPushProvider`, implementace v `IntervalsIcuIntegrationProvider`:
  - upsert přes `POST /athlete/0/events/bulk?upsert=true` s `external_id` = id tréninku v PeakForm;
  - mazání přes `PUT /athlete/0/events/bulk-delete` podle `external_id`;
  - text tréninku z `IntervalsIcuWorkoutDescriptionBuilder`.
- `PlannedWorkoutPushJob` (fronta na pozadí):
  - spouští se po vytvoření, úpravě, smazání a kopii tréninku (`TrainingPlanService`), po přepnutí souhlasu athletea a po (znovu)připojení účtu;
  - trénink je v kalendáři, jen když není smazaný ani odpočinkový, plán není smazaný a athlete má zapnutý souhlas;
  - jinak se odebere;
  - minulé tréninky se neposílají.
- **Souhlas athletea:** `IntegrationConnection.PushPlannedWorkouts`, výchozí stav vypnuto. Přepíná ho jen athlete na kartě intervals.icu v Integracích (`PUT /api/integrations/{provider}/push-planned-workouts`). Vypnutí odebere už odeslané tréninky.
- **Trenér vidí stav** na detailu tréninku (`GET /api/workouts/{id}/push-status`): odesláno / odebráno / chyba a varování.
- **Tepové cíle:** posílá se `Z{n} HR`. intervals.icu je vyhodnotí podle zón, které do něj PeakForm zapisuje (`garmin-workout-model.md` §5).
- **Známé omezení:** po odpojení intervals.icu zůstanou odeslané tréninky v jeho kalendáři (token je pryč), viz §9. Tento dokument shrnuje, co jsme v rámci rešerše a experimentu zjistili, a navrhuje přesně, kde a jak to zapojit do existujícího kódu PeakForm tak, aby trenér mohl naplánovat trénink a ten se athletovi reálně propsal na Garmin hodinky. Navazuje na `docs/integrations-research.md` §6 (TrainingPeaks rešerše + experiment) a `docs/intervals-garmin-data-audit.md`.

## 1. Cíl

Dnes PeakForm umí jen **číst** data z intervals.icu (aktivity, wellness). Trenér v appce naplánuje trénink (`PlannedWorkout` + `WorkoutSegment`), ale athlete ho nikde na svém Garmin zařízení neuvidí — musí se dívat do appky ručně. Cíl: když trenér vytvoří/upraví/smaže naplánovaný trénink, automaticky se to propíše do athletova intervals.icu kalendáře, a odtud (mimo naši kontrolu, ale ověřeno že to funguje) na jeho Garmin hodinky.

## 2. Ověřený mechanismus (shrnutí experimentu)

- **Cesta je nepřímá**: PeakForm → intervals.icu kalendář (`/events`) → Garmin Connect kalendář athletea → Garmin hodinky při příštím sync. Přímé Garmin Training API (viz `docs/integrations-research.md` §6) vyžaduje byznysové schválení a není použito.
- **Prerekvizita, kterou PeakForm nemůže zajistit sama**: athlete musí mít ve **svém vlastním** intervals.icu účtu zaškrtnuté "Upload planned workouts" a propojený Garmin Connect (OAuth udělený přímo intervals.icu, ne naší appce). Bez toho se event v intervals.icu kalendáři vytvoří, ale nikam dál se nepropíše — appka to nepozná (API vrátí 200 i bez tohoto propojení). **Nutno v UI zřetelně komunikovat**, že push na hodinky je mimo naši appku, athlete si ho musí jednou nastavit v intervals.icu.
- **OAuth scope**: appka dnes žádá `ACTIVITY:READ,WELLNESS:READ,CALENDAR:WRITE` (`IntervalsIcuIntegrationProvider.cs:47`, scope rozšířen 2026-09-21 přesně pro tento účel). Athlete musí mít účet **znovu připojený** po přidání scope — stará autorizace ho nemá.
- **Live ověřeno**: `POST https://intervals.icu/api/v1/athlete/0/events` s payloadem (viz §4) vrátil `200 OK`, vytvořil event (`id`, `uid`), a athlete **vizuálně potvrdil**, že se trénink objevil v Garmin Connect kalendáři (cloudová strana, nezávisle na fyzickém syncu hodinek). Cleanup přes `DELETE .../events/{id}` → `200 OK`.
- **Vedlejší nález**: oprava skutečné `DbUpdateConcurrencyException` v `IntegrationConnectionService.UpsertConnectionAsync` (viz commit `517fe17`/`30c490c`) — bez ní nešlo žádný athlete po odpojení znovu připojit. Netýká se přímo pushe, ale je to prerekvizita pro to, aby reconnect se scope vůbec fungoval.

## 3. Klíčová omezení (nutná pro UX i error handling)

| Omezení | Dopad na implementaci |
|---|---|
| **Jednosměrný push** — intervals.icu neumí stáhnout Garmin-vytvořené plány zpět ("Garmin doesn't allow to download their workouts", potvrzeno na fóru intervals.icu) | Netýká se nás přímo (jen pushujeme), ale znamená to, že athlete nemůže plán upravit na hodinkách a čekat, že se to vrátí do PeakForm — žádný konflikt k řešení. |
| **Upload okno ~7 dní dopředu** (komunitní zdroj, neověřeno oficiální dokumentací) | Push má smysl jen pro tréninky v blízké budoucnosti — plánování na měsíc dopředu se propíše, ale než dojde k samotnému zařazení na hodinky, intervals.icu/Garmin to stejně drží jen pro blízké dny. Netřeba řešit na naší straně, intervals.icu si to řeší samo. |
| **Podpora zařízení**: starší modely (Forerunner 235, Vivoactive 3) nepřijímají strukturované tréninky | Push samotný neselže, ale athlete na starém zařízení nic neuvidí. Nemáme signál o modelu hodinek — nelze detekovat, jen zmínit v UI jako known limitation. |
| **Žádný refresh token u intervals.icu** (`IntervalsIcuIntegrationProvider.cs:24-25`, existující známý fakt) | Push selže stejně jako čtení, pokud token vyexpiroval/byl odvolán — stejná `Error` cesta jako dnes u sync. |
| **Nekonzistentní JSON typy v odpovědích intervals.icu** (viz historické 2 neúspěšné sync běhy z 2026-09-18, `FlexibleInt32Converter`) | Parsování odpovědi `/events` musí být stejně defenzivní jako `IntervalsIcuWellnessEntry` — nepředpokládat pevné typy. |
| **Scope broader than requested**: athlete si na consent obrazovce může zaškrtnout i `ACTIVITY:WRITE`/`WELLNESS:WRITE`, i když appka žádala jen READ | Netýká se funkčně pushe, ale je to bezpečnostní poznámka k OAuth review — appka dnes žádný write kód pro Activity/Wellness nevolá, jen pro Calendar. |

## 4. API kontrakt (intervals.icu strana)

**Vytvoření/update** (ověřeno živě, jednotlivý event):
```
POST https://intervals.icu/api/v1/athlete/0/events
{
  "category": "WORKOUT",
  "start_date_local": "2026-09-23T17:00:00",
  "type": "Run",
  "name": "5x5min Threshold",
  "description": "- 15m 55% Warmup\n\n5x\n- 5m 120%\n- 2m 50%\n\n- 10m 50%",
  "moving_time": 2760,
  "external_id": "<PeakForm PlannedWorkout.Id>"
}
```
Odpověď obsahuje `id` (intervals.icu primární klíč, `number`), `uid`, `calendar_id`, `athlete_id`, a hlavně **`workout_doc.steps`** — intervals.icu si text v `description` samo rozparsuje do strukturovaných kroků s cíli (ověřeno živě). `external_id` je **důležité pole pro idempotenci** — komunitní dokumentace zmiňuje `POST .../events/bulk?upsert=true` a `PUT .../events/bulk-delete` s `[{"external_id": "..."}]`, což by šlo použít misto jednotlivých volání, jakmile budeme pushovat víc než jeden trénink najednou (např. celý týden).

**Smazání** (ověřeno živě, podle `id`):
```
DELETE https://intervals.icu/api/v1/athlete/0/events/{eventId}
```

**Neověřeno oficiálně** (jen komunitní zdroje + náš jeden živý test) — doporučeno ověřit znovu při implementaci, ne jen převzít: přesný seznam povolených `type` hodnot, chování při kolizi `external_id`, limity na délku `description`.

## 5. Mapování PeakForm doménového modelu → intervals.icu DSL

`WorkoutSegment` (`backend/src/TrainCoach.Domain/Planning/WorkoutSegment.cs`) má pole `Order`, `Type` (warm-up/interval/rest/cool-down...), `RepeatCount`, `DistanceMeters`/`DurationSeconds`, `IntensityTargetType` + `TargetHeartRateZoneNumber`/`TargetPaceSecondsPerKmMin/Max`/`TargetRpe`/`TargetPowerWatts`. Toto se **musí převést na textový DSL** v poli `description` (intervals.icu nemá JSON strukturu jako TrainingPeaks' `Workout Structure Object` — viz `docs/integrations-research.md` §6 — bere jen text).

Návrh mapovací logiky (nová třída, např. `IntervalsIcuWorkoutDescriptionBuilder` v `TrainCoach.Integrations/IntervalsIcu/`):

- Jeden `WorkoutSegment` bez `RepeatCount` → jeden řádek `- {délka} {cíl}`, např. `- 15m 55% Warmup` (délka z `DurationSeconds` v minutách, nebo z `DistanceMeters` pokud trvání chybí).
- `RepeatCount > 1` → blok `{N}x` následovaný odsazenými řádky pro segmenty ve stejné "repetici" (potřeba rozšířit `WorkoutSegment`/validátor o explicitní seskupení repetic, dnes `Order`+`RepeatCount` na jednom řádku nejspíš nerozlišuje, kde repetice začíná/končí — **ověřit při implementaci přesnou sémantiku `RepeatCount`**, než se píše builder).
- `IntensityTargetType` → cíl v DSL: `TargetPowerWatts` → `{watts}W` nebo `%FTP` (nutno znát athletovo FTP — není dnes součástí `WorkoutSegment`, řešit stejně jako TrainingPeaks' `PercentOfFtp`, buď athlete má FTP uložené jinde v appce, nebo posílat absolutní watty); `TargetHeartRateZoneNumber` → `Z{n}`; `TargetPaceSecondsPerKmMin/Max` → tempo rozsah; `TargetRpe` → RPE text.
- `Sport` (`PlannedWorkout.Sport`, `SportType` enum) → `type` pole (`Run`/`Ride`/`Swim`/...) — potřeba mapovací tabulka symetrická k `IntervalsIcuIntegrationProvider.MapSport` (ten mapuje opačným směrem, intervals.icu string → `SportType`; tady potřebujeme `SportType` → intervals.icu string).
- `PlannedWorkout.IsRestDay` → **nepushovat vůbec** (odpočinkový den nemá smysl posílat jako "workout" event).
- `PlannedWorkout.CoachDescription` (volný text) → pokud `Segments` je prázdné, použít přímo jako `description` beze změny (coach nemusí vždy strukturovat trénink, viz komentář v `PlannedWorkout.cs:7-9`).

## 6. Potřebné změny datového modelu

`PlannedWorkout` dnes **nemá žádné pole pro externí kalendářní event** (ověřeno — `backend/src/TrainCoach.Domain/Planning/PlannedWorkout.cs`, žádný `ExternalEventId`/`PushStatus`). Bez toho nejde dělat idempotentní update/delete. Navrhované doplnění (nová EF migrace, additive):

```csharp
// PlannedWorkout.cs
public long? IntervalsIcuEventId { get; set; }      // intervals.icu's own numeric event id, z response.id
public DateTime? PushedToIntervalsIcuAtUtc { get; set; }
public string? PushError { get; set; }               // poslední chyba, pro zobrazení trenérovi
```

Alternativa (konzistentnější s existujícím `ActivitySourceRecord` patternem pro aktivity): samostatná tabulka `PlannedWorkoutPushRecord (PlannedWorkoutId, Provider, ExternalEventId, PushedAtUtc, Error)` — lépe škáluje, pokud by v budoucnu přibyl další push cíl (např. přímé Garmin Training API, kdyby se někdy získalo schválení). Doporučuji **tuto variantu**, protože kopíruje už zavedený a otestovaný vzor (`ActivitySourceRecord` pro čtecí směr), místo přidávání provider-specific sloupců přímo na `PlannedWorkout`.

## 7. Kde v kódu zapojit (konkrétní místa)

- **Nové rozhraní** `IPlannedWorkoutPushProvider` v `TrainCoach.Application/Integrations/` (analogicky k existujícímu `IWellnessDataProvider`/`IActivityStreamProvider` v `IIntegrationProvider.cs`) s metodami `PushAsync(accessToken, PlannedWorkoutPushPayload)`, `DeleteAsync(accessToken, externalEventId)`.
- **Implementace** v `IntervalsIcuIntegrationProvider.cs` (`TrainCoach.Integrations/IntervalsIcu/`) — nová metoda vedle `FetchWellnessAsync`/`FetchRecentActivitiesAsync`, používá stejný `CreateAuthorizedClient` helper (`:299-304`).
- **Volání** z `TrainingPlanService.cs`:
  - `CreateWorkoutAsync` (`:118`) — po `db.PlannedWorkouts.Add(workout)` a uložení, zavolat push (ne před uložením — potřebujeme `workout.Id` jako `external_id`).
  - `UpdateWorkoutAsync` (`:144`) — po uložení změn, update existujícího eventu (nebo delete+recreate, pokud update endpoint nepodporuje změnu `start_date_local`/sportu — ověřit při implementaci).
  - `DeleteWorkoutAsync` (`:176`) — před/po soft-delete workoutu, zavolat `DeleteAsync` na intervals.icu straně, stejný best-effort pattern jako `IntegrationConnectionService.DisconnectAsync:85-95` (log warning, nikdy neblokovat lokální operaci).
  - `CopyWorkoutAsync` (`:189`) — nová kopie dostane vlastní push, ne sdílený `external_id` s originálem.
- **Athlete resolution**: `ResolveAthleteForWorkoutAsync` (`:221`) už existuje a řeší "kterému athletovi tenhle workout patří" — push logika potřebuje z `AthleteUserId` dohledat `IntegrationConnection` pro `IntervalsIcu`, stejným způsobem jako `--diagnose-intervals-icu`/`--test-calendar-push` ops nástroje dřív v tomto vlákně (`backend/src/TrainCoach.Api/Program.cs`, dnes odstraněné, ale vzor je zdokumentovaný v git historii commitu `30c490c`).
- **Graceful no-op**: pokud athlete nemá `IntervalsIcu` `IntegrationConnection` se `Status == Connected`, push se **tiše přeskočí** (ne chyba) — stejně jako dnešní sync přeskakuje domény bez připojení. Pokud připojení existuje, ale chybí `CALENDAR:WRITE` ve `GrantedScope`, zaznamenat jako varování (viditelné trenérovi), ne tvrdě selhat celé uložení workoutu.

## 8. Frontend dotykové body

- `frontend/src/workouts/WorkoutDetailPage.tsx` (coach editační stránka) — zobrazit stav pushe (např. malý badge "Propsáno na Garmin" / "Nepropsáno" / chyba), analogicky k dnešním sync-status badge na `IntegrationsPage.tsx`.
- **Nové nastavení pro athletea**: dnešní `ConnectorDomainPolicy`/`ConnectorMode` (`Primary/Secondary/EnrichmentOnly/FallbackOnly/Disabled`) řeší **čtecí** prioritu mezi poskytovateli, ne **zda pushovat ven**. Doporučuji **nepřetěžovat** tuto existující sémantiku (`DataDomain.PlannedWorkouts` dnes defaultuje na `Primary` pro intervals.icu, což je o čtení plánů z intervals.icu do PeakForm, ne o zápisu z PeakForm do intervals.icu — opačný směr dat). Místo toho přidat samostatný booleovský přepínač "Posílat naplánované tréninky na Garmin" do `IntegrationsPage.tsx` (per-athlete, uložený třeba přímo na `IntegrationConnection` nebo novým polem), s jasným vysvětlením prerekvizity z §2 (athlete si musí propojení ke Garminu udělat sám v intervals.icu).
- `frontend/src/calendar/WeekCalendar.tsx` — volitelně vizuální indikátor u dnů, kde je push aktivní, ale není to nutné pro MVP.

## 9. Chybové stavy k ošetření

- Token vypršel/odvolán → `Error` na connection, push se nezdaří — zobrazit trenérovi stejně jako dnešní sync chyby (`SynchronizationRuns.ErrorMessage` pattern).
- `CALENDAR:WRITE` scope chybí (athlete nereconnectoval po přidání scope) → odlišit od obecné token chyby, konkrétní hláška "Athlete nemá povolený zápis do kalendáře — požádejte ho o znovupřipojení Intervals.icu."
- Intervals.icu vrátí nečekaný JSON tvar (viz historické `FlexibleInt32Converter` případy) → nesmí shodit celé uložení workoutu, jen zalogovat a nastavit `PushError`.
- Athlete odpojí Intervals.icu poté, co má už napushované tréninky → osiřelé eventy v jeho intervals.icu kalendáři zůstanou (nemáme je jak smazat bez platného tokenu) — zmínit v `IntegrationConnectionService.DisconnectAsync`, že toto je known limitation, konzistentně s tím, jak už dnes `RevokeAsync` řeší "best-effort, nikdy neblokuje" (`:85-95`).

## 10. Navrhovaný postup implementace (v pořadí)

1. Ověřit přesnou sémantiku `WorkoutSegment.RepeatCount`/`Order` (jak se dnes reprezentuje repetice v DB) — nutné pro §5, než se píše DSL builder. **Zodpovězeno v `garmin-workout-model.md` §4–5:** `RepeatCount` opakuje jen jeden úsek; pro bloky (práce + pauza) je potřeba rozšířit model o opakovací skupiny. Tamtéž syntaxe intervals.icu, mapování typů/cílů a tichá selhání bez prahových hodnot athletea.
2. EF migrace: `PlannedWorkoutPushRecord` tabulka (§6).
3. `IPlannedWorkoutPushProvider` + implementace v `IntervalsIcuIntegrationProvider` (§7), jednotkové testy na DSL builder (§5) — **DSL builder hotový** (`IntervalsIcuWorkoutDescriptionBuilder`, viz `garmin-workout-model.md` §5) — pokrýt hlavně edge cases (rest day, bez segmentů, s repeticí, bez cíle/`Free` intenzita).
4. Zapojení do `TrainingPlanService` CRUD metod (§7), integrační test podle vzoru `SyncOrchestratorMatchingTests.cs`.
5. Frontend: badge stavu na `WorkoutDetailPage.tsx`, přepínač na `IntegrationsPage.tsx` (§8).
6. Ruční end-to-end test proti reálnému athlete účtu (stejně jako tento experiment), tentokrát s ověřením doručení na fyzická hodinky, ne jen Garmin Connect kalendář.

## 11. Otevřené otázky (rozhodnout před implementací)

- Update existujícího eventu: umí `/events/{id}` endpoint měnit `start_date_local`/`type`, nebo je nutné delete+recreate? Nebylo v tomto experimentu testováno (testovali jsme jen create+delete, ne update).
- Má push být automatický při každém uložení workoutu, nebo explicitní akce trenéra (tlačítko "Odeslat na Garmin")? Automatický je pohodlnější, ale explicitní dává trenérovi kontrolu nad tím, kdy se athlete "obtěžuje" notifikací na hodinkách.
- FTP/zóny pro `%FTP` cíle — odkud je appka vezme, pokud `WorkoutSegment.TargetPowerWatts` není vyplněné jako absolutní hodnota?
- Mělo by se pushovat i na `GarminDemoProvider`/jiné budoucí providery (Oura/WHOOP), nebo je tahle funkce čistě specifická pro Intervals.icu → Garmin cestu? (Oura/WHOOP nemají ekvivalentní "push workout to device" mechanismus podle dosavadní rešerše.)

## Zdroje

- `docs/integrations-research.md` §6 (TrainingPeaks rešerše, Garmin Training API, intervals.icu push mechanismus, live experiment)
- `docs/intervals-garmin-data-audit.md`
- `docs/integrations/canonical-data-and-deduplication-plan.md` (vzor pro `ActivitySourceRecord`/ConnectorPolicy, který §6 doporučuje kopírovat)
- Commity `517fe17`, `30c490c` (bugfix + experiment dokumentace)
