# Honza Performance — Design System

## 1. Účel a vizuální směr

Tento dokument je závazná UI specifikace pro aplikaci trenér–sportovec. Vychází z původního dashboardu **Honza Performance** a má být aplikován na React frontend bez změny existující business logiky.

Vizuální charakter: **prémiový tmavý sportovní analytický cockpit**. Rozhraní má působit klidně, přesně a výkonnostně — ne jako generická administrační šablona. Nejvyšší vizuální prioritu mají dnešní stav, další rozhodnutí a vztah plán × realita × regenerace.

Zásady:

- Dark-first. Základ není čistě černý, ale velmi tmavá zelená.
- Jeden dominantní limetkový akcent. Ostatní barvy jsou pouze sémantické.
- Velké informace mají dost prostoru; sekundární metadata jsou kompaktní.
- Panely používají jemné gradienty, tenké hranice a hluboký měkký stín.
- Čísla a stav musí být čitelné během několika sekund.
- UI nesmí sklouznout k bílým kartám, Bootstrap vzhledu, přebarveným tabulkám ani nahodilým gradientům.
- Dekorace nikdy nesmí konkurovat datům.

## 2. Design tokeny

Použij centrální CSS custom properties. Komponenty nesmějí obsahovat nahodilé hex hodnoty, pokud nejde o logo externí služby nebo datovou vizualizaci.

```css
:root {
  --color-bg: #07110f;
  --color-surface: rgba(17, 31, 27, 0.94);
  --color-surface-2: #152a24;
  --color-surface-3: #1b352e;
  --color-surface-inset: #10231d;
  --color-surface-input: #0b1a15;
  --color-sidebar: rgba(6, 17, 14, 0.78);

  --color-border: rgba(188, 218, 203, 0.12);
  --color-border-strong: rgba(199, 243, 77, 0.35);

  --color-text: #f4faf7;
  --color-text-muted: #91a79e;
  --color-text-subtle: #668078;

  --color-accent: #c7f34d;
  --color-accent-hover: #d8ff6c;
  --color-accent-deep: #99ca2f;
  --color-success: #51dfb0;
  --color-info: #72a8ff;
  --color-warning: #ff9a61;
  --color-danger: #ff7e72;

  --shadow-panel: 0 28px 70px rgba(0, 0, 0, 0.22);
  --shadow-modal: 0 35px 100px rgba(0, 0, 0, 0.55);
  --shadow-accent: 0 8px 24px rgba(159, 205, 50, 0.15);

  --radius-xs: 8px;
  --radius-sm: 10px;
  --radius-md: 12px;
  --radius-lg: 14px;
  --radius-xl: 16px;
  --radius-panel: 22px;
  --radius-modal: 24px;

  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-5: 20px;
  --space-6: 24px;
  --space-8: 32px;
  --space-9: 36px;
  --space-12: 48px;

  --sidebar-width: 246px;
  --content-max-width: 1720px;
  --topbar-height: 108px;
  --transition-fast: 200ms ease;
}
```

### Povrch stránky

```css
body {
  margin: 0;
  min-height: 100vh;
  color: var(--color-text);
  background:
    radial-gradient(circle at 72% 0%, #123328 0, transparent 30%),
    var(--color-bg);
}
```

Volitelně lze použít dvě velmi rozostřené ambientní plochy s opacitou maximálně `0.13`. Neumisťovat gradient na každou kartu.

## 3. Typografie

Primární font: `Inter`, fallback `ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`.

| Styl | Velikost | Váha | Další pravidla |
|---|---:|---:|---|
| Page title | `22–30px` | 700–800 | `letter-spacing: -0.03em` |
| Section title | `25–38px` | 700–800 | `line-height: 1.08`, `letter-spacing: -0.045em` |
| Card headline | `17–23px` | 700 | `line-height: 1.16–1.25` |
| Key metric | `21–48px` | 700–850 | těsnější tracking |
| Body | `11–13px` | 400–600 | `line-height: 1.55–1.65` |
| Control | `11–12px` | 700–850 | stručný text |
| Eyebrow / kicker | `9–10px` | 750 | uppercase, `letter-spacing: .09–.14em` |
| Metadata | `8–10px` | 500–700 | muted color |

Malá velikost metadata je záměrná, ale nesmí nést primární rozhodovací informaci. Pro běžné popisy v produkční aplikaci preferuj minimálně `12px`; původní 8–10px používej jen na krátké štítky a doplňkové hodnoty.

## 4. Layout aplikace

### Desktop

- Fixní levý sidebar: `246px`, přes celou výšku.
- Hlavní obsah: `margin-left: 246px`, horizontální padding `48px`, max-width `1720px`.
- Topbar: `108px`, spodní jemná border.
- Obsah sekce začíná `36px` pod topbarem.
- Standardní gap mezi panely: `17px`.
- Dashboard overview: `minmax(430px, 1.25fr) minmax(390px, 1fr)`.
- Readiness karta zabírá dva řádky; vpravo je dnešní trénink a palivo/hydratace.

### Breakpointy

- `≤1100px`: sidebar `205px`; obsah padding `28px`; hlavní dashboard jeden sloupec.
- `≤780px`: sidebar je off-canvas; obsah bez levého offsetu, padding `17px`; topbar `87px`; datové gridy jeden sloupec.
- `≤520px`: panel padding `17px`; přepínače na celou šířku; složené metriky se vertikálně skládají.

Horizontální týdenní timeline může na mobilu zůstat scrollovatelná. Nesmí se smrštit do nečitelné tabulky.

## 5. Navigace

### Sidebar

- Poloprůhledný tmavý povrch s `backdrop-filter: blur(20px)`.
- Logo: limetkový čtverec `37×37px`, radius `10px`, tmavý monogram.
- Navigační řádek: min-height `48px`, radius `13px`, gap ikony a textu `14px`.
- Aktivní položka: akcentní text, lehká akcentní plocha a `3px` vnitřní levý indikátor.
- Hover pouze jemně zesvětlí povrch; žádné velké animace.
- Dole je soukromí/synchronizace a profil přihlášeného uživatele.

Navigace musí být odlišná podle role, ale vizuální systém zůstává stejný:

- Sportovec: Přehled, Kalendář, Tréninky, Regenerace, Souvislosti, Závody.
- Trenér: Přehled, Sportovci, Kalendář, Šablony, Upozornění.

## 6. Základní komponenty

### `Panel`

```css
.panel {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-panel);
  background: linear-gradient(150deg,
    rgba(23, 43, 36, 0.96),
    rgba(13, 28, 23, 0.94));
  box-shadow: var(--shadow-panel);
}
```

Výchozí padding `22–24px`, mobil `17px`. Nevytvářet mnoho vnořených plnohodnotných panelů; vnitřní sekce používají `surface-inset`, border nebo divider.

### `CardHeader`

Flex mezi názvem a stavem/akcí. Název je uppercase kicker `9px`; pravá část používá sémantickou barvu nebo text button.

### `PrimaryButton`

- Výška `44px`, radius `12px`, horizontální padding `19px`.
- Akcentní background, velmi tmavý text, váha 850.
- Hover: `--color-accent-hover`, posun maximálně `-1px`.
- Compact varianta: výška `38px`.
- Disabled: snížená opacity, bez hover transformace.

### Sekundární a ikonové tlačítko

Tmavý surface, `1px` border, radius `9–11px`. Hover mění border a icon/text na accent. Ikonové tlačítko `38×38px`.

### `SegmentedControl`

Vnější tmavý inset surface, border a radius `12px`, vnitřní padding `4px`. Aktivní segment má `#274039`, bílý text a jemný stín. Použít například Ráno/Večer nebo Týden/Měsíc.

### `StatusBadge`

Pill nebo radius `8–999px`, malý tučný text. Používej tónovaný průhledný background:

- positive/readiness: accent nebo success,
- information/plan: info,
- warning/load: warning,
- critical/pain/error: danger,
- neutral/draft: muted surface.

Barva nesmí být jediným nositelem významu; vždy přidej text nebo ikonu.

### `MetricStrip`

Grid metrik oddělený jednobodovým gapem vytvořeným přes background border barvy. Vnitřní bloky používají `#10231d`. Label uppercase, hlavní hodnota výrazná, trend drobný a sémanticky zbarvený.

### Formuláře

- Input/select výška `42px`, radius `10px`.
- Background `--color-surface-input`, text bílý, border `--color-border`.
- Focus border `rgba(199,243,77,.6)` a viditelný focus ring.
- Label nad polem, jednotka zarovnaná doprava.
- Chyba: danger text + danger border; nikdy pouze červený border bez vysvětlení.

### Modal

- Backdrop `rgba(2,8,6,.78)` + blur `9px`.
- Šířka max `680px`, radius `24px`, panel `#10231d`.
- Border s lehkým akcentem, stín `--shadow-modal`.
- Close button vpravo nahoře.
- Focus trap, Escape, návrat focusu a zákaz scrollování body jsou povinné.

### Toast

Vpravo dole, max-width `330px`, tmavě zelený surface, jemný accent border. Automatické zmizení cca 3.2 s, `aria-live="polite"`.

## 7. Doménové komponenty

### `ReadinessCard`

Dominantní karta sportovce. Obsahuje:

1. label Připravenost + stav aktuálnosti,
2. kruhové skóre `166px` pomocí `conic-gradient`,
3. slovní stav a jednoznačný headline,
4. krátké vysvětlení,
5. spodní strip 4 klíčových metrik.

Skóre není diagnóza. Při chybějících datech zobraz vysvětlený empty state, ne falešnou nulu.

### `TodayWorkoutCard`

Ikona aktivity, čas, název, dominantní dávka (např. `5 × 6′`), tři strukturované parametry a trenérská poznámka. V produkci nesmí být trénink redukován na jediný textový blok.

### `FuelHydrationCard`

Dva progresy (sacharidy, tekutiny), aktuální/denní cíl a konkrétní další akce. Sacharidy používají accent, tekutiny info blue. Na mobilu jeden sloupec.

### `WeekTimeline`

- Souhrn zátěže, plnění, rovnováhy a legenda.
- Sedm denních sloupců; dnešek akcentní border.
- Plán a skutečnost jako dvojice úzkých sloupců.
- Dole trénink, detail, load a stav.
- Minimální šířka celé osy `730px`, na mobilu horizontální scroll.

### `PlanActualStateFlow`

Tři body: plán → zátěž → stav. Každý má vlastní sémantickou barvu: info → accent → success. Používat jen tam, kde pomáhá pochopit vztah; ne jako dekoraci.

### `InsightCard`

Musí obsahovat tvrzení, velikost efektu, období/vzorek a jistotu. Souvislost nesmí být formulována jako kauzalita. Hlavní insight může být velký panel, ostatní kompaktní řádky.

### Trenérské komponenty

Odvoď ze stejného systému:

- `AthleteStatusCard`: avatar, dnešní plán, readiness, poslední check-in, varování.
- `AttentionQueue`: seřazené problémy vyžadující trenéra; warning/danger jen na konkrétní problém.
- `WorkoutEditor`: panelové sekce, strukturované segmenty, sticky save action.
- `PlanVsActual`: stejné grafické kódování jako sportovcova timeline.

## 8. Ikony a grafy

- Ikony: jednotná outline sada, ideálně Lucide React.
- Standard `20–22px`, stroke cca `1.8`, round caps/joins.
- Nepoužívat emoji jako systémové ikony.
- Grafy: tmavé transparentní pozadí, grid `rgba(255,255,255,.07)`, bez těžkých os.
- HRV/accent, spánek/info blue, zátěž/warning orange.
- Tooltips musí mít tmavý surface a jasně uvést jednotku a datum.
- Každý graf musí mít alternativní text nebo tabulkovou/listovou interpretaci.

## 9. Interakce a stavy

Každá datová komponenta musí mít:

- loading skeleton odpovídající výslednému layoutu,
- empty state s důvodem a další akcí,
- error state s možností opakovat načtení,
- stale/synchronizing stav,
- success feedback po mutaci.

Animace trvají přibližně `200–260ms`. Respektuj `prefers-reduced-motion`. Nepoužívej dlouhé entrance animace, bouncing ani pulzování mimo drobný live/status bod.

## 10. Přístupnost

- Minimálně WCAG AA kontrast pro text a ovládací prvky.
- Viditelný `:focus-visible` ring v accent barvě.
- Vše ovladatelné klávesnicí.
- Ikonová tlačítka mají `aria-label` a tooltip.
- Segmented control používá korektní tab semantics jen tehdy, mění-li skutečně panel.
- Grafy, skóre a barevné stavy mají textové vysvětlení.
- Klikací plocha minimálně `40×40px`, ideálně `44×44px`.

## 11. React struktura

Doporučená organizace:

```text
src/
  app/
    AppShell.tsx
    RoleNavigation.tsx
  design-system/
    tokens.css
    globals.css
    components/
      Panel.tsx
      Button.tsx
      IconButton.tsx
      Badge.tsx
      CardHeader.tsx
      SegmentedControl.tsx
      MetricStrip.tsx
      Modal.tsx
      Toast.tsx
      FormField.tsx
      EmptyState.tsx
      Skeleton.tsx
  features/
    athlete-dashboard/
    coach-dashboard/
    training-calendar/
    workouts/
    recovery/
    insights/
    integrations/
```

Design-system komponenty nesmějí importovat business feature. Feature komponenty skládají design-system primitives. API data a výpočty readiness nesmějí být implementované uvnitř prezentačních komponent.

## 12. Vizuální kontrolní seznam

- [ ] Background je zelenočerný, nikoliv neutrálně šedý nebo čistě černý.
- [ ] Akcent `#c7f34d` se používá selektivně, nikoliv na vše.
- [ ] Panely mají gradient, jemnou border, radius 22px a měkký hluboký stín.
- [ ] Desktop má pevný sidebar a vzdušný obsah.
- [ ] Primární dashboard začíná připraveností a akcí, ne tabulkou.
- [ ] Primární čísla jsou výrazná, metadata kompaktní.
- [ ] Plan/actual/recovery mají konzistentní barevné mapování.
- [ ] Mobil je skutečně přeuspořádaný, ne pouze zmenšený.
- [ ] Loading, empty, error a stale stavy vypadají jako součást stejného systému.
- [ ] Trenérská i sportovcova role sdílejí stejné primitives.
- [ ] UI neobsahuje generické bílé karty, náhodné barvy ani nesourodé radiusy.
- [ ] Každá nová stránka vizuálně odpovídá původnímu Honza Performance dashboardu.

