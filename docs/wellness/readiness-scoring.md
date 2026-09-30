# Readiness — výpočet skóre připravenosti

Popisuje, jak vzniká skóre v bloku **Připravenost** na dashboardu sportovce. Implementace:

| Část | Soubor |
|---|---|
| Čistý výpočet (bez DB) | `backend/src/TrainCoach.Application/Wellness/ReadinessCalculator.cs` |
| Stav zdraví (rozsahy normálu) | `backend/src/TrainCoach.Application/Wellness/HealthStatusCalculator.cs` |
| Načtení dat, výběr zdroje, baseline | `backend/src/TrainCoach.Application/Wellness/ReadinessService.cs` |
| Endpoint | `GET /api/athletes/{athleteUserId}/readiness?date=YYYY-MM-DD` (`ReadinessController`) |
| UI | `frontend/src/features/athlete-dashboard/ReadinessCard.tsx`, `HealthStatusSection.tsx` |
| Testy | `ReadinessCalculatorTests`, `HealthStatusCalculatorTests` (Application.Tests), `ReadinessApiTests` (Api.IntegrationTests) |

## Proč vlastní výpočet

Původně karta zobrazovala jen `RecoveryMetric.ReadinessScore`, tedy skóre dodané poskytovatelem. Garmin přes intervals.icu ale skóre připravenosti **nedodává**. Dodává HRV, klidový tep, spánek (délku i skóre) a CTL/ATL. Karta proto zůstávala prázdná, i když byla data k dispozici (zjištěno 2026-09-28).

Principy:

- **Skóre poskytovatele má přednost.** Když ho zdroj má (Oura, WHOOP…), zobrazí se beze změny (`ScoreSource = Vendor`).
- **Jinak odhad PeakForm** (`ScoreSource = Computed`). Je jednoduchý a vysvětlitelný. UI vždy ukazuje i dílčí skóre jednotlivých signálů, takže nejde o černou skříňku.
- **Skóre se nikdy nevymýšlí.** Když je dat málo, vrátí se `Score = null` a UI zobrazí surové hodnoty s vysvětlením.
- Jde o orientační odhad, ne o diagnózu.

## Výběr dat (`ReadinessService`)

1. **Jedna hodnota na metriku a den.** Pokud existuje `DailyMetricSelection` (respektuje přepsání priority zdroje sportovcem), použije se ta. Jinak se ze surových řádků po zdrojích vybere podle výchozí priority `DailyMetricSelectionService.DefaultRankFor` (intervals.icu > Strava/Oura/WHOOP > demo > import > ruční). Hodnoty z různých zdrojů se **nikdy neprůměrují**.
2. **Den, o kterém skóre je.** Frontend posílá lokální „dnes“. Vezme se nejnovější den v okně `dnes … dnes − 3` (`LookbackDays = 3`), který má aspoň jeden regenerační signál: HRV, klidový tep, spánek nebo skóre poskytovatele. Samotná tréninková zátěž se nepočítá, protože vychází z tréninků, ne z těla. Pokud dnešní data chybí, odpověď má `IsToday = false` a UI ukáže „Naposledy …“.
3. **Baseline** = aritmetický průměr hodnot za **7 dní před** daným dnem, bez dne samotného (`BaselineDays = 7`). Počítá se jen při **≥ 3 hodnotách** (`MinimumBaselineSamples = 3`). Jinak signál nemá srovnání a nevstupuje do skóre (UI: „zatím bez srovnání s průměrem“).
4. **Ranní check-in** (`DailyCheckIn`, `Type = Morning`) z téhož dne. Pole `HasPainOrIllness` se v UI zobrazí jako varování, ale skóre nemění.

## Algoritmus (`ReadinessCalculator.Calculate`)

Každý dostupný signál se převede na dílčí skóre 0–100 (zaokrouhleno, oříznuto na 0–100). Výsledek je **vážený průměr přes dostupné signály**: váhy chybějících signálů se nepočítají a zbytek se přepočítá na 100 %.

| Signál | Váha | Vzorec | Kotevní body |
|---|---|---|---|
| **HRV** (rMSSD) | 30 | `80 + (hodnota / baseline − 1) × 300` | na baseline = 80 · −10 % = 50 · −20 % = 20 · +6,7 % = 100 |
| **Spánek** | 25 | skóre spánku od zdroje, pokud existuje; jinak `90 − (480 − minuty) × 0,3` | 8 h = 90 · 7 h = 72 · 6 h = 54 · 5 h = 36 |
| **Klidový tep** | 20 | `80 − (hodnota − baseline) × 8` | na baseline = 80 · +2,5 bpm = 60 · +5 bpm = 40 · −2,5 bpm = 100 |
| **Tréninková zátěž** | 15 | `80 + (CTL − ATL) × 1,5` (forma / TSB) | TSB 0 = 80 · −10 = 65 · −20 = 50 · −30 = 35 · +13 = 100 |
| **Subjektivní pocit** | 10 | `(průměr − 1) / 4 × 100` přes vyplněné dimenze ranního check-inu | vše 3/5 = 50 · vše 5/5 = 100 |

Poznámky k signálům:

- **HRV a klidový tep** se hodnotí vůči vlastní baseline, ne vůči absolutním normám (rMSSD se mezi lidmi liší řádově).
- **Spánek:** skóre spánku od zdroje už zahrnuje délku, fáze i přerušení, proto má přednost před samotnou délkou.
- **Forma:** pásmo −10 až −30 je běžné pro produktivní trénink, pod −30 jde o nahromaděnou únavu.
- **Check-in:** dimenze jsou Energy, Fatigue, LegsFeeling, Stress, SleepQuality, MuscleSoreness a Motivation. Všechny mají škálu „vyšší = lepší stav“ (stejně je čte `ReportRuleEngine`, např. `Fatigue ≤ Poor` = vysoká únava).

**Minimum dat:** skóre se počítá jen z **≥ 2 signálů** (`MinimumComponents = 2`). Jinak je `Score = null` a UI zobrazí „Skóre zatím nelze spočítat“.

**Slovní hodnocení v UI:** ≥ 75 „Výborná“ (zelená) · 50–74 „Snížená“ (oranžová) · < 50 „Nízká“ (červená). Stejné prahy obarvují i pruhy dílčích skóre.

## Příklad: reálná data 2026-09-28 (Garmin přes intervals.icu)

| Signál | Hodnota | Baseline | Dílčí skóre | Váha |
|---|---|---|---|---|
| HRV | 43 ms | 47,7 ms (−10 %) | 50 | 30 |
| Klidový tep | 52 bpm | 50,6 bpm (+1,4) | 69 | 20 |
| Spánek | 8h 35m, skóre 86 | — | 86 | 25 |
| Tréninková zátěž | CTL 30,3 / ATL 65,1 → forma −35 | — | 28 | 15 |
| Check-in | nevyplněn | — | — | (10, vynecháno) |

`(50×30 + 69×20 + 86×25 + 28×15) / 90 = 60,6` → **61 / 100, „Snížená“**. Skóre táhne dolů nahromaděná únava z tréninku (ATL ≫ CTL) a HRV pod průměrem, spánek ho naopak drží nahoře.

## Stav zdraví (`HealthStatusCalculator`)

Sekce v kartě Připravenost podle vzoru Garmin „Status zdraví“. Každá noční metrika se porovná s **osobním běžným rozsahem** a zařadí do skupiny *Mimo rozsah / V rozsahu / Bez rozsahu / Žádné údaje*. Nahoře je souhrn, např. „Všechny metriky jsou v rozsahu“.

Jak se liší od readiness:

- **Stav zdraví** odpovídá na otázku „chová se tělo normálně?“ a nezahrnuje tréninkovou zátěž.
- **Readiness** odpovídá na otázku „jak jsem dnes připravený trénovat?“ a zátěž zahrnuje.
- Řádky HRV, klidového tepu a skóre spánku (případně délky spánku, pokud skóre chybí) ukazují i svůj **příspěvek k readiness** (dílčí skóre). Tréninková zátěž a ranní check-in zůstávají v kartě jako „Další faktory připravenosti“.

**Rozsah** = průměr ± 1,5 × směrodatná odchylka hodnot za **28 dní před** daným dnem (`WindowDays = 28`, bez dne samotného). Počítá se až od **5 hodnot** (`MinimumSamples = 5`), do té doby je stav *Bez rozsahu*. Aby velmi stabilní historie nedala tak úzký rozsah, že by běžný šum vypadal jako odchylka, má každá metrika minimální poloviční šířku:

| Metrika | Zdroj | Min. ± | Znepokojivý směr |
|---|---|---|---|
| HRV | `HrvMeasurement.RmssdMs` | 8 % průměru | pod rozsahem |
| Klidový tep | `RecoveryMetric.RestingHeartRateBpm` | 2 bpm | nad rozsahem |
| Délka spánku | `SleepRecord.DurationMinutes` | 30 min | pod rozsahem |
| Skóre spánku | `SleepRecord.SleepScore` | 5 bodů | pod rozsahem |
| Tep ve spánku | `SleepRecord.AvgSleepingHeartRateBpm` | 2 bpm | nad rozsahem |
| SpO2 | `RecoveryMetric.SpO2Percent` | 1,5 % | pod rozsahem |

Hodnota mimo rozsah ve **znepokojivém směru** má `IsConcerning = true` a UI ji zvýrazní oranžově s vysvětlením. Odchylka opačným směrem (např. HRV nad rozsahem) se jen označí a vysvětlí jako obvykle dobré znamení.

**Ukazatel:** 270° oblouk. Zvýrazněná část je rozsah normálu, tečka je dnešní hodnota. Stupnice přesahuje rozsah o 60 % jeho šířky na každou stranu, takže hodnota mimo rozsah leží viditelně mimo zvýrazněnou část.

### Dostupnost dat z Garminu přes intervals.icu (ověřeno 2026-09-29)

Diagnostika `dotnet TrainCoach.Api.dll --diagnose-intervals-icu <athleteUserId>` vypisuje, která wellness pole účet skutečně vyplňuje (řádek `non-null field counts`). Pro testovací účet (Garmin → intervals.icu):

- **Vyplněné:** `hrv`, `restingHR`, `sleepSecs`, `sleepScore`, `sleepQuality`, `steps`, `vo2max`, `ctl`/`atl`/`rampRate`.
- **Prázdné:** `spO2`, `respiration`, `avgSleepingHR`. Garmin je do intervals.icu neposílá, proto je UI ukazuje jako „Žádné údaje“. Teplotu kůže intervals.icu nemá vůbec.
- **Historie** HRV, tepu a spánku v intervals.icu začíná až 2026-09-18, kdy byl propojen Garmin. Backfill delší historie by tedy přidal jen CTL/ATL, proto se zatím nedělá.
- Dechová frekvence (`respiration`) se zatím **neimportuje** (chybí v DTO i v datovém modelu). Až ji zdroj začne posílat, stačí ji přidat do `IntervalsIcuWellnessEntry`, `ExternalWellnessSample`, uložit (např. do `RecoveryMetric`) a přidat `HealthMetric.Respiration`.

## Známé limity a otevřené otázky

Konstanty jsou **výchozí odhad**, ne validovaná metodika. Až bude víc dat, stojí za to je projít:

- **Kalibrace vah a sklonů.** Porovnat odhad se subjektivním pocitem z check-inů a s tréninkovým výkonem v následujících dnech.
- **Baseline.** Sedmidenní průměr je citlivý na jednotlivé výkyvy. Zvážit delší okno (14–28 dní), medián nebo pracovat s ln(rMSSD) a směrodatnou odchylkou (běžný přístup HRV aplikací: „v normálním pásmu / pod ním“).
- **Bolest nebo nemoc** skóre zatím nesnižuje, jen se zobrazí jako varování. Zvážit strop (např. max 40).
- **Kombinace se skórem poskytovatele.** Skóre poskytovatele dnes plně nahrazuje odhad. Zvážit, jestli ho místo toho brát jako jeden ze signálů.
- **Absolutní délka spánku** nebere v úvahu individuální potřebu spánku.
- **Konstanty jsou v kódu** (`ReadinessCalculator`, `ReadinessService`), ne v `appsettings`. Při ladění je zvážit přesunout do options jako u `ActivityMatchingOptions`.
- **Dvě baseline:** readiness používá 7denní průměr, stav zdraví 28denní rozsah. Obě jsou záměrně jednoduché, ale časem je zvážit sjednotit (např. aby readiness bral odchylku v násobcích SD z 28denního okna).
- **Rozsah z krátké historie** (tady 12 dní) se bude lišit od Garminu, který má delší historii. Např. HRV 62 ms je u Garminu v rozsahu 39–63 ms, u nás zatím nad rozsahem.
- Při změně vzorců upravit i kotevní body v `ReadinessCalculatorTests` / `HealthStatusCalculatorTests` a tento dokument.
