# Activity Matching — Scoring Reference

Doplněk k `docs/integrations/canonical-data-and-deduplication-plan.md`. Popisuje přesný confidence scoring algoritmus implementovaný v `TrainCoach.Application.Integrations.Matching.ActivityMatchingService`.

## Konfigurace (`ActivityMatchingOptions`, sekce `Integrations:ActivityMatching`)

| Klíč | Default | Význam |
|---|---|---|
| `AutoMergeThreshold` | 85 | skóre ≥ → automatické sloučení |
| `ManualReviewThreshold` | 60 | skóre ≥ (a < AutoMerge) → ruční review |
| `TimeWindowMinutesForFullScore` | 5 | rozdíl startu ≤ → plné body za čas |
| `TimeWindowMinutesMax` | 120 | rozdíl startu ≥ → 0 bodů za čas |
| `DurationTolerancePercentForFullScore` | 5 % | |
| `DurationToleranceMaxPercent` | 30 % | |
| `DistanceTolerancePercentForFullScore` | 2 % | |
| `DistanceToleranceMaxPercent` | 25 % | |
| `DeviceMatchBonus` | 10 | bonus, nikdy penalizace při chybějícím/neshodném zařízení |
| `NoDistanceActivityMaxScore` | 75 | strop skóre, když ani jedna strana nemá vzdálenost (posilování, no-GPS) |

Jen `AutoMergeThreshold`/`ManualReviewThreshold` jsou v `appsettings.json` — zbytek má defaulty v kódu (viz `ActivityMatchingOptions.cs`), dokumentované zde.

## Algoritmus (`ActivityMatchingService.Score`)

Váhy: sport 40, čas 30, trvání 15, vzdálenost 15 (celkem 100), + volitelný device bonus nad rámec.

1. **Sport mismatch → skóre 0, tvrdý diskvalifikátor.** Žádná jiná shoda to nepřebije (kryje "změna sport type").
2. **Čas**: lineární decay z plných 30 bodů na `TimeWindowMinutesForFullScore` k 0 na `TimeWindowMinutesMax`.
3. **Trvání**: rozdíl jako procento delší hodnoty, lineární decay 15→0.
4. **Vzdálenost**: pokud OBĚ strany mají vzdálenost, lineární decay 15→0 podle procentního rozdílu. Pokud ANI JEDNA strana vzdálenost nemá, vzdálenostní komponenta se **vyloučí** (ne vynuluje) z čitatele i jmenovatele a zbylé skóre se přeškáluje na 100 bodů — ale výsledek je stropován `NoDistanceActivityMaxScore`, aby dvě různé no-GPS aktivity ve stejný den nedosáhly automatického sloučení jen na základě sportu a času.
5. **Device bonus**: +`DeviceMatchBonus`, pokud obě strany mají neprázdný, case-insensitive shodný název zařízení. Nikdy záporný příspěvek.
6. Celkové skóre je `Math.Clamp(achieved + deviceBonus, 0, 100)`.

## Kandidátní vyhledávání (`FindOrScoreMatchAsync`)

- **Level 3** (FIT identity): pokud má příchozí aktivita `FitFileUuid`, hledá se přímá shoda v `ActivitySourceRecords` — okamžité `AutoMerge`, skóre 100, bez průchodu scoringem výše.
- **Level 4** (fingerprint): `ActivityFingerprint.Compute(sport, startUtc, durationSeconds, distanceMeters, deviceName)` — hrubě zaokrouhlený (5 min / 60 s / 100 m), jen kandidátní index. Rozšířeno o hrubší okno (±2 h, stejný sport) pro případ, že jiný poskytovatel zaokrouhluje start jinak.
- Pokud dva kandidáti dosáhnou stejného nejvyššího skóre nad review prahem, výsledek je vždy `FlagForReview` — nikdy nejednoznačné automatické sloučení (kryje "warm-up + závod ve stejném okně", "dvě krátké aktivity ve stejný den").

## Testované negativní případy (viz `ActivityMatchingServiceScoringTests.cs`)

- Odlišný sport (tvrdá diskvalifikace)
- Warm-up + závod ve stejném okně (neslučuje se)
- Ručně rozdělený běh na dvě poloviny (neslučuje se)
- Garmin aktivita přes Strava i přes intervals.icu (slučuje se — hlavní scénář, ověřeno i end-to-end přes DB v `SyncOrchestratorMatchingTests`)
- Přejmenovaná aktivita (název se do skóre nepočítá, shoda podle ostatních signálů)
- Aktivita bez GPS (stropováno, ledaže se shoduje zařízení)
- Silový trénink bez vzdálenosti
- Shoda zařízení sama o sobě nikdy nestačí k dosažení review prahu

## Doporučené další kroky (mimo rozsah tohoto MVP)

- Webhooky pro Strava a intervals.icu (obě API je podporují, dnes nevyužito — sync je jen on-connect/manuální).
- Reálné OAuth pro Oura/WHOOP (viz `docs/integrations/oura-whoop-activation.md`).
- Reálné napojení na Garmin Health API (dnes jen přes intervals.icu, scraping Garmin Connect je zakázáno).
- `SyncTrigger.Scheduled` periodický/cron sync loop — enum hodnota existuje, nic ji dnes nekonstruuje.
- Dedup kontrola i pro manuálně zapsané aktivity.
- Per-athlete override matcher prahů (analogicky k `ConnectorDomainPolicy`).
- Rozšíření `MeasurementContext`/unikátního klíče wellness tabulek pro víc než jeden kontext HRV za den ze stejného zdroje.
