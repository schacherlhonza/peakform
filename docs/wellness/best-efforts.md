# Nejlepší výkony a osobní rekordy

Z uložených streamů aktivit se počítají nejlepší výkony (`ActivityBestEffort`) a z nich se za běhu odvozují osobní
rekordy. Rekordy se nikam nekopírují, takže smazání, sloučení nebo vyčištění aktivity nemůže nechat zastaralý rekord.
Ručně zadané rekordy (`PersonalRecord`, stránka Regenerace) jsou samostatná funkce a zůstávají beze změny.

| Část | Soubor |
|---|---|
| Výpočet | `backend/src/TrainCoach.Application/Execution/Streams/BestEffortCalculator.cs` |
| Uložení, přepočet historie | `backend/src/TrainCoach.Application/Execution/BestEffortRecomputeJob.cs` |
| Rekordy, pořadí | `backend/src/TrainCoach.Application/Execution/PersonalBestService.cs` |
| API | `GET /api/athletes/{id}/personal-bests`, `GET /api/activities/{id}/best-efforts` |
| UI | Aktivity → Rekordy (`PersonalBestsView.tsx`), detail aktivity (`BestEffortsPanel.tsx`) |

## Co se počítá

- **Běh:** nejrychlejší 1 km, 5 km, 10 km, půlmaraton a maraton uvnitř jakékoli aktivity (ne jen závodu).
  Používá se posuvné okno nad kanálem vzdálenosti a začátek úseku se dopočítá mezi vzorky.
- **Výkon:** nejlepší průměr za 1, 5 a 20 minut, zvlášť pro každý sport (běžecký výkon z Garminu a výkon na kole
  jsou samostatné rekordy). Okno nesmí přes pauzu (mezera mezi vzorky > 30 s).

## Čištění dat (ověřeno na reálném archivu 3 490 aktivit)

- **Skoky GPS:** přírůstek vzdálenosti se zahodí, pokud by rychlost za posledních 60 s překročila 8 m/s, **nebo**
  pokud je rychlejší než 10 m/s od posledního zvýšení vzdálenosti.
  - Podle druhé podmínky se pozná „doskok“ po ztrátě signálu: vzdálenost se zastaví a pak naráz doskočí, což je
    skutečně uběhnutá vzdálenost. Takový doskok se zachová (maraton 2019: 356 m po 39 s zastavení).
  - Bloudící GPS skáče za plného pohybu, proto se zahodí (jeden běh měl 23 skoků až 144 m/s a z nich „1 km za 2:31“).
  - Doskoky po zastavení odpovídaly v celém archivu rychlosti 1–3 m/s, nejvýš 9 m/s.
- **Výpadky výkonu:** vzorky nad 1 000 W u běhu a nad 2 500 W u ostatních sportů se ignorují. Běžecký výkon měl
  špičky až 3 664 W a z nich „2 479 W za minutu“.
- Úsek rychlejší než 7 m/s (~2:23 /km) se zahodí.

## Přesnost

- **Nové aktivity** (import archivu, soubory z intervals.icu) se počítají přímo z plného souboru (`IsPrecise`).
- **Historie** uložená dřív se po startu aplikace dopočítá ze zmenšeného streamu. Na archivu to dělá rozdíl
  průměrně 0,1 s, nejvýš 3,2 s. UI takové hodnoty značí „≈“. Další import archivu je přepočítá přesně.
- Změna algoritmu = zvýšení `BestEffortCalculator.Version`. Startovní průchod pak všechno přepočítá.
