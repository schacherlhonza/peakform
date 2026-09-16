# Migrace na design systém "Honza Performance"

Sleduje postup přepracování frontendu na závazný design systém z `docs/DESIGN_SYSTEM.md`.
Viz schválený plán v relaci (design tokeny → primitivy → AppShell → sportovcův dashboard →
trenérský dashboard → zbytek stránek). Tento soubor je jediný zdroj pravdy o tom, co už je
migrováno a co ještě běží na starém Mantine výchozím vzhledu — nenechávat nezdokumentovaný mix.

## Stav fází

- [x] **Fáze 0 — design tokeny a globální styly.** `src/design-system/tokens.css`,
      `src/design-system/globals.css`, `src/theme.ts` (limetková `brand` paleta + vlastní `dark`
      paleta navázaná na tokeny), `src/main.tsx` (`defaultColorScheme="dark"`, notifikace
      přesunuty na `bottom-right` dle spec §6), `src/index.css` vyprázdněn, `src/App.css` smazán.
- [x] **Fáze 1 — design-system primitivy.** Všech 11 povinných primitiv v
      `src/design-system/components/`: Panel, CardHeader, MetricStrip, EmptyState (bespoke);
      Button, IconButton, Badge, SegmentedControl, Modal, Skeleton (Mantine přetémované přes
      `src/design-system/theme-extend.ts` + tenký re-export); FormField (layout wrapper + globální
      `.extend()` na formulářové Mantine inputy); Toast (`showToast()` helper nad
      `@mantine/notifications`).
- [x] **Fáze 2 — AppShell, responzivní sidebar, role-based navigace.**
      `src/app/AppShell.tsx` + `src/app/RoleNavigation.tsx` nahrazují smazaný
      `src/layout/AppLayout.tsx`. Nové stránky `/templates` (`src/features/templates/`,
      trenér-only) a `/notifications` (`src/features/notifications/`, obě role) napojené na
      reálné, dosud nepoužité backend API (`workout-templates`, `notifications`).
- [x] **Fáze 3 — sportovcův dashboard (referenční obrazovka).**
      `src/features/athlete-dashboard/` — `AthleteDashboardPage.tsx` (tenká kompozice),
      `ReadinessCard.tsx` (skóre připravenosti z `recovery-metrics`, nikdy fake 0 — explained
      empty state při chybějících datech), `TodayWorkoutCard.tsx` (dnešní trénink z aktivního
      plánu), `FuelHydrationCard.tsx` (dnešní součty jídla/pití — viz odchylka níže),
      `WeekTimelineSection.tsx` (plán vs. realita, horizontálně scrollovatelné na mobilu, min-width
      730px), `RecoveryInsightsSection.tsx` (poslední report insights — viz odchylka níže),
      `useAthleteDashboardData.ts` (veškerá datová kompozice/logika mimo prezentační komponenty).
      Původní obsah (ranní/večerní check-in karty, nejbližší závod) zachován jako kompaktní
      sekundární panely — nic nezmizelo. `src/dashboard/AthleteDashboardPage.tsx` smazán.
- [x] **Fáze 4 — trenérský dashboard.**
      `src/features/coach-dashboard/` — `CoachDashboardPage.tsx`, `AthleteStatusCard.tsx` (avatar,
      dnešní plán, readiness badge, aktivní zdravotní upozornění — vlastní hook
      `useAthleteStatusData.ts` na sportovce, aby se dodržela pravidla React hooks při
      proměnlivém počtu sportovců), `AttentionQueue.tsx` (odvozeno z reálných nepřečtených
      notifikací, řazeno podle závažnosti — viz odchylka níže), `useCoachDashboardData.ts`.
      Používá stejné primitivy jako sportovcův dashboard (Panel/Badge/EmptyState/Skeleton) —
      žádná odlišná vizuální šablona. `src/dashboard/CoachDashboardPage.tsx` smazán;
      `src/dashboard/` teď obsahuje jen triviální `DashboardPage.tsx` role-router.

      **Toto je povinný minimální stopping point této relace** — dál pokračuje Fáze 5/6.
- [x] **Fáze 5/6 — zbytek stránek.** Všech zbývajících ~20 souborů přestavěno na primitivy
      (Panel/CardHeader/Badge/Button/IconButton/Modal/FormField/EmptyState/Skeleton/showToast):
      `calendar/WeekCalendar.tsx` + `CalendarPage.tsx` (WeekTimeline vzhled, dnešek accent border),
      `workouts/WorkoutDetailPage.tsx` → `WorkoutEditor` (MetricStrip, sticky save bar při editaci
      struktury, opraven `bg="gray.0"` light-mode leak u komentářů), `athletes/AthleteListPage.tsx`
      + `AthleteDetailPage.tsx`, `checkins/MorningCheckInPage.tsx` + `EveningCheckInPage.tsx` +
      `WellnessScaleControl.tsx`, `settings/SettingsPage.tsx` + `HeartRateZonesPage.tsx` +
      `AbbreviationsPage.tsx` + `PermissionsPage.tsx` (obě nahradily `window.confirm()` vlastním
      potvrzovacím Modalem), `integrations/IntegrationsPage.tsx` (přidány per-card loading/error
      stavy), `import/ImportPage.tsx` (opraven `var(--mantine-color-*-0)` light-mode leak u
      barvení řádků), `nutrition/NutritionPage.tsx`, `wellness/WellnessTrendsPage.tsx` +
      `Sparkline.tsx` (sémantické barvy: HRV/RHR=accent, spánek=info; přidán textový alt),
      `races/RacesPage.tsx`, `reports/ReportsPage.tsx` + `ReportCard.tsx` (sdílí `severityTone`
      mapping s `RecoveryInsightsSection.tsx`, žádné fabrikované effect-size/confidence pole —
      stejný princip jako Fáze 3). `LoginPage.tsx`/`RegisterPage.tsx` sjednoceny na `showToast`
      (byly restylované dřív, ještě na přímém `notifications.show`).

      Provedeno jako 9 paralelních agentů (nezávislé stránky, podrobné instrukce odkazující na
      `TemplatesPage.tsx` jako referenční vzor) + 2 stránky (kalendář, editor tréninku) ručně kvůli
      jejich sdílenému/architektonickému významu. Po dokončení nalezeny a opraveny dva další reálné
      bugy (viz "Bugy nalezené při vizuálním ověření Fáze 5/6" níže).

## Záměrné odchylky od ilustrativního seznamu navigace ve spec §5

1. **Trenérská navigace nemá položku "Kalendář".** Generická `/calendar` route byla v dřívější
   session záměrně odstraněna z trenérské navigace, protože se dotazovala na tréninkový plán
   *přihlášeného* uživatele (trenéra), ne vybraného sportovce — to je bezpečnostně
   problematické (viz IDOR test). Ekvivalentní funkčnost existuje přes Sportovci →
   `/athletes/:athleteId`, který vestavěný `WeekCalendar` s `canEdit` obsahuje. Toto zůstává
   zachováno.
2. **Sportovcova navigace nemá samostatnou položku "Tréninky".** Kalendář (`/calendar`) už je
   tréninkovým povrchem (týdenní zobrazení + detail tréninku); duplicitní flat-list route by byla
   redundantní. Volitelné jako budoucí stretch item (alternativní zobrazení stejných, už
   načtených dat, bez nového API volání).
3. **"Reporty"/"Výživa" (trenér: "Reporty" navíc) nejsou ve spec ilustrativním seznamu**, ale jde
   o reálné existující funkce — zachovány jako doplňkové položky nad rámec spec ilustrace.
4. **Ikony zůstávají `@tabler/icons-react`**, ne Lucide. Spec povoluje obojí ("Lucide nebo
   stávající konzistentní outline ikony") — Tabler už je konzistentní outline sada používaná v
   celé aplikaci, takže migrace na Lucide by přidala novou závislost bez funkčního přínosu.

## Záměrné odchylky ve Fázi 3/4 (dodržení "žádná fabrikovaná data")

5. **`FuelHydrationCard` nezobrazuje "aktuální/denní cíl" progress bary přesně dle spec §7.**
   Doménový model nemá žádné pole pro denní nutriční cíl sportovce — jen zalogované záznamy. Aby
   nedošlo k vymyšlení cílové hodnoty, karta zobrazuje reálné dnešní součty (sacharidy, tekutiny)
   s konkrétní další akcí (odkaz na `/nutrition`), ne fiktivní procento naplnění cíle.
6. **`RecoveryInsightsSection`/`InsightCard` nezobrazuje "velikost efektu, období/vzorek, jistotu"
   dle spec §7.** `GeneratedReportInsightDto` (ověřeno v generovaném modelu) nese jen
   `ruleCode`/`severity`/`message`. Karta zobrazuje tato reálná pole; přidání fiktivní statistiky
   by porušilo pravidlo "nikdy nezobrazovat fabrikovaná data" i "nezaměňovat korelaci za kauzalitu".
7. **`AttentionQueue` nezahrnuje "odvoditelné zpožděné check-iny"** zmíněné v plánu — vyžadovalo by
   to dotaz na check-in stav pro každého sportovce v rosteru navíc k již provedeným dotazům
   (recovery, aktivní zdravotní příznaky, plán) na kartu. V rámci rozsahu této relace fronta
   vychází jen z reálných nepřečtených notifikací (`HealthFlagRaised`, `CommentAdded`,
   `PlanUpdated`, `AccessRevoked`, …), řazených podle závažnosti.
8. **`AthleteStatusCard` nezobrazuje samostatné "poslední check-in"** — časové razítko nejnovějšího
   záznamu regenerace (`readinessDate`, viditelné jako badge/kontext skóre) slouží jako obecný
   ukazatel aktuálnosti, aby se nepřidával další dotaz jen pro tento jeden údaj.

## Ověření v prohlížeči — PROVEDENO

Chrome, `npm run dev` (`localhost:5173`) proti reálnému běžícímu backendu. Ověřeno na
1440×1000 / 1024×768 / 390×844: login/register, trenérský dashboard (roster + AttentionQueue,
loading skeleton, empty states), sportovcův dashboard (referenční obrazovka — ReadinessCard
s reálným skóre 92 a conic-gradient ring, TodayWorkoutCard s reálným dnešním tréninkem,
FuelHydrationCard empty state, WeekTimelineSection plán vs. realita včetně reálné synchronizované
aktivity "Běh (Garmin demo)", RecoveryInsightsSection s reálným insightem `elevated_stress`),
nová stránka Šablony (plný CRUD cyklus: vytvoření → toast → smazání přes vlastní potvrzovací
Modal, ne `window.confirm()`), Upozornění (empty state), cross-role navigace, mobilní off-canvas
sidebar (burger otevře/zavře plnou nabídku), mobilní horizontální scroll týdenní timeline
(nezhroutila se do tabulky), klávesnicová navigace + `:focus-visible` accent ring, Modal focus
trap. Konzole bez chyb na žádné z ověřovaných obrazovek.

**Dva reálné bugy nalezené a opravené při tomto ověření:**

1. **Pozadí stránky bylo světle šedé místo tmavě zelené.** Mantine vlastní scoped pravidla pro
   `body`/kořenový element vyhrávala nad `tokens.css` bez ohledu na pořadí importů. Oprava:
   `html, body, #root` v `tokens.css` teď nastavují barvu/pozadí s `!important` (zdůvodněno
   komentářem v souboru — přebíjí knihovní styl, ne libovolná zkratka).
2. **`LoginPage`/`RegisterPage` měly natvrdo `bg="gray.0"` a Mantine `Paper`** (zbytek
   předchozí architektury) — na první pohled aplikace vypadala jako tmavá karta plovoucí na
   světlém pozadí. Opraveno na `Panel` primitivu bez světlého podkladu; `RegisterPage` navíc
   zbaven inline `style={{flexDirection:'row'}}` hacku (nahrazeno `Group grow`).

Všechny čtyři automatizované kontroly (`typecheck`/`build`/`test`/`lint`) zůstávají zelené i po
těchto opravách.

## Bugy nalezené při vizuálním ověření Fáze 5/6

Ověřeno znovu v Chrome (1440×1000, mobilní šířka) po dokončení všech 9 paralelních agentů + ručních
úprav kalendáře/editoru tréninku: přihlášení jako trenér i sportovec, Sportovci (roster + invite
modal), detail sportovce (WeekCalendar s dnešním accent borderem), detail tréninku (WorkoutEditor
včetně editace struktury a sticky save baru), Šablony (mazání přes potvrzovací Modal), Regenerace
(sparklines se sémantickými barvami), Závody a cíle, Jídlo a pití, cross-role redirect na
role-gated routách. Konzole bez chyb všude.

1. **CSS Grid `auto-fill` místo `auto-fit` v roster gridech** (`AthleteListPage.module.css` a
   `CoachDashboardPage.module.css`, druhý vznikl už ve Fázi 4) — s `auto-fill` si grid rezervuje
   neviditelné prázdné sloupce místo aby se `1fr` karty roztáhly na volné místo, takže karty
   sportovců zůstávaly přišpendlené na `minmax` minimum (260px) a jméno/e-mail/badge se zbytečně
   ořezávaly (`truncate`) i při volném místě vedle nich. Opraveno na `auto-fit` v obou souborech.

Zkontrolováno napříč celým `frontend/src`, že žádný jiný soubor nemá stejný `auto-fill` vzorec,
žádný zbylý `window.confirm(`, `var(--mantine-color-*-0)` ani `bg="gray` leak, a že jediné zbylé
přímé `notifications.show(` volání je uvnitř `Toast.ts` (interní implementace `showToast()`).
