# Strukturované tréninky v Garminu — co lze vytvořit a co z toho projde přes intervals.icu

Stav: **rešerše (2026-10-05)**, podklad pro implementaci pushe z `docs/integrations/garmin-calendar-push.md`. Dokument popisuje (1) jak Garmin modeluje strukturovaný trénink, (2) co z toho umí intervals.icu, přes které budeme pushovat, a (3) co to znamená pro náš model `PlannedWorkout`/`WorkoutSegment`.

Značení důvěryhodnosti: **[oficiální]** = dokumentace/SDK Garminu nebo vývojář intervals.icu, **[RE]** = reverse-engineering open-source knihoven nad Garmin Connect, **[komunita]** = fórum / třetí strana, **[neověřeno]** = odvozené nebo z jediného zdroje. Co je neověřené, je potřeba vyzkoušet živě před implementací.

## 1. Shrnutí pro rozhodování

- Garmin umí výrazně víc, než projde přes intervals.icu. **Rozhoduje průnik**, tj. to, co umí textový formát intervals.icu (§3). Plné Garmin Training API je jen pro schválené komerční partnery a jeho schéma není veřejné (§2.7).
- Náš model po rozšíření o šablony pokrývá jednoduché tréninky dobře. Pro intervalové tréninky ale **chybí opakovaný blok více kroků** („6× (5 min Z4 + 2 min klus)“). Garmin i intervals.icu ho umí, my ne. Je to největší mezera (§4).
- **Tichá selhání:** když athlete nemá v intervals.icu nastavené prahové tempo/LTHR/zóny, kroky dorazí na hodinky bez cíle a API nic nehlásí. Bez kontroly nastavení athletea před pushem bude push „fungovat“, ale bude k ničemu (§3.2).
- Tepové zóny: intervals.icu převádí `Z2 HR` podle **zón athletea v intervals.icu**, ne podle zón v PeakForm. Absolutní tep (bpm) intervals.icu formát nepodporuje. Je potřeba rozhodnout, čí zóny platí (§5, otázka 1).
- RPE jako cíl kroku Garmin ani intervals.icu nepodporují. Lze ho poslat jen jako text kroku.

## 2. Model tréninku v Garminu

### 2.1 Struktura

- **Trénink → (segmenty pro multisport) → kroky.** Krok je buď *spustitelný krok*, nebo *opakovací skupina* s vlastními kroky. [RE: [python-garminconnect workout.py](https://github.com/cyberjunky/python-garminconnect/blob/master/garminconnect/workout.py)] Ve FIT souboru jsou kroky plochý seznam a opakování je krok, který ukazuje zpět na první krok bloku. [oficiální: [FIT workout file](https://developer.garmin.com/fit/file-types/workout/)]
- **Vnořené opakování** Garmin umí (model je rekurzivní, v Garmin Connect lze přetáhnout repeat do repeatu). Maximální hloubka není zdokumentovaná, počítejme se 2 úrovněmi. [RE, komunita]
- **Typ kroku (intenzita):** warmup, cooldown, interval, active (v Garmin Connect „main“), recovery, rest, other. Na hodinkách určuje popisek kroku a u opakování „do splnění“ se počítají jen aktivní kroky. [oficiální FIT `intensity`; RE `stepTypeId` 1–8]
- **Název a poznámky:** trénink má název a popis, každý krok může mít název a poznámku, kterou hodinky zobrazí. Délkové limity nejsou zdokumentované (na fóru se zmiňuje 212 znaků poznámky). Držme název ≤ ~32 znaků a poznámky krátké. [oficiální pole FIT; limity komunita]

### 2.2 Jak krok končí (délka kroku)

- Běžné: **čas**, **vzdálenost**, **otevřený krok do stisku Lap**, kalorie. [oficiální FIT `wkt_step_duration`]
- Podmíněné: tep pod/nad hodnotou, výkon pod/nad hodnotou (i 3s/10s/30s/lap průměry), TSS. [oficiální]
- Plavání: pevný odpočinek, „repeat on“ interval (např. 5×100 na 2:00). Síla: **opakování (reps)**. [oficiální]
- Krok má **právě jednu** koncovou podmínku. Čas a vzdálenost zároveň nejdou.
- Opakovací skupina: pevný počet opakování, případně „opakuj dokud“ (čas, vzdálenost, tep, výkon…). [oficiální]

### 2.3 Cíle intenzity

| Cíl | Varianty v Garminu |
|---|---|
| Tep | zóna (číslo) nebo vlastní rozsah v bpm nebo % max TF. %LTHR/%HRR Garmin nemá, posílá se jako bpm. |
| Tempo / rychlost | zóna nebo vlastní rozsah (interně m/s) |
| Výkon | zóna, rozsah ve W nebo %FTP; i 3s/10s/30s/lap varianty |
| Kadence | rozsah rpm/spm |
| Ostatní | sklon, odpor (trenažér), plavecký styl, CSS offset |
| Žádný cíl | „No target“ |

- Krok může mít **druhotný cíl** (hlavně cyklistika a plavání). [oficiální FIT `secondary_target_*`]
- Garmin očekává **rozsah**, ne jednu hodnotu. Integrátoři kolem jedné hodnoty dělají ±2,5–5 %. [komunita: Terra, intervals.icu]
- RPE jako cíl neexistuje.

### 2.4 Sporty

Strukturovaný trénink jde vytvořit pro: běh, kolo, bazénové plavání, sílu, kardio, jógu, pilates, HIIT, mobilitu a obecný „Custom/Other“. [oficiální [FIT cookbook](https://developer.garmin.com/fit/cookbook/encoding-workout-files/); RE `sportTypeId`]

- **Plavání:** jen bazén, povinná délka bazénu, aktivní kroky na vzdálenost. Lze zadat styl (kraul, znak, prsa, motýlek, drill, IM…) a pomůcky (ploutve, prkno, packy, piškot, šnorchl). Open water strukturovaný trénink nepodporuje. [oficiální; komunita: Tredict]
- **Síla / kardio / jóga / pilates:** cviky z pevného katalogu Garminu (kategorie + název, např. SQUAT/GOBLET_SQUAT), opakování, váha. Neznámý název cviku Garmin zahodí. [oficiální FIT exercise_category; komunita: Terra]

### 2.5 Kalendář a doručení na hodinky

- Garmin rozlišuje **trénink v knihovně** a **naplánování na datum**. Změna obsahu se v Training API dělá smazáním a znovuvytvořením. [RE; komunita: Terra]
- Naplánovaný trénink se na hodinky dostane při příští synchronizaci a zobrazí se v tréninkovém kalendáři hodinek. Zůstane tam, ať ho athlete splní, nebo ne. [oficiální manuál Forerunner 265]
- Na hodinkách: krok za krokem, poznámka kroku, cíl, upozornění mimo cíl, otevřené kroky se ukončí tlačítkem Lap. [oficiální manuál]
- Dokončená aktivita nemá odkaz na naplánovaný trénink, párování musíme dál dělat podle data a sportu (to už dělá `PlanVsActualService`). [komunita: Terra]

### 2.6 Limity

- **50 kroků na trénink** v Garmin Connect (chyba „Workout exceeds maximum expected number of 50 steps“), novější zařízení až 100. Opakovací skupina se počítá jako 1 krok + její kroky, ne rozbaleně. [komunita, potvrzeno vývojářem intervals.icu ve [vlákně 1521](https://forum.intervals.icu/t/1521/121)]
- Počet tréninků uložených v hodinkách je omezený (fēnix 6: 25). [komunita]
- Starší zařízení (Forerunner 235, Vivoactive 3) strukturované tréninky nepřijímají (viz `integrations-research.md` §6).

### 2.7 Oficiální Garmin Training API

Specifikace (partnerské PDF „Garmin Connect Developer Program – Training API“) je důvěrná. Veřejná je jen [přehledová stránka](https://developer.garmin.com/gc-developer-program/training-api/). Program je „pro firemní použití“, odpověď na žádost do 2 pracovních dnů, bez licenčních poplatků, integrace typicky 1–4 týdny ([FAQ](https://developer.garmin.com/gc-developer-program/program-faq/)). Kdybychom jednou šli přímo, model kroků odpovídá FIT výčtům výše. Pro teď platí rozhodnutí z `garmin-calendar-push.md`: jdeme přes intervals.icu.

## 3. Co z toho projde přes intervals.icu

Pushujeme text tréninku do pole `description` eventu (`POST /api/v1/athlete/0/events`, `category: WORKOUT`). intervals.icu ho rozparsuje a odešle do Garmin Connect. Hlavní zdroj: [Workout builder syntax – quick guide](https://forum.intervals.icu/t/workout-builder-syntax-quick-guide/123701).

### 3.1 Syntaxe kroků

```
Rozklus
- 15m Z2 HR intensity=warmup

Hlavní část 6x
- 5m Z4 HR intensity=interval
- Klus 2m Z1 HR intensity=recovery

- 10m Z1 HR intensity=cooldown
```

- Krok je řádek začínající `-`: `[text] [délka] [cíl] [kadence] [příznaky]`. Text před první délkou/cílem se na hodinkách ukáže jako poznámka kroku.
- **Délka:** `30s`, `10m`, `1h`, `1h2m30s`, `5'`, `30"`. **Vzdálenost:** `400mtr`, `2km`, `1mi`. Pozor, **`m` znamená minuty**, metry se píšou `mtr`.
- **Do stisku Lap:** `- Press lap Z1 HR`. Funguje jen pro Garmin. [oficiální]
- **Typ kroku:** `intensity=warmup|cooldown|active|interval|recovery|rest|other`. Bez něj intervals.icu typ odhaduje (téměř vše „interval“). Nadpis „Warmup“ sám o sobě typ nenastaví. **Posílat `intensity=` u každého kroku.** Známá chyba: `active` a `interval` se na běžeckém tréninku zobrazí jako „Other“. [oficiální, [vlákno 120462](https://forum.intervals.icu/t/120462)]
- **Opakování:** řádek `6x` nebo nadpis s `6x`, před a za blokem prázdný řádek. **Vnořené opakování intervals.icu nepodporuje** (Garmin ano). [oficiální]
- Rampy (`ramp 50-75%`) Garmin neumí. Náš model je nemá, nevadí.
- Řádek bez `-` je nadpis. První odstavec se stane popisem tréninku.

### 3.2 Cíle

| Cíl | Zápis | Co musí mít athlete nastavené v intervals.icu |
|---|---|---|
| Tepová zóna | `Z2 HR`, `Z2-Z3 HR` | HR zóny pro daný sport |
| % max TF / % LTHR | `70-80% HR`, `95% LTHR` | max TF / LTHR |
| Absolutní tep | **nepodporováno** (`140-150bpm` je jen text) | — |
| Absolutní tempo | `4:30-4:50/km Pace` | **prahové tempo** (jinak se cíl tiše zahodí) |
| Tempo zóna / % prahu | `Z2 Pace`, `80-85% Pace` | prahové tempo + zóny |
| Výkon | `250w`, `240-260w` | nic |
| %FTP / výkonová zóna | `75%`, `Z3` | FTP |
| Kadence | `90rpm`, `85-95rpm` | nic |
| RPE | **nepodporováno** | — |

- Při exportu do Garminu intervals.icu vše převede na absolutní rozsahy: tep ±1,5 %, tempo ±2,5 % (5:30/km → 2,955–3,106 m/s), výkon ±2,5 % venku. Zóny tedy na hodinky nedorazí jako „Garmin Z2“, ale jako rozsah bpm spočítaný ze zón v intervals.icu. [oficiální]
- **Bez nastaveného prahového tempa dorazí běžecké kroky bez cíle, i s absolutním tempem.** API vrátí 200 a `workout_doc` vypadá správně. [vyřešeno ve [vlákně 130706](https://forum.intervals.icu/t/130706)]
- Pole eventu `target` (`AUTO`/`POWER`/`HR`/`PACE`) určuje, podle čeho hodinky trénink řídí. Míchat v jednom běžeckém tréninku kroky s tepem a s tempem je riskantní. [oficiální; komunita]

### 3.3 Sporty přes intervals.icu

| PeakForm `SportType` | intervals.icu `type` | Výsledek na Garminu |
|---|---|---|
| Running | `Run` | plná podpora |
| Cycling | `Ride` (`VirtualRide` pro trenažér) | plná podpora |
| Swimming | `Swim` | jen částečně: nutný řádek `Pool length: 25m`, styly nejsou pole (jen náhodně z textu), odpočinky nespolehlivé. Brát jako best effort. |
| Strength | `WeightTraining` | **jen kroky na čas.** Cviky, opakování ani váhy neprojdou, kroky bez času se zahodí. |
| CrossTraining / Other | `Workout` / `Other` [neověřeno] | jen kroky na čas/vzdálenost |
| Rest | nepushovat | — |

### 3.4 Chování kalendáře a API

- **Okno ~7 dní dopředu**, pevné. Vzdálenější tréninky se nahrají, až se dostanou do okna. Úpravy se nahrají znovu automaticky. [oficiální, [vlákno 1521](https://forum.intervals.icu/t/upload-planned-workouts-to-garmin-connect/1521)]
- Tréninky nahrané z intervals.icu nejde v Garmin Connect upravovat. Garmin to blokuje. [komunita]
- Smazání v intervals.icu → zmizí z Garminu? **Neověřeno.** Nejspíš ano, synchronizace spravuje sadu na příští týden. Otestovat. U Corosu se mazání nepropisuje.
- Jeden integrátor po opravě nastavení musel event smazat a vytvořit znovu, aby se znovu exportoval. [komunita]
- Endpointy:
  - `POST /events` vytvoří jeden event.
  - `PUT /events/{id}` upraví jeden event.
  - **`POST /events/bulk?upsert=true`** vytvoří nebo upraví podle `external_id`. Páruje jen eventy vytvořené naší OAuth aplikací.
  - `PUT /events/bulk-delete` s `[{"external_id": "…"}]` smaže podle `external_id`. Neexistující tiše přeskočí.
- Užitečná pole eventu:
  - `external_id` (= `PlannedWorkout.Id`)
  - `target`
  - `indoor`
  - `athlete_cannot_edit`, `structure_read_only` (athlete nepřepíše trenérovu strukturu)
  - **`push_errors[]`**: chyby exportu do Garminu, číst zpět po uložení [neověřeno, že se tam objeví i Garmin chyby]
- Posílat strukturu přímo jako JSON (`workout_doc`) **nefunguje spolehlivě**. Export pak měl 0 kroků a moderátor doporučuje `description`. Soubory (.zwo/.fit) se stejně převedou na text a ztratí tep nebo opakování. **Text v `description` je jediná spolehlivá cesta.** [komunita, [vlákno 132326](https://forum.intervals.icu/t/132326)]

## 4. Porovnání s naším modelem (`WorkoutSegment`)

| Schopnost | Garmin | intervals.icu text | PeakForm dnes | Akce |
|---|---|---|---|---|
| Krok na čas / vzdálenost | ✓ | ✓ | ✓ (`DurationSeconds`, `DistanceMeters`) | Garmin bere jen jedno. Editor dnes dovolí obojí → validovat „právě jedno“, nebo pravidlo přednosti. |
| Krok do stisku Lap | ✓ | ✓ `Press lap` | ✗ (úsek bez délky nic neznamená) | Úsek bez času i vzdálenosti exportovat jako `Press lap`, v editoru to pojmenovat. |
| Typ kroku | 7 typů | `intensity=` | `WorkoutSegmentType` | Mapovat (níže). Přidat typ **Recovery** (aktivní klus mezi intervaly ≠ Rest). |
| Opakování jednoho kroku | ✓ | ✓ | ✓ `RepeatCount` | Exportovat jako blok `Nx` s jedním krokem. |
| **Opakovaný blok více kroků** | ✓ (i vnořený) | ✓ (bez vnoření) | **✗** | **Rozšířit model** (§5): skupina s `RepeatCount` a podřízenými úseky, jedna úroveň. |
| Cíl: tepová zóna | ✓ | ✓ `Z2 HR` | ✓ `TargetHeartRateZoneNumber` | Platí zóny v intervals.icu, ne naše (§5, otázka 1). |
| Cíl: tep v bpm | ✓ | ✗ | ✗ | Nepřidávat. |
| Cíl: tempo rozsah | ✓ | ✓ `/km Pace` | ✓ min/max | Vyžaduje prahové tempo v intervals.icu. |
| Cíl: výkon | ✓ | ✓ `w` | jen jedna hodnota `TargetPowerWatts` | Stačí (intervals.icu dopočítá rozsah). Volitelně přidat max. |
| Cíl: RPE | ✗ | ✗ | ✓ | Exportovat jako text kroku („RPE 6“) bez cíle. Trenéra v UI upozornit. |
| Kadence | ✓ | ✓ | ✗ | Zatím ne, případně později jako druhotný cíl. |
| Poznámka kroku | ✓ | ✓ (text před délkou) | ✓ `Notes` | **Sanitizovat**: text s čísly a jednotkami („2m klus“) by parser vzal jako délku nebo cíl. |
| Plavání: bazén, styl, pomůcky | ✓ | částečně | ✗ | Mimo MVP. |
| Síla: cviky, opakování, váha | ✓ | ✗ | ✗ | Mimo MVP přes intervals.icu (neprojde). |
| Limit 50 kroků | ✓ | ✓ | nehlídá se | Varovat v editoru (počítat opakovací blok jako 1 + kroky). |

**Mapování typů úseků → `intensity=`:**

| `WorkoutSegmentType` | `intensity=` | Poznámka |
|---|---|---|
| WarmUp | `warmup` | |
| CoolDown | `cooldown` | |
| Main | `active` | Kvůli chybě „Other“ zvážit `interval`. Obojí má stejný problém, ověřit živě. |
| Interval | `interval` | |
| Rest | `rest` | |
| *(nový)* Recovery | `recovery` | |
| Strides, Drill | `interval` / `active` + text kroku („Stupňované úseky“, „Abeceda“) | |
| Repeat | — | V plochém modelu nedává smysl. Po zavedení skupin se z něj stane typ skupiny. |

**Mapování ostatních polí:**

- `PlannedWorkout.Title` → `name`
- `CoachDescription` → první odstavec `description`, před strukturou. Sanitizovat řádky začínající `-` a nadpisy s `Nx`, jinak je parser vezme jako kroky.
- `Date` → `start_date_local`
- `Sport` → `type` (§3.3)
- `IsRestDay` → nepushovat
- Trénink bez úseků → jen popis. intervals.icu vytvoří event bez struktury a na hodinky přijde jen jako poznámka v kalendáři [neověřeno].

## 5. Doporučení a otevřené otázky

**Navrhované změny modelu (před implementací pushe):** — *body 1–4 implementovány 2026-10-05 (migrace `AddWorkoutSegmentRepeatBlocks`, editor v `frontend/src/workouts/segments/`).*

1. **Opakovací skupiny:** `WorkoutSegment.ParentSegmentId` (nullable). Úsek typu `Repeat` nese `RepeatCount` a jeho děti jsou kroky bloku. Validace: jen jedna úroveň (limit intervals.icu), skupina nemá vlastní délku ani cíl. `PlanVsActualService.PlannedZones` musí násobit děti počtem opakování rodiče. Editor dostane „Přidat blok opakování“.
2. **Typ Recovery** do `WorkoutSegmentType`.
3. **Pravidlo délky:** právě jedno z čas/vzdálenost, nebo nic = do stisku Lap.
4. **Počítadlo kroků** v editoru s varováním nad 50.

Tyto změny dávají smysl i bez Garminu: trenér dnes intervalový trénink se šlapanými pauzami zapsat neumí.

**Builder `description`** — *implementováno 2026-10-05:* `TrainCoach.Integrations/IntervalsIcu/IntervalsIcuWorkoutDescriptionBuilder.cs`, testy `IntervalsIcuWorkoutDescriptionBuilderTests`. Vrací `name`, `type`, `description`, `target` (převažující typ cíle, při remíze `AUTO`), `moving_time` (jen bez struktury) a seznam varování (RPE jako text, > 50 kroků, plavání bez délky bazénu, síla/ostatní jen časové kroky). Příklad výstupu:

```
- 15m Z2 HR intensity=warmup

6x
- 1km Z4 HR intensity=interval
- Klusem 2m intensity=recovery

- 10m intensity=cooldown
```

Sanitizace volného textu (popis trenéra, poznámky kroků): úvodní `-` → `•`, `6x` → `6×`, `Z4` → `zóna 4`, číslo s jednotkou se oddělí mezerou (`2m` → `2 m`, `80%` → `80 %`). **Neověřeno živě**, zda intervals.icu mezerou oddělené `2 m` opravdu nebere jako délku — přidat do živého testu.

**Kontrola nastavení athletea před pushem:** z intervals.icu načíst sportovní nastavení athletea (prahové tempo, LTHR, max TF, HR zóny, FTP). Když cíl tréninku potřebuje něco, co chybí, ukázat trenérovi varování („Tempo se na hodinky nepropíše — athlete nemá v intervals.icu nastavené prahové tempo“). Push kvůli tomu neblokovat.

**Živý test 2026-10-05 (tepové cíle, reálný účet athletea).** Testovací event na 19. 10., tedy mimo 7denní okno pro Garmin, se po vyhodnocení hned smazal (DELETE → 200). Výsledky:

| Krok v `description` | Jak ho intervals.icu rozparsovalo (`workout_doc`) |
|---|---|
| `- 10m 73.7-78.9% HR` | `hr: {start: 73.7, end: 78.9, units: "%hr"}`: **desetinná procenta projdou přesně** |
| `- 10m 74-79% HR` | `hr: {start: 74, end: 79, units: "%hr"}` |
| `- 5m 75% HR` | `hr: {value: 75, units: "%hr"}` |
| `- 3m 140-150bpm` | **žádný cíl**, text kroku také zmizel (potvrzeno: absolutní bpm nejde) |
| `- 5m Z2 HR` | `hr: {value: 2, units: "hr_zone"}` |
| `- zóna 4 tempo, 2 m klus mezi 5m Z4 HR` | text `zóna 4 tempo, 2 m klus mezi`, délka 300 s, cíl Z4: **sanitizace funguje**, `2 m` se jako délka nebere |
| `- Klusem 2 m volně 3m` | text `Klusem 2 m volně`, délka 180 s |

- **Nastavení athletea (max TF, LTHR, zóny) nejde přečíst:** `GET /athlete/0` i `/athlete/0/sport-settings` vracejí `403 SETTINGS:READ scope required`.
- `?resolve=true` nevrátil přepočtené bpm (`_hr` prázdné). Nevíme proto, jestli athlete má v intervals.icu nastavenou max TF a na jaké bpm se procenta převedou. Ověří se až po přidání `SETTINGS:READ`.

**Živý test 2026-10-06 (testovací šablona → intervals.icu → Garmin Connect).** Trénink dorazil do kalendáře Garmin Connect, ale **bez kroků**, jen s popisem. Diagnostika (event přečtený z intervals.icu):
- Text, typy kroků (`intensity=`), bloky, zóny i upravené poznámky se rozparsovaly přesně podle plánu.
- **Kroky `Press lap` bez délky intervals.icu tiše zahodilo.** Ověřeno experimentem: s délkou (`- Pauza Press lap 1m`) se krok zachová jako `until_lap_press: true` a délka je jen odhad. Builder proto posílá `Press lap 1m`. Jestli hodinky krok ukončí až tlačítkem Lap, nebo už po minutě, zatím neověřeno.
- **V nastavení běhu v intervals.icu chybí prahové tempo** (`threshold_pace` prázdné). Podle [vyřešeného vlákna na fóru](https://forum.intervals.icu/t/structured-run-workouts-don-t-appear-on-garmin-connect-correctly/125954) je to příčina „v Garmin Connect jen poznámky, žádné kroky“ u běhů s cíli v tempu. Athlete ho musí nastavit, nebo ho PeakForm začne zapisovat.

**Výsledek na hodinkách (2026-10-06, po nastavení prahového tempa):**
- Struktura dorazila správně: kroky, bloky, tepové zóny i tempo.
- Aplikace Garmin Connect u tréninků z intervals.icu kroky nezobrazuje, je to omezení aplikace.
- RPE (jen text) a výkon při běhu hodinky jako cíl neukázaly. Výkon nahlásí builder varováním `RunPowerMayNotShow`.
- Jedna hodnota tempa (5:00/km) se na hodinkách ukáže jako rozsah 4:53–5:08, protože intervals.icu přidává ±2,5 %. Kdo chce užší okno, zadá rozsah.

**Prahové tempo z PeakForm (2026-10-06):** `AthleteProfile.ThresholdPaceSecondsPerKm` se edituje na stránce Tepové zóny (`GET/PUT /api/athletes/{id}/thresholds`). Zapisuje se stejnou synchronizací jako zóny (`TrainingSettingsSyncJob`) do `threshold_pace` jako rychlost v m/s (4:30/km → 3,7037). Jednotka m/s je převzatá z `workout_doc` a **při prvním zápisu je potřeba ji v intervals.icu zkontrolovat**.

**Rozhodnutí o tepových cílech (2026-10-05, trenér):** zdrojem pravdy jsou zóny v PeakForm. PeakForm je při uložení zapíše do intervals.icu a tréninky posílají jen `Z{n} HR`. intervals.icu je pak na hodinkách převede na rozsah bpm podle našich zón. *Implementováno 2026-10-05:*
- **Kdy:** po uložení zón (`HeartRateZoneService.SetZonesAsync`) a po (znovu)připojení účtu (`IntegrationConnectionService.UpsertConnectionAsync`) se zařadí `TrainingSettingsSyncJob`. Ten zapíše sadu zón platnou dnes. Sadu s budoucím datem platnosti zapíše až další uložení nebo připojení.
- **Jak:** `PUT /api/v1/athlete/0/sport-settings/Run?recalcHrZones=false` s tělem `{hr_zones: [horní hranice], hr_zone_names, max_hr: poslední hranice}`.
  - PUT je částečný, ostatní nastavení se nemění.
  - `max_hr` musí být rovno poslední hranici, jinak ho intervals.icu přepíše.
  - `recalcHrZones=false` zabrání přepočtu zón podle LTHR.
  - Chování převzato z živě ověřeného [intervals-icu-mcp PR #139](https://github.com/hhopke/intervals-icu-mcp/pull/139). **Na našem účtu zatím neověřeno**, protože athlete se musí nejdřív znovu připojit.
- **Oprávnění:** `SETTINGS:WRITE` přidáno do `IntervalsIcuIntegrationProvider.Scopes`. Bez něj job uloží chybu „athlete musí znovu připojit účet“.
- **Stav:** `IntegrationConnection.HeartRateZonesSyncedAtUtc` / `HeartRateZonesSyncError`. Zobrazuje se na kartě intervals.icu v Integracích, při chybě i s tlačítkem „Znovu připojit“.
- **Omezení:** zóny se zapisují jen do nastavení pro **běh** (`Run`, v intervals.icu pokrývá i trail a virtuální běh). Pro kolo a další sporty platí dál zóny athletea v intervals.icu. PeakForm má jednu sadu zón pro všechny sporty.

**Otevřené otázky:**

1. **Čí tepové zóny?** `Z2 HR` se vyhodnotí podle zón v intervals.icu. Ty se mohou lišit od zón v PeakForm, které počítají plán vs. skutečnost. Varianty:
   - (a) Spolehnout se na zóny v intervals.icu a zobrazit trenérovi rozdíl.
   - (b) Převést naši zónu na `% HR` z maxima athletea.
   - (c) Před pushem synchronizovat naše zóny do intervals.icu. Vyžaduje zápis do nastavení athletea, dnes nemáme scope.
2. **Automaticky, nebo tlačítkem?** Platí otázka z `garmin-calendar-push.md` §11. Fakt, že úpravy se nahrají znovu samy a Garmin pushnuté tréninky nedovolí upravit, mluví spíš pro automatický push s přepínačem na straně athletea.
3. **Hlavní metrika tréninku (`target`):** odvodit z převažujícího typu cíle úseků (HR/PACE/POWER), jinak `AUTO`.
4. Živě ověřit (rozšíření kroku 6 v `garmin-calendar-push.md` §10):
   - mazání → zmizí z Garminu?
   - update přes `bulk?upsert` → nový export?
   - obsah `push_errors`
   - zobrazení `intensity=active` vs. `interval` na běhu
   - chování tréninku bez struktury
   - ztráta cíle bez prahového tempa
