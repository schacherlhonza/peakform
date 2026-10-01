# Tréninková zátěž, kondice a únava (PeakForm)

PeakForm počítá vlastní tréninkovou zátěž každé aktivity z tepu a z ní denní kondici (CTL), únavu (ATL) a formu
(TSB) za celou historii sportovce. Hodnoty z intervals.icu zůstávají uložené. **U CTL/ATL ale mají přednost hodnoty
PeakForm** (rozhodnuto 1. 10. 2026), protože pokrývají celou historii.

| Část | Soubor |
|---|---|
| Výpočet | `backend/src/TrainCoach.Application/Wellness/TrainingLoadCalculator.cs` |
| Přepočet | `backend/src/TrainCoach.Application/Wellness/TrainingLoadRecomputeJob.cs` |
| API | `GET /api/athletes/{id}/training-load?from&to[&source=PeakForm]` |
| UI | Regenerace → „Kondice, únava a forma“ (`TrainingLoadHistorySection.tsx`) |

## Výpočet

- **Zátěž aktivity:** Banisterův TRIMP = Σ minut × HRr × k·e^(b·HRr), kde HRr je podíl tepové rezervy
  (k = 0,64, b = 1,92, u žen 0,86 / 1,67). Normalizuje se tak, aby **hodina na prahovém tepu = 100 bodů**, tedy na
  stejnou stupnici jako TSS/hrTSS a zátěž z intervals.icu. Mezera mezi vzorky delší než 30 s je pauza. Aktivita bez
  uloženého streamu se odhadne z průměrného tepu a trvání. Aktivita bez tepu zátěž nemá.
- **Vstupní hodnoty** (rozhodnuto 1. 10. 2026):
  - **Klidový tep:** z profilu → medián z wellness dat → 60.
  - **Maximální tep:** z profilu → nejvyšší naměřený za posledních 365 dní (věrohodné hodnoty 120–220) → starší
    historie → 190.
  - **Prahový tep:** spodní hranice zóny 4 nejnovější sady zón → 89 % maxima.
- **CTL / ATL:** exponenciálně vážený průměr denní zátěže za 42 / 7 dní. Dny bez aktivity mají nulovou zátěž,
  takže obě hodnoty klesají. Ramp rate = změna CTL za 7 dní. Den se určuje v časovém pásmu sportovce.
- Výsledky se ukládají jako `ActivityMetric.TrainingLoad` a `TrainingLoadSnapshot` se zdrojem `DataSource.PeakForm`.
  Každý přepočet je úplný (pro ~3 500 aktivit zabere jednotky sekund).

## Kdy se přepočítává

Po každé úspěšné synchronizaci (CTL/ATL se mění každý den), po importu archivu, po uložení zón (mění se práh),
po stažení streamů z intervals.icu a jednou po startu aplikace.

## Výběr zdroje

- API bez `source` vrací za každý den jeden řádek, a to zdroj s nejvyšší prioritou. Hodnoty různých zdrojů se
  v jednom dni nikdy nemíchají.
- **Pro CTL/ATL je PeakForm první** (`DailyMetricSelectionService.DefaultRankFor`, `MetricKindsPreferringPeakForm`).
  Kondice je kumulativní za týdny historie, a intervals.icu zná jen aktivity od svého připojení. Jeho CTL tak začíná
  na nule a měsíce zůstává nízké. Ruční přepsání priority sportovcem (`AthleteMetricSourcePrecedence`) má dál
  přednost. U ostatních metrik (HRV, spánek, klidový tep, váha) zůstává PeakForm až na konci pořadí.
- Přepočet zátěže na konci obnoví uložené denní výběry (`DailyMetricSelection`) pro dny, kde existují i hodnoty
  jiného zdroje, protože z nich čte readiness.
- Graf historie žádá `source=PeakForm`, aby řada neměla švy. PeakForm je jediný zdroj, který pokrývá roky zpět.

## Ověření na reálných datech (1. 10. 2026)

- **Zátěž aktivit:** u 15 aktivit, které mají zátěž i z intervals.icu, je hodnota PeakForm o 0–15 % nižší (Spartan
  116 vs 112, běh 42 vs 47, chůze 32 vs 34).
- **CTL se liší výrazně:** k 30. 9. je 32 z intervals.icu proti 50 z PeakForm. intervals.icu počítá jen z aktivit,
  které má u sebe, a 27. 8. začínalo od nuly. Jeho CTL proto teprve stoupá a podhodnocuje. ATL se shoduje (68 vs 64).
