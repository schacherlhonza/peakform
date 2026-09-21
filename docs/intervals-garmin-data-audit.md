# Audit: Intervals.icu integrace a data z Garminu — co skutečně máme

Stav: audit provedeno 2026-09-21, na reálném dev prostředí (`peakform-backend-1`, `peakform-postgres-1`, běžící 3 dny), proti reálně připojenému a synchronizujícímu Intervals.icu účtu jednoho atleta. Žádné změny kódu, žádný resync, žádné mazání. Nikde v tomto dokumentu nejsou uvedeny tokeny, connection stringy ani syrové zdravotní hodnoty nad rámec toho, co je nutné pro klasifikaci pole.

## 1. Executive summary

Intervals.icu integrace **funguje** a reálně synchronizuje aktivity i wellness data z Garmin zařízení (Garmin Forerunner 965) atleta, který má připojený jak Strava (2026-09-16), tak Intervals.icu (2026-09-18, poslední sync dnes 2026-09-21 06:59). Dedup mezi Strava a Intervals.icu **prokazatelně funguje** — nalezena jedna reálná aktivita se dvěma zdrojovými záznamy (Strava + Intervals.icu), sloučená do jedné kanonické aktivity (`AutoMerged`), 0 nevyřešených duplicitních kandidátů.

Klíčová zjištění:

1. **Tréninková data (aktivity) jsou pokryta velmi dobře** — distance, čas, tep, výkon, kadence, elevation, training load, intensity, decoupling (pole existuje, ale u krátkých/intervalových aktivit nevyplněné), počet laps a detekce intervalů jsou **k dispozici v syrové API odpovědi**, ale **naše DTO mapuje jen zlomek** (viz §6). Streamy (GPS, HR, pace, power, elevation, kadence) se stahují a zobrazují, ale ne vždy dostupná pole jako `grade_smooth`, `respiration` (u některých aktivit skutečně přítomno v datech zařízení!) se nestahují vůbec.
2. **Regenerace/wellness data jsou zásadně slabší, než dokumentace Intervals.icu slibuje.** Reálně synchronizovaná pole za posledních 7 dní: klidová TF (denně), HRV RMSSD (4/7 dní), spánek — jen celková délka a sleep score (4/7 dní), kroky, VO2max (občas), CTL/ATL/ramp rate (26 dní zpětně). **Readiness, stress score, mood, motivation, soreness, fatigue, SpO2, injury flag jsou v databázi u tohoto atleta VŽDY `NULL`** — ne proto, že by je appka nemapovala (mapovací kód existuje a je správný), ale nejpravděpodobněji proto, že **Garmin sám o sobě tato pole do Intervals.icu nepropisuje** (readiness/stress/mood/motivation/soreness jsou u Intervals.icu typicky plněna ze zdrojů jako Oura/WHOOP/HRV4Training, které tento atlet nemá připojené — potvrzeno i vlastní dřívější rešerší v repozitáři). Toto se nepodařilo ověřit proti syrové API odpovědi (Intervals.icu neukládá raw payload pro wellness, jen pro aktivity), takže zůstává `UNKNOWN_NOT_VERIFIED` s vysokou pravděpodobností, ne jistotou.
3. **Kritický dopad na produkt:** `ReadinessCard` (ranní readiness na dashboardu) čte přímo `RecoveryMetric.ReadinessScore` bez jakéhokoliv vlastního výpočtu (`useAthleteDashboardData.ts:92`, `score: latestRecovery?.readinessScore ?? null`). Protože je toto pole u reálného Garmin-only atleta vždy `null`, **readiness karta na dashboardu dnes u reálných dat nezobrazuje žádné skóre** — jen HR/HRV/sleep metriky ve `MetricStrip`. To je zásadní zjištění pro rozhodnutí o Garmin API (viz §9).
4. **Sleep stages (deep/REM/light/awake), sleep efficiency, čas usnutí/probuzení, Body Battery (ranní/večerní/průběh), průběh stresu během dne, denní kalorie (mimo aktivitu), tělesný tuk/composition, minimální SpO2 a dechová frekvence jako denní metrika nejsou dostupné přes Intervals.icu** vůbec — nejsou v našem DTO, nejsou v dřívější rešerši Intervals.icu wellness pole, a `DeepSleepMinutes`/`RemSleepMinutes` sloupce v DB existují, ale nemají žádnou mapovací cestu z Intervals.icu (rezervováno pro budoucí zdroje typu Oura/WHOOP).
5. **Několik polí se reálně ukládá, ale v aplikaci se nikdy nezobrazuje**: `SleepScore`, `HrvSdnnMs`, `AvgSleepingHeartRateBpm`, `MoodScore`, `SorenessScore`, `FatigueScore`, `MotivationScore`, `HasInjurySignal`, celý `TrainingFeedbackDto` dataset, `GeneratedReportDto.deliveryChannel/deliveryStatus`. Toto je čistě frontendová mezera, ne integrace — data jsou v DB.
6. **Nalezeny 2 historické neúspěšné sync běhy** (2026-09-18, oba `Failed`) se stejnou chybou deserializace (`JSON value could not be converted to Nullable<Int32>`) — přesně ten typ chyby, který je zdokumentovaný v kódu jako důvod přidání `FlexibleInt32Converter` (Intervals.icu vrátilo `sleepScore` jako `79.0` místo `79`). Novější běhy (2026-09-18 07:27 a dál) už uspěly — chyba je vyřešená, ale ukazuje na obecné riziko: Intervals.icu API vrací nekonzistentní JSON typy a naše DTO to musí ošetřovat defenzivně i pro další pole.
7. **Doporučení: Varianta B** — Intervals.icu zůstává hlavním zdrojem aktivit, přímé Garmin Health API by mělo smysl přidat **jen pro regeneraci/wellness**, a i tak jen po prověření, že Garmin Health API skutečně poskytuje pole, která Intervals.icu nedává (Training Readiness, Body Battery, sleep stages) — což se v této session nepodařilo ověřit z oficiální dokumentace bez schváleného přístupu (viz §8). Detailní odůvodnění v §9.

## 2. Popis současného datového toku

```
Garmin zařízení (Forerunner 965)
   → Garmin Connect (mimo kontrolu appky)
   → Intervals.icu (agregátor, athlete si sám připojil Garmin uvnitř Intervals.icu)
   → OAuth2 Bearer token (žádný refresh token — Intervals.icu ho nevydává)
   → IntervalsIcuIntegrationProvider (HTTP klient, backend/src/TrainCoach.Integrations/IntervalsIcu/)
   → SyncOrchestrator.RunAsync (backend/src/TrainCoach.Application/Integrations/SyncOrchestrator.cs)
       ├─ aktivity → ActivityMatchingService (dedup/matching cascade) → CompletedActivities + ActivitySourceRecords + ActivityMetrics
       └─ wellness → per-tabulka upsert (Date, Source) → HrvMeasurements / RecoveryMetrics / SleepRecords / WeightMeasurements / TrainingLoadSnapshots
   → REST API (/api/athletes/{id}/...)
   → React Query hooks (orval generated)
   → UI komponenty (WellnessTrendsPage, ReadinessCard, ActivityDetailPage, ...)
```

Sync se spouští **jen** při prvním připojení (`OnConnect`) a manuálním tlačítku ("Sync Now") — **žádný periodický/cron sync neexistuje** (`SyncTrigger.Scheduled` je definovaná enum hodnota, ale nic ji nikdy nevytváří). Pokud atlet nezmáčkne tlačítko, data stárnou bez upozornění.

## 3. Používané Intervals.icu endpointy

Vše v `backend/src/TrainCoach.Integrations/IntervalsIcu/IntervalsIcuIntegrationProvider.cs`:

| Endpoint | Metoda | Účel | Použito v kódu? |
|---|---|---|---|
| `https://intervals.icu/oauth/authorize` | GET (redirect) | OAuth autorizace | Ano (řádek 33, 50–58) |
| `https://intervals.icu/api/oauth/token` | POST | výměna kódu za token | Ano (40, 66–73) — pozn.: jediný endpoint bez `/v1/` segmentu, ověřeno živě po 2 chybných pokusech |
| `{ApiBaseUrl}/athlete/0/activities?oldest=...&newest=...` | GET | seznam aktivit | Ano (98–99) |
| `{ApiBaseUrl}/activity/{externalId}/streams?types=time,heartrate,watts,cadence,distance,altitude,velocity_smooth,grade_smooth` | GET | senzorové streamy | Ano (192) — **pevný seznam typů, nerozšiřuje se dynamicky podle `stream_types` z aktivity** |
| `{ApiBaseUrl}/athlete/0/wellness?oldest=...&newest=...` | GET | wellness záznamy | Ano (242–243) |
| `{ApiBaseUrl}/disconnect-app` | DELETE | revokace tokenu | Ano (291) |

**Endpointy zdokumentované, ale nikdy nevolané** (jen v `docs/integrations-research.md`): `GET/POST/PUT/DELETE /api/v1/athlete/{id}/events` (kalendář/plánované tréninky, obousměrně) — žádný kód pod `IntervalsIcu` na ně nesahá. Rovněž `GET /activity/{id}?intervals=true` (detailní intervalová analýza) se nikdy nevolá samostatně — ale zajímavě, **syrová odpověď ze základního `/activities` endpointu už obsahuje pole `interval_summary`** (viz §6), takže tato analýza je částečně dostupná i bez extra volání.

Athlete ID je v URL vždy `0` (= "aktuálně autentizovaný atlet" pro Bearer token), ne skutečné Intervals.icu ID.

## 4. Matice dostupnosti dat

Legenda: `AVAILABLE_IN_API` (potvrzeno v syrové odpovědi nebo dřívější rešerši), `MAPPED_AND_STORED` (DTO pole existuje a mapuje se do DB), `STORED_NOT_DISPLAYED` (v DB je, na UI ne), `AVAILABLE_NOT_MAPPED` (API pole existuje, náš DTO ho nezná), `NOT_AVAILABLE` (Intervals.icu API ho dle dostupných důkazů neposílá), `UNKNOWN_NOT_VERIFIED` (nepodařilo se ověřit ani jedním směrem).

### 4.1 Regenerace a zdraví

| Pole | Klasifikace | Poznámka |
|---|---|---|
| HRV (RMSSD) | `MAPPED_AND_STORED` + zobrazeno | `wellness.hrv` → `HrvMeasurement.RmssdMs`. Reálná data 4/7 dní (39–54 ms). Zobrazeno ve `WellnessTrendsPage` sparkline a `ReadinessCard`. |
| HRV SDNN | `MAPPED_AND_STORED`, ale **prázdné** | `wellness.hrvSDNN` → `HrvMeasurement.SdnnMs`. Mapovací kód existuje a je správný, ale **ve všech 4 reálných záznamech je `NULL`**. Nelze ověřit, zda Intervals.icu pole vůbec posílá pro toto zařízení (raw wellness payload se neukládá) → `UNKNOWN_NOT_VERIFIED` s poznámkou, že pipeline je funkční. Nikdy nezobrazeno na UI. |
| Klidová TF | `MAPPED_AND_STORED` + zobrazeno | `restingHR` → `RecoveryMetric.RestingHeartRateBpm`. Reálná data všech 7 dní (48–53 bpm). Zobrazeno v `ReadinessCard`. |
| Průměrná TF během spánku | `MAPPED_AND_STORED`, ale **prázdné** | `avgSleepingHR` → `SleepRecord.AvgSleepingHeartRateBpm`. Vždy `NULL` ve 4 reálných záznamech. `UNKNOWN_NOT_VERIFIED` proč. Nikdy nezobrazeno. |
| Délka spánku | `MAPPED_AND_STORED` + zobrazeno | `sleepSecs` → `SleepRecord.DurationMinutes`. Reálná data 4/7 dní (429–593 min). |
| Sleep score | `STORED_NOT_DISPLAYED` | `sleepScore` → `SleepRecord.SleepScore`. Reálná data 4/7 dní (71–82). Nikdy nerenderováno na UI (potvrzeno grepem přes celý frontend). |
| Sleep quality | `UNKNOWN_NOT_VERIFIED` (pravděpodobně `AVAILABLE_NOT_MAPPED`) | Zmíněno ve starší rešerši (`integrations-research.md`) jako dokumentované pole `sleepQuality`, ale **v našem DTO (`IntervalsIcuWellnessEntry`) toto pole vůbec neexistuje** — nikdy se nezkoušelo deserializovat. Nepodařilo se ověřit proti živé odpovědi. |
| Čas usnutí/probuzení | `NOT_AVAILABLE` | Není v DTO, není v dřívější rešerši Intervals.icu wellness polí, není v DB schématu. Intervals.icu wellness endpoint se zdá poskytovat jen `sleepSecs` (délku), ne konkrétní časy. |
| Deep sleep | `UNKNOWN_NOT_VERIFIED` (spíš `NOT_AVAILABLE` via Intervals.icu) | DB sloupec `SleepRecord.DeepSleepMinutes` existuje, ale **žádné DTO pole ho neplní** — potvrzeno v kódu, sloupec je připraven pro budoucí zdroje (Oura/WHOOP), ne pro Intervals.icu/Garmin dnes. |
| REM sleep | stejně jako deep sleep | `SleepRecord.RemSleepMinutes` — stejná situace. |
| Light sleep | `NOT_AVAILABLE` | Žádný DB sloupec, žádné DTO pole, není v žádné rešerši. |
| Awake time | `NOT_AVAILABLE` | Stejně. |
| Sleep efficiency | `NOT_AVAILABLE` | Stejně. |
| Průměrné SpO₂ | `MAPPED_AND_STORED` + zobrazeno, ale **prázdné** | `spO2` → `RecoveryMetric.SpO2Percent`. Sparkline existuje ve `WellnessTrendsPage` (řádek 152/200), ale ve všech 5 reálných záznamech je `NULL`. Garmin Forerunner 965 umí pulse ox, ale funkce může být na zařízení vypnutá (šetří baterii) — neověřeno. |
| Minimální SpO₂ | `NOT_AVAILABLE` | Intervals.icu wellness pole je jen jedna hodnota `spO2` (průměr/jedna hodnota za den), ne min/max rozpad. |
| Dechová frekvence (denní) | `NOT_AVAILABLE` jako wellness pole | Není v DTO, není v rešerši jako denní metrika. **Zajímavá výjimka:** `respiration` se objevilo jako **per-activity stream typ** u jedné reálné aktivity (`stream_types` obsahovalo `"respiration"`) — to je dechová frekvence *během konkrétního tréninku*, ne denní klidová hodnota, a navíc se nestahuje (viz §6, streamy se stahují jen s pevným seznamem typů, který `respiration` neobsahuje). |
| Stress score | `MAPPED_AND_STORED`, ale **prázdné** | `stress` → `RecoveryMetric.StressScore`. Vždy `NULL`. Nikdy nezobrazeno. |
| Průběh stresu během dne | `NOT_AVAILABLE` | Intervals.icu wellness endpoint dává jen jedno denní číslo, ne timeline. |
| Body Battery (celkově) | `NOT_AVAILABLE` | Nenalezeno v DTO, v DB schématu, ani v dřívější rešerši Intervals.icu wellness polí. Je to Garmin-proprietární metrika, kterou Intervals.icu podle všech dostupných důkazů nepropisuje. |
| Ranní/večerní Body Battery | `NOT_AVAILABLE` | Stejně. |
| Průběh Body Battery | `NOT_AVAILABLE` | Stejně. |
| Readiness | `MAPPED_AND_STORED`, ale **prázdné — s produktovým dopadem** | `readiness` → `RecoveryMetric.ReadinessScore`. Vždy `NULL` u tohoto atleta. Dřívější rešerše v repozitáři výslovně uvádí: "`readiness` pole je typicky populováno ze zdrojových služeb, které readiness/recovery samy počítají (Oura, HRV4Training...) — Intervals.icu ho nepočítá vlastním algoritmem." Tento atlet má jen Garmin, žádný Oura/WHOOP/HRV4Training → vysoce pravděpodobné vysvětlení, proč je pole prázdné (nejde o mapovací chybu). **`ReadinessCard.score` na dashboardu čte přímo toto pole bez vlastního výpočtu** (`useAthleteDashboardData.ts:92`) → u reálných dat dnes efektivně nefunkční. |
| Recovery time (Garmin) | `NOT_AVAILABLE` | Není v DTO, není v rešerši. Garmin-proprietární metrika, nepotvrzena přes Intervals.icu. |
| Training readiness (Garmin proprietární) | `NOT_AVAILABLE` (odlišné od Intervals.icu `readiness`!) | Nezaměňovat s obecným polem `readiness` výše — Garmin Connect má vlastní "Training Readiness" skóre, které nebylo v žádné dostupné dokumentaci potvrzeno jako propisované do Intervals.icu ani do Garmin Health API (viz §8). |
| Kroky | `MAPPED_AND_STORED` + zobrazeno | `steps` → `RecoveryMetric.Steps`. Reálná data všech 5 dní (72–36934). Zobrazeno ve `WellnessTrendsPage`. |
| Denní kalorie (mimo aktivitu) | `NOT_AVAILABLE` jako wellness pole | Intervals.icu DTO ani rešerše nemá "denní kalorie" jako wellness pole — kalorie existují jen na úrovni aktivity (`CompletedActivity.Calories`). |
| Body weight | `MAPPED_AND_STORED`, ale **0 záznamů** | `weight` → `WeightMeasurement.WeightKg`. Unique index existuje, mapování existuje, ale **v DB je 0 řádků** pro tohoto atleta za celou historii — buď atlet nemá připojenou chytrou váhu k Garminu/Intervals.icu, nebo Intervals.icu pole u tohoto účtu nikdy neposílá. `UNKNOWN_NOT_VERIFIED`, ale prakticky `NOT_AVAILABLE` pro tento účet. |
| Body fat / composition | `NOT_AVAILABLE` přes Intervals.icu | Žádné DB pole, žádné DTO pole, není v rešerši Intervals.icu wellness polí. Garmin Health API naopak **výslovně uvádí "body composition"** jako vlastní kategorii (§8) — potenciální reálný rozdíl. |

### 4.2 Tréninková data (aktivity)

| Pole | Klasifikace | Poznámka |
|---|---|---|
| Externí ID aktivity | `MAPPED_AND_STORED` | `id` → `ActivitySourceRecord.ExternalId`, unique index `(Source, ExternalId)`. |
| Zdroj aktivity | `MAPPED_AND_STORED` + zobrazeno | `source`/`device_name` → `ActivitySourceRecord.Source`/`DeviceName`. Zdroj zobrazen v `ActivityDetailPage`. |
| FIT/TCX/GPX dostupnost | `AVAILABLE_NOT_MAPPED` (částečně) | Intervals.icu umožňuje upload/download FIT/TCX/GPX (dle rešerše), ale **žádný endpoint pro stažení souboru se v kódu nevolá** a `FitFileUuid` se nikdy nepopulovává živým adaptérem (jen syntetickou fixture v testech). |
| Čas začátku | `MAPPED_AND_STORED` + zobrazeno | `start_date`/`start_date_local` → `CompletedActivity.StartedAtUtc`. |
| Typ aktivity | `MAPPED_AND_STORED` + zobrazeno | `type` → `Sport` enum. |
| Vzdálenost | `MAPPED_AND_STORED` + zobrazeno | `distance` → `DistanceMeters`. |
| Moving time | `MAPPED_AND_STORED` + zobrazeno | `moving_time` → `DurationSeconds`. |
| Elapsed time | `MAPPED_AND_STORED` + zobrazeno | `elapsed_time` → `ActivityMetrics` (generický bucket), zobrazeno v `ActivityDetailPage`. |
| Převýšení (gain) | `MAPPED_AND_STORED` + zobrazeno | `total_elevation_gain` → `ElevationGainMeters`. |
| Převýšení (ztráta) | `AVAILABLE_NOT_MAPPED` | `total_elevation_loss` **je v syrové odpovědi** (potvrzeno u 6/8 reálných aktivit), ale žádné DTO pole ho nezachytává. |
| Průměrný/max tep | `MAPPED_AND_STORED` + zobrazeno | `average_heartrate`/`max_heartrate` → oba sloupce, zobrazeno v `ActivityDetailPage` a stream chart. |
| Tepové zóny (time-in-zone per aktivita) | `AVAILABLE_NOT_MAPPED` | `icu_hr_zone_times` **je v syrové odpovědi** (potvrzeno u všech 8 aktivit), ale nikde se nemapuje. `HeartRateZones` tabulka v DB obsahuje jen **statickou definici zón atleta** (min/max bpm), ne per-aktivitu rozpad času v zóně. |
| Tempo/rychlost | `MAPPED_AND_STORED` + zobrazeno | Tempo je **dopočítané** (`durationSeconds*1000/distanceMeters`), ne přímo z pole API. Zobrazeno jako pace chart. |
| Výkon | `MAPPED_AND_STORED` + zobrazeno | `icu_average_watts` → `AveragePowerWatts`. Zobrazeno. |
| Kadence | `MAPPED_AND_STORED` (jen stream) + zobrazeno | Souhrnná `average_cadence` **se nemapuje** (`AVAILABLE_NOT_MAPPED`), ale stream `cadence` se stahuje a zobrazuje. |
| Training load | `MAPPED_AND_STORED` + zobrazeno | `icu_training_load` → `ActivityMetrics`, zobrazeno. |
| Intensity | `MAPPED_AND_STORED` + zobrazeno | `icu_intensity` → `ActivityMetrics`, zobrazeno. |
| Decoupling | `AVAILABLE_IN_API`, **nikdy vyplněné v datech**, `NOT_MAPPED` | Pole `decoupling` **existuje v syrové odpovědi u všech 8 reálných aktivit**, ale u všech 8 je `null` (pravděpodobně proto, že decoupling vyžaduje delší steady-state úsek, který krátké/intervalové tréninky tohoto atleta nemají). Navíc žádné DTO pole ho nezachytává, i kdyby bylo vyplněné. |
| Detekované intervaly | `AVAILABLE_NOT_MAPPED` | Pole `interval_summary` **je v syrové odpovědi u všech 8 aktivit**, ale nemapuje se nikam. |
| Laps | `AVAILABLE_NOT_MAPPED` | Pole `icu_lap_count` (počet kol) **je v syrové odpovědi u všech 8 aktivit**, ale nemapuje se. Detailní lap-by-lap rozpad by vyžadoval `/activity/{id}?intervals=true`, který se nevolá vůbec. `WorkoutSegments` tabulka v DB je pro **plánované** tréninky, ne pro laps dokončené aktivity — jiný koncept. |
| GPS stream | `MAPPED_AND_STORED` + zobrazeno | `latlng` stream se stahuje (`ActivitySourceRecord.RawStartLatitude/Longitude` pro start bod), plná trasa v `ActivityStreamsDto`, ale **UI ji nevykresluje jako mapu** — jen HR/pace/elevation/cadence/power grafy (`ActivityDetailPage`). |
| HR stream | `MAPPED_AND_STORED` + zobrazeno | |
| Pace/speed stream | `MAPPED_AND_STORED` + zobrazeno | `velocity_smooth` → pace chart (invertovaná osa). |
| Power stream | `MAPPED_AND_STORED` + zobrazeno | `watts` stream. |
| Nadmořská výška (stream) | `MAPPED_AND_STORED` + zobrazeno | `altitude` stream → elevation area chart. |
| Teplota | `AVAILABLE_IN_API` (pole existuje), **prázdné u všech testovaných aktivit** | `average_temp`/`min_temp`/`max_temp` jsou v syrové odpovědi (potvrzeno u 8/8 aktivit — u žádné vyplněné). Tato pole u Intervals.icu odpovídají spíš **předpovědi počasí** (`has_weather`) než senzoru zařízení — u aktivit tohoto atleta nebyla nikdy vyplněná. Žádné DTO pole je navíc nezachytává. |
| Garmin training effect | `NOT_AVAILABLE` (v tomto vzorku) | **Žádné pole v syrové odpovědi žádné z 8 aktivit neobsahuje** nic jako `training_effect`/`aerobic_effect`/`anaerobic_effect` — prohledáno explicitně. Buď Intervals.icu tuto Garmin-proprietární metriku nepropisuje vůbec, nebo ji Garmin Forerunner 965 do Intervals.icu neposílá. Nelze s jistotou odlišit bez přímého přístupu ke Garmin Connect. |

## 5. Výsledky kontroly posledních 7 dní (2026-09-15 až 2026-09-21)

Zdroj: reálná data atleta `a0a44929-…` (jediný atlet s aktivním Intervals.icu připojením a reálnou synchronizací), přímý dotaz do produkční dev databáze.

| Tabulka | Dny s daty (z 7) | Poznámka |
|---|---|---|
| `HrvMeasurements` | 4 (09-18 až 09-21) | Chybí 09-15 až 09-17 — **před připojením Intervals.icu** (09-18 07:20), ne chyba synchronizace. |
| `RecoveryMetrics` (RHR, steps) | 5 (09-17 až 09-21) | RHR a steps dostupné o den dřív než HRV/sleep — buď Intervals.icu mělo data od 09-17, nebo šlo o částečný wellness záznam. |
| `SleepRecords` | 4 (09-18 až 09-21) | |
| `WeightMeasurements` | 0 | Žádný záznam v celé historii atleta. |
| `TrainingLoadSnapshots` (CTL/ATL/ramp) | 26 dní zpětně (až 08-27) | CTL/ATL je kumulativní fitness model počítaný z historie aktivit, Intervals.icu ho dopočítá zpětně i za dny před samotným připojením integrace. |
| `CompletedActivities` (tento atlet) | 8 aktivit za období 09-15 až 09-21 | Žádné duplicity, žádní pending `DuplicateCandidate`. |

**Chybějící dny nejsou dané aplikační chybou** — vysvětlují se datem připojení Intervals.icu (09-18) a tím, že HRV/sleep vyžadují, aby zařízení bylo nošené celou noc (na rozdíl od RHR/steps, které Garmin měří kontinuálně).

**Sync historie (`SynchronizationRuns`) pro toto připojení:**

| Trigger | Status | Fetched/Created | Chyba |
|---|---|---|---|
| OnConnect | Failed | 1 fetched, 0 created | `JSON value could not be converted to Nullable<Int32>` |
| Manual | Failed | 1 fetched, 0 created | stejná chyba |
| Manual | Succeeded | 3 fetched, 0 created, 1 skip-dup | — |
| Manual | Succeeded | 23 fetched, 1 created | — |
| Manual | Succeeded | 11 fetched, 7 created | dnešní sync |

Dva neúspěšné běhy z 2026-09-18 dopoledne odpovídají přesně zdokumentovanému bugu (`sleepScore` jako `79.0` místo `79`), který byl mezitím opraven (`FlexibleInt32Converter`). Od té doby žádný neúspěšný běh.

## 6. Nezmapovaná nebo zahazovaná pole (nalezená v syrové API odpovědi)

Zdroj: `ActivitySourceRecords.RawPayloadJson` (Source=6, Intervals.icu), 8 reálných aktivit — syrová odpověď má **cca 150 top-level polí** oproti ~20 poli v `IntervalsIcuActivity` DTO. Nejvýznamnější nezmapovaná pole s potvrzenou přítomností v datech:

- `decoupling` — aerobní decoupling %, přítomné jako pole, prázdné u vzorku (krátké aktivity)
- `icu_lap_count` — počet kol/laps
- `interval_summary` — detekované intervaly (souhrn)
- `icu_hr_zone_times` — čas v jednotlivých tepových zónách per aktivita
- `total_elevation_loss` — ztráta výšky (máme jen gain)
- `average_temp`/`min_temp`/`max_temp` — teplota (prázdné u vzorku)
- `average_cadence` — souhrnná kadence (stream ano, souhrn ne)
- `icu_efficiency_factor`, `icu_variability_index`, `polarization_index` — pokročilé tréninkové metriky
- Running dynamics: `average_stance_time`, `average_vertical_oscillation`, `average_vertical_ratio`, `average_step_length` — dostupné u aktivit s Garmin Running Dynamics Pod/hodinkami
- `respiration` — objevilo se jako **stream typ** u jedné aktivity (per-vteřinová dechová frekvence během tréninku), ale streamy se stahují s pevným seznamem typů, který `respiration` neobsahuje

Toto jsou **konkrétní, reálně ověřené** rozdíly (ne teoretické) — pole jsou v datech přítomná, kód je nikdy nečte.

**Uložené, ale nezobrazované na frontendu** (data v DB existují, žádná UI komponenta je nerenderuje — potvrzeno grepem):
`RecoveryMetric.MoodScore/SorenessScore/FatigueScore/MotivationScore/HasInjurySignal`, `SleepRecord.SleepScore/DeepSleepMinutes/RemSleepMinutes/AvgSleepingHeartRateBpm`, `HrvMeasurement.SdnnMs`, `ActivityStreamsDto.gradePercent`, `ActivityMetricType.WorkJoules/WeightedAveragePowerWatts`, celý `TrainingFeedbackDto` (hook definován, nikdy nezavolán žádnou stránkou), `GeneratedReportDto.deliveryChannel/deliveryStatus/generatedAtUtc`, `PainOrHealthFlagDto.relatedCheckInId/resolvedOnDate`, `CompletedActivityDto.plannedWorkoutId` (nikdy neproklikáno zpět na plán).

## 7. Duplicity a rizika synchronizace

- **Nalezena a ověřena 1 skutečná cross-provider duplicita, správně sloučená**: aktivita z 2026-09-17 08:59:30 UTC má 2 zdrojové záznamy (`Source=3` Strava, `Source=6` IntervalsIcu), sloučené do jedné `CompletedActivity` s `MatchStatus=AutoMerged`. Matching cascade (fingerprint + confidence scoring) funguje podle očekávání.
- **0 nevyřešených `DuplicateCandidate` záznamů** pro tohoto atleta.
- **Žádná jiná duplicita nenalezena** mezi 8 aktivitami tohoto atleta za posledních 7 dní.
- **Riziko nalezené v kódu (ne v datech)**: `RecoveryMetrics` a `SleepRecords` **nemají DB-level unique index** na `(AthleteUserId, Date, Source)`, na rozdíl od `HrvMeasurements`/`WeightMeasurements`/`TrainingLoadSnapshots`, které ho mají. Dedup pro tyto dvě tabulky spoléhá jen na aplikační `FirstOrDefaultAsync` kontrolu před insertem — teoreticky zranitelné vůči souběžným sync běhům (race condition), i když v praxi sync běhy pro jednoho atleta neběží paralelně.
- **Kanonická identita aktivity** je stabilní napříč poskytovateli díky `ActivitySourceRecord` (many:1 k `CompletedActivity`) + fingerprint/confidence matching — potvrzeno reálným příkladem výše, ne jen teoreticky.
- **Historické parsovací chyby** (§5) ukazují, že Intervals.icu API vrací nekonzistentní JSON typy (float místo int) — riziko, že podobný problém může nastat i u jiných, dosud nezasažených polí, pokud/až se rozšíří mapování (viz §9 doporučení).

## 8. Rozdíly proti Garmin Health API

Na základě dřívější rešerše v repozitáři (`docs/integrations-research.md`, ověřeno 2026-09-15, ne v této session znovu ověřováno proti živému Garmin API — přístup vyžaduje schválení, které appka nemá):

| Garmin Health API (dle oficiálního přehledu) | Máme přes Intervals.icu? | Poznámka |
|---|---|---|
| Heart rate (obecně) | Ano (RHR, avg/max u aktivit) | |
| Sleep | Částečně | Jen duration + score, ne stages/efficiency/times |
| Stress | Ne (u tohoto atleta) | Pole existuje v Intervals.icu DTO, ale prázdné |
| Pulse Ox (SpO2) | Ne (u tohoto atleta) | Stejně — pole existuje, prázdné |
| Body Battery | **Ne, vůbec** | Intervals.icu tuto metriku nezdá se propisovat |
| Respiration | Ne jako denní metrika | Objevilo se jen jako per-aktivitní stream u 1/8 aktivit |
| Steps | Ano | Plně funkční |
| Calories | Jen per-aktivitu | Ne jako denní celková hodnota |
| Body composition | **Ne** | Garmin Health API ho výslovně zmiňuje jako kategorii, Intervals.icu ho v žádném poli, které jsme našli, nemá |
| Detailed epoch summaries | **Neověřeno** | Nebylo možné ověřit z veřejné dokumentace bez schváleného přístupu do Garmin developer portálu |
| Beat-to-beat intervals | **Neověřeno** | Stejně — zmíněno jako "Enhanced Beat-To-Beat Interval" v přehledu, přesný obsah/formát nepotvrzen |
| Training Readiness / Endurance Score / jiná proprietární skóre | **Neověřeno, nepotvrzeno** | Nejsou explicitně jmenované na veřejných přehledových stránkách Garmin Health API dle dřívější rešerše — nelze předpokládat, že by přímé API tato skóre poskytovalo, i kdyby Garmin Connect (spotřebitelská appka) je zobrazovala. |

**Důležité metodologické upozornění přebírané z rešerše**: Garmin Connect Developer Program je oficiálně "only for business use" s formálním schvalovacím procesem — přístup pro nezávislý/malý projekt jako PeakForm je nejistý a nebyl (a nemohl být) v tomto auditu ověřen živě.

## 9. Doporučení: Varianta B

**Intervals.icu zůstává hlavním zdrojem aktivit. Garmin Health API by mělo smysl přidat jen pro regeneraci/wellness — ale až po prověření skutečné dostupnosti a nákladů přístupu, ne automaticky.**

Odůvodnění:

- **Aktivity jsou přes Intervals.icu pokryté velmi dobře** už dnes (a ještě lépe po doplnění nezmapovaných polí z §6) — není důvod duplikovat tuto cestu přes Garmin API.
- **Regenerace/wellness má reálnou mezeru** — ale ne tam, kde by ji čekal. Klidová TF, HRV RMSSD, délka spánku, kroky **fungují**. Chybí ale právě to, co je pro ranní/večerní report a analýzu spánku klíčové: readiness skóre (kvůli chybějícímu Oura/WHOOP/HRV4Training zdroji, ne kvůli Garmin API), sleep stages, Body Battery.
- **Než investovat do Garmin Health API, je nutné nejdřív ověřit dvě věci, které tento audit nemohl ověřit**: (1) zda PeakForm jako malý/nezávislý projekt vůbec dostane schválení do Garmin Connect Developer Program ("only for business use"), a (2) zda Health API skutečně obsahuje Body Battery/sleep stages/Training Readiness v podobě použitelné pro tento produkt — přehledové stránky to naznačují u Body Battery a sleep, ale Training Readiness/Endurance Score nejsou explicitně potvrzené.
- **Levnější alternativa k části mezery**: Intervals.icu samo o sobě umí agregovat i **Oura a WHOOP** (dle rešerše) — pokud by athlete/PeakForm chtěl reálné `readiness` skóre, může to přijít přes Oura/WHOOP připojené *do* Intervals.icu, bez nutnosti řešit Garmin Health API vůbec. To by měl být první krok k prověření, ne přímé Garmin napojení.
- Varianta A (Intervals.icu plně stačí) není doporučena, protože readiness/sleep-stage mezera je reálná a dotýká se přesně těch use-case, které uživatel označil jako klíčové (ranní readiness report, analýza nízkého REM).
- Varianta C (přímé Garmin API i pro aktivity) je zbytečná — aktivity jsou už dnes pokryté a Garmin by přinesl jen redundanci s vyšším integračním rizikem (business-only schvalování).

## 10. Konkrétní další kroky (seřazené podle priority)

1. **Doplnit mapování `decoupling`, `icu_lap_count`, `interval_summary`, `icu_hr_zone_times`, `total_elevation_loss` do `IntervalsIcuActivity` DTO** — data už dorazí v každé synchronizaci, jen se zahazují. Nízké riziko, vysoká hodnota (laps a intervaly jsou běžně žádaná data v tréninkové appce).
2. **Rozšířit seznam requestovaných stream typů** o `grade_smooth` (chybí v seznamu, i když je v kódu zmíněný jako podporovaný) a zvážit `respiration` tam, kde je dostupný — potvrzeno, že se u některých aktivit reálně vyskytuje.
3. **Zobrazit již uložená, ale skrytá pole na frontendu**: `SleepScore`, mood/soreness/fatigue/motivation (subjektivní wellness), `HasInjurySignal` — nulové riziko (žádné backend změny), okamžitá hodnota pro uživatele.
4. **Prověřit, proč `WeightMeasurements` má 0 záznamů** — zkontrolovat, zda atlet má na Intervals.icu připojenou chytrou váhu; pokud ne, nejde o bug.
5. **Přidat DB-level unique index `(AthleteUserId, Date, Source)` na `RecoveryMetrics` a `SleepRecords`**, po vzoru ostatních wellness tabulek — uzavře teoretické race-condition riziko duplicit.
6. **Prověřit dostupnost Oura/WHOOP přes Intervals.icu jako cestu k readiness skóre**, než se investuje do přímého Garmin API — architektura (`ConnectorDomainPolicy`) už je na to připravená, jen chybí aktivace OAuth (`docs/integrations/oura-whoop-activation.md`).
7. **Teprve poté** zvážit žádost o Garmin Connect Developer Program (pokud Oura/WHOOP cesta nestačí) — s vědomím byznysového schvalovacího rizika popsaného v §8.
8. **Zavést periodický (scheduled) sync** — dnes žádný neexistuje, data stárnou bez upozornění, pokud atlet nezmáčkne "Sync Now". Netýká se přímo Garmin otázky, ale je to prerekvizita pro spolehlivý denní readiness report.

## 11. Zkontrolované soubory, tabulky a endpointy

**Backend kód:**
- `backend/src/TrainCoach.Integrations/IntervalsIcu/IntervalsIcuIntegrationProvider.cs`
- `backend/src/TrainCoach.Integrations/IntervalsIcu/IntervalsIcuApiDtos.cs`
- `backend/src/TrainCoach.Integrations/IntervalsIcu/IntervalsIcuOptions.cs`
- `backend/src/TrainCoach.Integrations/Strava/StravaIntegrationProvider.cs`
- `backend/src/TrainCoach.Application/Integrations/SyncOrchestrator.cs`
- `backend/src/TrainCoach.Application/Integrations/IntegrationConnectionService.cs`
- `backend/src/TrainCoach.Application/Integrations/ConnectorPolicyService.cs`
- `backend/src/TrainCoach.Application/Integrations/Matching/{ActivityMatchingService,ActivityFingerprint,ActivityMatchingOptions}.cs`
- `backend/src/TrainCoach.Infrastructure/BackgroundJobs/QueuedHostedService.cs`
- `backend/src/TrainCoach.Domain/Execution/{ActivitySourceRecord,CompletedActivity,MergeDecision,DuplicateCandidate}.cs`
- `backend/src/TrainCoach.Domain/Wellness/{HrvMeasurement,RecoveryMetric,SleepRecord,WeightMeasurement,TrainingLoadSnapshot}.cs`
- `backend/src/TrainCoach.Domain/Enums/{IntegrationEnums,ExecutionEnums}.cs`
- `backend/src/TrainCoach.Infrastructure/Persistence/Migrations/*.cs` (všechny 4 migrace)

**Frontend kód:**
- `frontend/src/wellness/WellnessTrendsPage.tsx`, `Sparkline.tsx`
- `frontend/src/features/athlete-dashboard/{ReadinessCard,TrainingLoadCard,RecoveryInsightsSection}.tsx`
- `frontend/src/features/athlete-dashboard/useAthleteDashboardData.ts`
- `frontend/src/activities/{ActivityDetailPage,StreamChart}.tsx`
- `frontend/src/api/generated/models/*.ts` (DTO typy)

**Databázové tabulky** (přímý dotaz v `peakform-postgres-1`, databáze `traincoach`):
`IntegrationConnections`, `IntegrationCredentials` (jen struktura, ne obsah), `SynchronizationRuns`, `ActivitySourceRecords` (včetně `RawPayloadJson` klíčů a přítomnosti hodnot), `CompletedActivities`, `ActivityMetrics`, `HrvMeasurements`, `RecoveryMetrics`, `SleepRecords`, `WeightMeasurements`, `TrainingLoadSnapshots`, `HeartRateZones`, `DuplicateCandidates`.

**Endpointy** (potvrzeny výskytem v backend logu skutečné dnešní synchronizace 2026-09-21 06:59):
`GET https://intervals.icu/api/v1/athlete/0/activities`, `GET https://intervals.icu/api/v1/athlete/0/wellness`.

**Dokumentace v repozitáři** (přečtena, ne znovu nezávisle ověřována): `docs/integrations-research.md`, `docs/integrations/canonical-data-and-deduplication-plan.md`, `docs/integrations/activity-matching.md`.

## 12. Omezení auditu

- **Syrová wellness odpověď Intervals.icu se nikde neukládá** (na rozdíl od aktivit, kde `RawPayloadJson` existuje) — pro wellness pole, která jsou v DB vždy `NULL` (SDNN, avgSleepingHR, readiness, stress, mood, soreness, motivation, SpO2, injury), **nelze s jistotou odlišit "Intervals.icu pole neposílá vůbec" od "posílá, ale jako `null`"**. Klasifikace `UNKNOWN_NOT_VERIFIED` v §4.1 je založená na nepřímých důkazech (dřívější rešerše, chybějící zdrojové appky jako Oura/HRV4Training), ne na přímém pozorování syrové odpovědi.
- **Nebyl proveden žádný nový živý HTTP dotaz na Intervals.icu API v rámci tohoto auditu** — záměrně, aby nedošlo k riziku extrahování/vystavení uloženého OAuth tokenu (šifrovaného přes ASP.NET Data Protection) mimo běžný aplikační kód. Veškerá "živá" data v tomto auditu pocházejí z **již proběhlé, aplikací samotnou spuštěné synchronizace** (poslední dnes 06:59), ne z dotazu vytvořeného pro účely auditu.
- **Garmin Health API nebylo možné ověřit živě** — vyžaduje schválený přístup do Garmin Connect Developer Program, který appka nemá. Srovnání v §8 je založené výhradně na dřívější rešerši veřejně dostupné dokumentace (ne na Swagger/skutečných odpovědích), a tato rešerše sama výslovně uvádí několik neověřených bodů (HRV jako samostatné pole, rate limity, přesný obsah "Enhanced Beat-to-Beat Interval").
- **Audit se opírá o data jednoho atleta** (jediný s aktivním, reálně synchronizujícím Intervals.icu připojením v tomto prostředí) — chování se může lišit u jiných zařízení (jiný Garmin model, jiné nastavení funkcí jako Pulse Ox) nebo u atletů s doplňkovými zdroji (Oura/WHOOP), které dnes nejsou aktivované.
- **Sleep quality, hydration a některá další pole zmíněná ve starší rešerši nebyla ověřena vůbec** — nejsou v našem DTO, a bez syrové odpovědi nelze potvrdit, zda je Intervals.icu skutečně posílá.
- Nebyly zkoumány **plánované tréninky / kalendář (`/events`)** ani **athlete profile endpoint** do hloubky nad rámec potvrzení, že se nikdy nevolají — nebyly součástí aktuálně synchronizovaných dat.
