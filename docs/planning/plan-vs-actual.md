# Plán vs. skutečnost v kalendáři

Kalendář ukazuje u každého plánovaného tréninku, co sportovec skutečně odjel: aktivitu, splnění času a rozložení do
tepových zón v plánu a ve skutečnosti. Pod týdnem je souhrn.

| Část | Soubor |
|---|---|
| Párování a souhrn | `backend/src/TrainCoach.Application/Planning/PlanVsActualService.cs` |
| API | `GET /api/athletes/{id}/plan-vs-actual?from&to` (nejvýš 62 dní) |
| UI | `frontend/src/calendar/WeekCalendar.tsx` (`WorkoutActual`, `UnplannedActivities`, `WeekComparison`), `ZoneCompareBars.tsx` |

## Párování

- Ke každému plánovanému tréninku nejvýš jedna aktivita: nejdřív aktivita s explicitní vazbou (`PlannedWorkoutId`,
  ruční zápis), jinak nejdelší aktivita **stejného sportu** z téhož dne. Den se určuje v časovém pásmu sportovce.
- Když aktivita stejného sportu chybí, vezme se nejdelší zbývající aktivita dne s příznakem `SportMismatch`
  (UI: „Jiný sport: …“). Typicky posilování naplánované omylem jako běh. Párování běží ve třech
  průchodech (explicitní vazba → stejný sport → jiný sport). Náhradní párování tak nikdy nevezme aktivitu, která
  sedí sportem k jinému tréninku téhož dne.
- Den může mít víc tréninků (dopolední a odpolední). Kalendář ukáže každý zvlášť a s vlastním srovnáním.
- Každá aktivita patří nejvýš k jednomu tréninku. Ostatní aktivity dne se ukážou jako „mimo plán“ (+ název · čas).
- Den volna se nepáruje. Uplynulý trénink bez aktivity je „Nesplněno“.

## Srovnání

- **Čas:** skutečný čas v pohybu / plánovaný čas. Barva odznaku: 80–120 % zelená, 50–150 % oranžová, jinak červená.
  Stejně se počítá vzdálenost (zobrazená v souhrnu).
- **Souhrn týdne:** čas ve tvaru „12 h 35 min“. Když plán nemá čas nebo vzdálenost, ukáže se jen skutečnost.
  Vzdálenost se skryje, když je nulová na obou stranách. Skutečný čas sčítá všechny aktivity týdne, i ty mimo plán.
  Duplicitní záznamy z více zdrojů nafukují součty. Proto se čekající dvojice po startu a po každé synchronizaci
  přehodnocují (`PendingDuplicateReevaluationJob`, viz `docs/integrations/activity-matching.md`).
- **Zóny v plánu:** z úseků tréninku, které mají jako cíl tepovou zónu (délka × počet opakování). Úseky s jiným cílem
  (volně, tempo, RPE, výkon) jsou „bez cílové zóny“. Úseky zadané jen vzdáleností plánovaný čas nemají a do
  srovnání zón nevstupují.
- **Zóny ve skutečnosti:** z metrik `TimeInHrZone1..7` / `TimeBelowHrZones` (viz `docs/wellness/hr-zones.md`).
- **Graf:** dvě zarovnané 100% lišty (plán, skutečnost), stejné barvy zón jako jinde. Čas bez cílové zóny je
  šrafovaná šedá, čas pod Z1 plná šedá. Přesné časy jsou v tooltipu. Lišta plánu se v souhrnu skryje, když
  plán nemá žádné cílové zóny.
- **Trenér:** plán vidí s oprávněním `ViewTrainingPlan`. Skutečnost jen navíc s `ViewCompletedActivities`, jinak
  API vrátí `ActualAvailable = false` a kalendář skutečnost neukáže.
