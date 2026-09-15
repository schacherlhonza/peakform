# TrainCoach — Produktové požadavky

## 1. Vize produktu

TrainCoach je webová SaaS aplikace, která nahrazuje ruční sledování běžeckých (a obecně vytrvalostních) tréninků v Google Sheets. Cílem je poskytnout trenérovi a sportovci jeden sdílený nástroj pro plánování tréninků, zaznamenávání skutečně odběhnutých aktivit, sledování zdraví a regenerace (spánek, HRV, klidová tepová frekvence, bolesti/nemoc), výživy a hydratace, cílů a závodů, vzájemnou komunikaci a automatické, pravidly řízené denní reporty.

Aplikace nahrazuje tabulku, ale nekopíruje její omezení — místo jedné velké sdílené tabulky nabízí strukturovaná data, historii, oprávnění a integrace na externí zdroje dat (Strava, Garmin, manuální zápis, import souborů).

### Klíčové principy

- **Trenér vs. sportovec jako oddělené role** s jasně definovanými oprávněními a souhlasem (consent) na přístup k datům.
- **Pravidly řízené (rule-based) reporty**, nikoli AI diagnostika. Report nikdy nenahrazuje lékařské posouzení.
- **Čeština jako primární jazyk** produktu, s architekturou připravenou na další jazyky (i18n-ready).
- **Mobil-first / responzivní** rozhraní a architektura připravená na budoucí PWA/mobilní klienta bez nutnosti přepisovat backend.
- **Data v vlastnictví sportovce** — sportovec musí explicitně souhlasit s tím, že trenér vidí jeho data, a souhlas může kdykoliv odvolat.

## 2. Persony

### 2.1 Sportovec (Athlete)

- Běžec (rekreační až výkonnostní), který dostává tréninkový plán od trenéra nebo si plán vede sám.
- Chce vidět, co má dnes/tento týden trénovat, zapsat, jak trénink proběhl, a jak se cítí (spánek, únava, bolesti).
- Chce propojit Strava/Garmin, aby se aktivity zapisovaly automaticky, případně nahrát data ručně nebo importem.
- Chce vidět svůj kalendář závodů a cílů a postup směrem k nim.
- Chce komunikovat s trenérem u konkrétního tréninku/dne (komentáře), ne pouze mimo aplikaci (WhatsApp, telefon).
- Očekává, že jeho citlivá zdravotní data (spánek, HRV, bolesti) jsou vidět jen trenérovi, kterému to výslovně povolil.

### 2.2 Trenér (Coach)

- Vede více sportovců současně (desítky), potřebuje přehledový dashboard, ne procházení tabulek po sloupcích.
- Sestavuje tréninkové plány po týdnech se strukturovanými tréninky (segmenty — rozcvičení, intervaly, tempo, výklus).
- Potřebuje rychle vidět odchylky: sportovec netrénoval podle plánu, má zvýšenou klidovou tepovku, hlásí bolest, spí málo.
- Potřebuje ke sportovci přístup až po jeho souhlasu (pozvánka/vztah coach–athlete), a musí vidět jen to, k čemu má oprávnění.
- Chce psát komentáře k jednotlivým tréninkům/dnům a reagovat na denní reporty.
- Spravuje vlastní slovník zkratek tréninkové terminologie (např. WU, CD, MK, ABC), protože každý trenér používá jiné zkratky.

## 3. Rozsah funkcí podle modulů

### 3.1 Dashboard

- Sportovec: přehled aktuálního týdne (plán vs. skutečnost), nejbližší cíle/závody, poslední check-in, nepřečtené komentáře/notifikace.
- Trenér: přehled všech svěřených sportovců — kdo netrénoval podle plánu, kdo hlásí bolest/nemoc, kdo má odchylku v HRV/klidové tepovce, nepřečtené zprávy.
- Vizuální odlišení stavů (v pořádku / vyžaduje pozornost / kritické — např. hlášená bolest nebo výrazně zvýšená klidová tepovka).

### 3.2 Kalendář a tréninkový plán

- Zobrazení tréninkového plánu (Season → TrainingPlan → TrainingWeek → PlannedWorkout) v týdenním i měsíčním pohledu.
- Plánovaný trénink obsahuje strukturované segmenty (WorkoutSegment) — typ (rozcvičení, interval, tempo, výklus, odpočinek), vzdálenost/čas, intenzita/tempo/tepová zóna.
- Párování plánovaného a skutečně odběhnutého tréninku (podle data a sportovce, případně explicitního odkazu na PlannedWorkout).
- Sportovec vidí, co má dnes/tento týden na programu; trenér plánuje dopředu na týdny/měsíce.
- Podpora vlastních zkratek (CustomAbbreviation) v popisu tréninku — uživatelsky spravovaný slovník, ne hardcoded logika v kódu.

### 3.3 Detail tréninku

- Zobrazení plánovaného tréninku (segmenty, cíle, poznámky trenéra) vedle skutečně odběhnuté aktivity (tempo, tep, vzdálenost, čas, mapa/trasa je-li k dispozici ze zdroje).
- Zpětná vazba sportovce k tréninku (TrainingFeedback — jak se trénink zvládal, subjektivní náročnost).
- Vlákno komentářů (Comment) u konkrétního tréninku mezi sportovcem a trenérem.
- Zobrazení zdroje dat (DataProvenance) — odkud metrika pochází (manuál, import, Strava, mock provider) a která hodnota má přednost, pokud existuje víc zdrojů pro stejnou aktivitu.

### 3.4 Ranní a večerní check-in

- Jedna entita DailyCheckIn s typem Ranní/Večerní a poli, která dávají smysl pro daný typ (ranní: kvalita spánku, HRV, klidová tepovka, subjektivní únava, nálada; večerní: shrnutí dne, bolesti, celková spokojenost s tréninkem).
- Hlášení bolesti nebo příznaků nemoci (PainOrHealthFlag) s úrovní závažnosti a lokalizací (pokud jde o bolest pohybového aparátu).
- Historie check-inů v čase (trendy), ne jen aktuální den.

### 3.5 Spánek, HRV a regenerace

- Záznam spánku (SleepRecord) — doba, kvalita, případně fáze spánku, pokud je zdroj poskytuje.
- Měření HRV (HrvMeasurement) a odvozené metriky regenerace (RecoveryMetric).
- Osobní výchozí hodnoty (PerformanceBaseline) pro vyhodnocování odchylek (např. „o kolik je dnešní klidová tepovka nad osobním průměrem").
- Osobní rekordy (PersonalRecord) evidované automaticky z dokončených aktivit.

### 3.6 Výživa a hydratace

- Jednoduché zaznamenávání jídla (FoodEntry) a příjmu tekutin (HydrationEntry) v průběhu dne, primárně v kontextu tréninkové zátěže (ne jako plnohodnotná nutriční aplikace).
- Přehled v kontextu náročných tréninkových dnů/závodů.

### 3.7 Závody a cíle

- Sezóna (Season) sdružuje cíle (Goal) a závody (Race) v daném období.
- Závod má datum, typ, cílový čas/umístění, návaznost na tréninkový plán (např. vrchol formy k danému datu).
- Přehled blížících se závodů a postupu k cílům na dashboardu obou rolí.

### 3.8 Reporty

- Automaticky generovaný denní/týdenní report (GeneratedReport) postavený na oddělených krocích: výpočet metrik → vyhodnocení pravidly (rule engine) → generování textu → doručení (notifikace/e-mail/v aplikaci).
- Report je vždy pravidly řízený, nikdy autonomní AI diagnóza. Pokud existuje AI vysvětlení reportu, jde výhradně o srozumitelnější přeformulování výstupu pravidel, nikdy o vlastní lékařský úsudek nebo automatickou úpravu plánu.
- Report je adresný — sportovec vidí svůj, trenér vidí reporty svěřených sportovců (dle oprávnění).

### 3.9 Oprávnění a vztah trenér–sportovec

- CoachAthleteRelationship: pozvánka, stav (čeká na potvrzení/aktivní/ukončený), datum začátku/konce, auditní stopa.
- Granulární oprávnění (RelationshipPermission) — např. přístup k tréninkovým datům odděleně od přístupu ke zdravotním/wellness datům.
- Sportovec může kdykoliv oprávnění omezit nebo vztah ukončit (odvolání souhlasu).
- Žádný přístup k datům sportovce bez aktivního, souhlasem podloženého vztahu (viz `security.md`).

### 3.10 Import dat

- Import z Google Sheets (jednorázový/opakovaný, souborový) s mapováním sloupců, validací, náhledem před uložením a idempotentním opakovaným importem (stejný soubor nahraný znovu nevytvoří duplicity).
- Import obecně přes ImportedFile a DataProvenance, aby bylo vždy dohledatelné, odkud data pocházejí.

### 3.11 Integrace

- Strava: OAuth2 propojení účtu, automatická synchronizace aktivit.
- Garmin a MySASY: připraveno na úrovni kontraktu (IIntegrationProvider) a mock/demo poskytovatele; reálné napojení čeká na schválení přístupu k API (viz `mvp-scope.md`).
- Průběh a stav synchronizace je viditelný uživateli (SynchronizationRun).

### 3.12 Notifikace

- V aplikaci (a případně e-mailem) o nových komentářích, vygenerovaných reportech, žádostech o propojení trenér–sportovec, upozorněních na hlášenou bolest/nemoc.

## 4. Nefunkční požadavky

### 4.1 Jazyk a lokalizace

- Výchozí a primární jazyk uživatelského rozhraní je **čeština**.
- Frontend používá i18n vrstvu (react-i18next) od začátku, aby přidání dalšího jazyka nevyžadovalo přepis komponent — všechny texty jsou od počátku vedeny přes překladové klíče.

### 4.2 Responzivita a PWA-ready

- Rozhraní je plně použitelné na mobilním telefonu (sportovec bude aplikaci nejčastěji používat ráno/večer na mobilu při check-inu).
- Frontendová architektura (Vite + React) je vedena tak, aby v budoucnu šlo přidat PWA vrstvu (service worker, manifest, offline cache klíčových obrazovek) bez zásahu do backendu — API je čistě REST/JSON a nezávisí na konkrétním klientovi.
- Datový model a API jsou navrženy tak, aby v budoucnu mohl vzniknout nativní mobilní klient (např. pro odesílání push notifikací) bez nutnosti měnit doménovou vrstvu.

### 4.3 Bezpečnost a soukromí

- Viz `security.md` pro detail — zde jen shrnutí: ochrana proti IDOR, šifrované úložiště integračních přihlašovacích údajů, rotace refresh tokenů, explicitní souhlas sportovce, export a smazání dat na žádost.

### 4.4 Spolehlivost a udržovatelnost

- Modulární monolit (viz `architecture.md`) — jednoduché nasazení, testovatelnost, prostor pro budoucí vytěžení mikroslužeb, pokud to reálná zátěž vyžádá.
- Automatizované testy na úrovni domény, aplikace i API (integrační testy) — viz testovací strategie v `architecture.md`.

### 4.5 Výkon

- Dashboard trenéra se svěřenými sportovci musí zůstat použitelný i při desítkách sportovců na jednoho trenéra (stránkování, agregace na backendu, ne stahování všech dat najednou).
- Synchronizace s externími poskytovateli (Strava apod.) probíhá asynchronně na pozadí, nikdy neblokuje UI požadavek.

## 5. Mimo rozsah (viz také `mvp-scope.md`)

- Živá obousměrná synchronizace s Google Sheets (jen souborový import).
- Reálné napojení na Garmin a MySASY (čeká na schválení přístupu).
- Nativní mobilní aplikace (architektura je připravena, samotná aplikace není součástí této fáze).
- AI jako autonomní rozhodovací prvek (úprava plánu, lékařský úsudek) — AI smí nanejvýš vysvětlit výstup pravidlového enginu.
