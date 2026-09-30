# Import historie ze Strava archivu (export dat)

Sportovec může naimportovat celou historii aktivit z exportu dat Stravy („Download your data“ / GDPR export),
stejně jako to umí intervals.icu. Doplňuje to živý Strava sync, který stahuje jen poslední aktivity
(max. 100 od posledního syncu, první sync 30 dní zpět).

## Proč archiv, a ne Strava API

- Export jsou **vlastní data sportovce**, ne data získaná přes Strava API, takže se na ně nevztahují pravidla
  API Agreement (povinnost smazat data při odpojení, zákaz použití pro AI/ML – viz `docs/integrations-research.md` §1).
  Právní posouzení tohoto výkladu zatím neproběhlo.
- Obsahuje i aktivity, které přes intervals.icu nedostaneme (intervals.icu pro aktivity pocházející ze Stravy
  vrací přes API jen prázdné záznamy).
- Nečerpá limity Strava API.

## Formát archivu

Ověřeno na reálném exportu (české nastavení účtu, 3 490 aktivit, 135 MB):

| Část | Obsah |
|---|---|
| `activities.csv` | 1 řádek na aktivitu, ~100 sloupců. **Hlavičky, datum a typ aktivity jsou lokalizované** podle jazyka účtu. |
| `activities/` | Soubor ke každé aktivitě: `.fit.gz` (většina), `.gpx`, `.gpx.gz`, `.tcx.gz`. |
| ostatní | `messaging.json`, `followers.csv`, `media/`, `logins.csv`, … – importér je **nikdy nečte**. |

Vlastnosti `activities.csv`:

- Sloupce 0–14 obsahují hodnoty pro zobrazení v jednotkách a formátu účtu (vzdálenost `"33,22"` km), sloupce od 15
  strojové hodnoty (s, m, m/s, desetinná tečka). Několik hlaviček je proto dvakrát (`Uplynulý čas`, `Vzdálenost`,
  `Maximální tepová frekvence`). Parser vždy bere **poslední výskyt**.
- `ID aktivity` je stejné Strava activity id, jaké vrací API. Číslo v `Název souboru` je jiné (upload id).
- `Datum aktivity` je v UTC, jen naformátované podle jazyka (`6. 9. 2026 11:18:40`, `Sep 6, 2026, 11:18:40 AM`).
  Na reálném archivu se shodovalo s FIT `session.start_time` u všech aktivit.
- Popisy aktivit mohou obsahovat nové řádky v uvozovkách (RFC 4180).

Sloupce se hledají podle tabulky aliasů cs/en (`StravaActivitiesCsvReader`). Když se nenajdou, použije se jejich
pevná pozice v exportu. U jiných jazyků účtu se proto spoléháme na pozice sloupců – ověřeno zatím jen s češtinou
a (syntetickou) angličtinou.

## Odkaz z e-mailu

Tlačítko „Download Archive“ v e-mailu vede přes adresu pro sledování kliknutí `https://email.strava.com/ls/click?upn=…`,
která přesměruje (302) na podepsanou S3 URL pro GET:
`https://s3.amazonaws.com/strava.portability/athlete/{athleteId}/export/live/export_{athleteId}.zip?X-Amz-…`

- Platí **7 dní** (`X-Amz-Expires=604800`). Po vypršení vrací `403` s `<Message>Request has expired</Message>` a
  uživatel dostane srozumitelnou hlášku.
- Podpis platí jen pro GET, takže HEAD vrací `403`.
- Nevyžaduje přihlášení. Je to tedy **tajný údaj**: neukládá se do DB ani do logu, do úlohy na pozadí se předává jen
  v paměti.

## Tok

1. `POST /api/integrations/strava/archive-imports/link` (odkaz) nebo `/upload` (multipart ZIP) → záznam
   `StravaArchiveImport` ve stavu `Pending`, odpověď 202.
2. Úloha na pozadí (`StravaArchiveImportJob.AnalyzeAsync`):
   1. `Downloading`: stáhne ZIP do dočasného úložiště (`IStravaArchiveFileStore`, konfigurace
      `StravaArchiveImport:StoragePath`, výchozí `%TEMP%/peakform/strava-archives`).
   2. Ověří vlastníka: athlete id z URL nebo z názvu `export_{id}.zip` musí odpovídat `ExternalAccountId` připojené
      Stravy, pokud je připojená.
   3. `Analyzing`: suchý běh každé aktivity přes `IActivityIngestionService.PreviewAsync`. Výsledkem je stav
      `PreviewReady` s počty (nové, sloučí se, ke kontrole, už existuje, nečitelné řádky).
3. Uživatel potvrdí (`/{id}/confirm`) → `Importing` → `ImportAsync` → `Succeeded`. Archiv se smaže.
   Zrušit (`/{id}/cancel`) lze před potvrzením. Import samotný zrušit nejde: opakování je bezpečné a zastavení v půlce
   by nechalo jen matoucí částečný výsledek.

Uživatel může mít nejvýš jeden aktivní import. Klient se dotazuje na `GET /{id}` (frontend jen po dobu běhu úlohy).

## Mapování a deduplikace

- Každý řádek se převede na `ExternalActivity` stejně jako v `StravaIntegrationProvider`:
  - `Source = Strava`, `ExternalId = ID aktivity`,
  - trvání = aktivní čas, tempo z průměrné rychlosti,
  - sport podle lokalizovaného typu (`StravaSportTypeMapper`, stejné cílové hodnoty jako `MapSport` v API adaptéru).

  Díky tomu funguje **level-1 dedup s API syncem v obou směrech**: aktivita už stažená přes API se z archivu
  přeskočí a obráceně.
- Záznam má `ActivitySourceRecord.StravaArchiveImportId`, podle kterého se archivní data odliší od dat z API.
- `RawPayloadJson` obsahuje celý CSV řádek (popis, vybavení, relative effort, …), protože u vlastních dat žádné
  omezení uchovávání neplatí.
- Z FIT souboru se čte jen `file_id`. Z něj vzniká `FitFileUuid = fit:{manufacturer}:{serial}:{time_created}`, klíč
  pro level-3 shodu. Dekódování končí hned po první zprávě, což zkrátí čtení z ~160 s na ~4 s pro 3 500 souborů.
  Celý soubor se dekóduje, jen když CSV nezná sport nebo datum. Sport a čas z GPX/TCX slouží také jen jako záloha.
- Deduplikace probíhá stejnou pipeline jako živý sync (`ActivityIngestionService`, vyčleněná ze `SyncOrchestrator`).
  Použije se efektivní `ConnectorDomainPolicy` pro Strava/Activities: při připojeném intervals.icu je to
  `FallbackOnly`, takže archiv nepřepíše hodnoty z intervals.icu. `Disabled` se bere jako `FallbackOnly`, protože
  import je výslovná akce uživatele.
- Nové pravidlo matcheru, které platí i pro živý sync: kandidát, který už má zdrojový záznam **stejného zdroje**,
  se nesloučí. Jeden poskytovatel nikdy nehlásí stejnou aktivitu dvakrát, takže například dvě posilování téhož
  odpoledne na Stravě zůstanou dvě aktivity.
- Import zpracovává dávky po 50 aktivitách, každou ve vlastním DI scope (vlastní DbContext). Když dávka narazí na
  konflikt unikátního indexu (souběžný sync), zpracuje se znovu po jedné aktivitě.

## Bezpečnost

- **Ochrana proti SSRF** (`StravaArchiveLink`): povolená je jen HTTPS adresa na výchozím portu, a to buď
  `email.strava.com/ls/click…`, nebo S3 (`s3[.-region].amazonaws.com`) s cestou
  `/strava.portability/athlete/{id}/….zip`. Přesměrování se sledují ručně (`AllowAutoRedirect = false`, nejvýš 3)
  a každý krok se znovu kontroluje. Přesměrování jinam se odmítne.
- **ZIP se nikdy nerozbaluje na disk.** Otevírají se jen `activities.csv` a soubory, na které CSV odkazuje, a to podle
  názvu položky v ZIPu, ne jako cesta v souborovém systému (proto nehrozí zip-slip). Každá položka se čte přes
  stream s limitem velikosti po dekompresi (CSV 200 MB, soubor aktivity 64 MB) jako ochrana proti zip bombám.
- GPX a TCX se čtou přes `XmlReader` s `DtdProcessing.Prohibit` (ochrana proti XXE).
- Maximální velikost archivu je `StravaArchiveImport:MaxArchiveBytes` (výchozí 4 GB). Pokud aplikace běží za reverse
  proxy, musí ten stejný limit povolit i proxy (např. `client_max_body_size`).
- Import je jen pro vlastní účet. Cizí import vrací 404.

## Odolnost

Fronta úloh běží v paměti procesu (ADR 0003), takže úlohy se po restartu ztratí.
`StravaArchiveRecoveryHostedService` proto:

- **při startu** označí importy ve stavech `Pending`, `Downloading`, `Analyzing` a `Importing` jako `Failed`
  („spusťte znovu“, opakování je bezpečné díky level-1 dedup),
- **každých 6 h** zruší náhledy, které nikdo nepotvrdil do `RetentionHours` (48 h), a smaže osiřelé archivy.
  Archivy aktivních importů přeskočí.

## Výkon (reálný archiv, 3 490 aktivit, testovací host se SQLite)

Nahrání 135 MB trvá ~7 s, náhled ~10 s a import ~110 s. Výsledek: 3 490 vytvořených aktivit, 0 chyb.

## Streamy a mapa (fáze 2)

Import ukládá z každého souboru aktivity průběhová data do `ActivityStream`: čas, tep, výkon, kadenci, vzdálenost,
výšku, rychlost, **GPS** a teplotu. Detail aktivity z nich kreslí grafy a mapu trasy bez volání poskytovatele.

- **Čtení** (`ActivityFileProbe.ReadStream`): FIT `record` (poloha v semicircles → stupně), GPX `trkpt` včetně
  rozšíření `TrackPointExtension` (hr, cad, atemp) a TCX `Trackpoint` (včetně `Speed`/`Watts`/`RunCadence`).
  `ActivityStreamBuilder` vzorky seřadí, odstraní duplicitní časy a pozice (0, 0). Vzdálenost dopočítá z GPS
  (haversine) a rychlost ze vzdálenosti, pokud je soubor nemá.
- **Zmenšení** (`ActivityStreamDownsampler`): nejvýš 2 000 bodů. Časový rozsah se rozdělí na stejné úseky a každý
  neprázdný úsek dá jeden bod. Tep, výkon, kadence, rychlost a teplota se v úseku průměrují. Poloha, výška a
  vzdálenost se berou z jednoho vzorku nejblíž středu úseku, takže trasa nezkracuje zatáčky. První a poslední
  bod zůstávají vždy. Pauzy zůstanou mezerou v čase, protože prázdné úseky žádný bod nedají. Všechny kanály jsou
  zarovnané podle indexu, takže najetí myší na graf v bodě i odpovídá poloze na mapě v bodě i.
- **Formát** (`ActivityStreamCodec`, `FormatVersion = 1`): sloupcový zápis, celá čísla s měřítkem (GPS 1e-6°,
  výška a vzdálenost v dm, rychlost v mm/s), delta + zigzag varinty a bitmapa chybějících hodnot, vše
  komprimované Brotli (`Optimal`). Na reálném archivu (3 477 streamů) to je **11 MB celkem, ~3 KB na aktivitu**.
  Kódování všech streamů trvá 1,4 s a přesnost GPS je lepší než 0,1 m. Úroveň `SmallestSize` ušetří 12 %, ale je
  35× pomalejší.
- **Doplnění ke stávajícím aktivitám:** stream se uloží ke zdrojovému záznamu (`Source = Strava`, `ExternalId`)
  bez ohledu na výsledek deduplikace. Opakovaný import stejného archivu tak doplní grafy a mapy i k už existujícím
  aktivitám, včetně těch stažených živě přes API. Náhled ukazuje odhad `PreviewStreamsToAdd`: řádky se souborem,
  jejichž záznam stream ještě nemá.
- **Poskytování:** `GET /api/activities/{id}/streams` vrací uložený stream z kteréhokoli zdrojového záznamu
  aktivity (primární má přednost), jinak stahuje živě jako dřív. Sklon se počítá při čtení přes posuvné okno
  30 m a tempo z rychlosti (pod 0,5 m/s se nezobrazí). Kadenci běhu, kterou soubory i Strava API uvádějí za jednu
  nohu, API vrací jako kroky za minutu (×2), u uložených i živých dat.
- **Live Strava sync** streamy dál neukládá (podmínky Strava API).
- **Mapa:** Leaflet + dlaždice OpenStreetMap. Dlaždice jsou ztmavené CSS filtrem, protože aplikace je jen tmavá.
  OSM odmítá požadavky bez hlavičky Referer, zatímco nginx posílá `Referrer-Policy: no-referrer`, proto má
  vrstva dlaždic vlastní `referrerPolicy`. Pravidla používání OSM dlaždic nepovolují větší provoz; pro produkci
  s více uživateli je potřeba jiný poskytovatel (URL dlaždic je konstanta v `ActivityMap.tsx`). Mapu vidí každý,
  kdo smí vidět aktivitu (`ViewCompletedActivities`), tedy i trenér s tímto oprávněním.

### Streamy z intervals.icu

Aktivity z intervals.icu (Garmin, Polar, … — ne ty, které intervals.icu samo dostalo ze Stravy) mají grafy a mapu
automaticky, bez archivu:

- Po každé úspěšné synchronizaci připojení s `IActivityFileProvider` se spustí `ActivityStreamBackfillJob`.
  Ten stáhne **původní soubor ze zařízení** (`GET /api/v1/activity/{id}/file`; endpoint ověřen v OpenAPI
  specifikaci intervals.icu, pro aktivity ze Stravy ho intervals.icu nepodporuje) a zpracuje ho stejným
  parserem jako archiv. Uloží se stream (`Origin = IntervalsIcuFile`) a navíc `FitFileUuid`, takže se stejná
  aktivita z archivu nebo z intervals.icu spáruje přes FIT identitu (level 3).
- Postupuje od nejnovějších aktivit. Každý záznam zkusí jen jednou (`StreamFetchAttemptedAtUtc`), takže aktivity
  bez souboru nestojí požadavek při každé synchronizaci. Přeskočí aktivity, které už stream mají z jiného zdroje
  (typicky z archivu). Mezi staženími čeká 200 ms (limit intervals.icu je 10 požadavků/s na IP). Po HTTP 429
  skončí a při další synchronizaci pokračuje. Za jeden běh zpracuje nejvýš 3 000 aktivit.
- **Starší historie:** `POST /api/integrations/IntervalsIcu/history { fromDate }` (tlačítko „Stáhnout starší
  historii“ na kartě intervals.icu) zařadí synchronizaci `SyncTrigger.HistoryBackfill`. Datum
  (`SynchronizationRun.HistoryFromUtc`) je uložené přímo u běhu, takže se zobrazí i v historii synchronizací.
  Stáhnou se aktivity i wellness data od zvoleného data. Kurzor běžné synchronizace (`LastSyncedAtUtc`) se kvůli
  tomu neposune zpět. Pro Stravu je endpoint odmítnutý: historii ze Stravy řeší archiv, API Stravy nejde použít
  kvůli limitům a podmínkám.
- **Atribuce Garminu:** podmínky API intervals.icu vyžadují u dat ze zařízení Garmin uvést Garmin. Detail
  aktivity proto zobrazuje zařízení (`CompletedActivityDto.DeviceName`) a u Garmin zařízení text s atribucí.

## Mimo rozsah (další kroky)

- **Privátní zóny** (`privacy_zones.csv` v archivu): skrytí začátku a konce trasy.
- Streamy nejsou součástí exportu dat účtu (GDPR). Mažou se kaskádově se zdrojovým záznamem.
- **Hmotnost sportovce** z CSV: sloupec existuje, na ověřeném archivu byl ale prázdný.
- Nahrávání přes `IFormFile` ukládá soubor do mezisouboru ASP.NET a pak ho kopíruje do úložiště (u velkých archivů
  dvojí zápis na disk). Stahování přes odkaz tímto netrpí a je doporučená cesta.
