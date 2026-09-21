# TrainCoach — Rešerše externích integrací (Strava, Garmin, MySASY, Google Sheets, intervals.icu)

Tento dokument je podkladová rešerše pro `architecture.md`, `security.md` a `mvp-scope.md` — vysvětluje **proč** je zvolený integrační přístup takový, jaký je (Strava = reálný OAuth2 adaptér, Garmin/MySASY = kontrakt + mock provider + souborový import, Google Sheets = souborový import, intervals.icu = doporučený reálný OAuth2 adaptér jako primární cesta k Garmin/wellness datům). Obsahuje jen fakta ověřená v této session z oficiálních zdrojů poskytovatelů, s přesnými odkazy a datem ověření. Kde se fakt nepodařilo ověřit z oficiálního zdroje, je to výslovně uvedeno — nic není odhadováno.

**Datum ověření zdrojů: sekce 1–4 dne 2026-09-15, sekce 5 (intervals.icu) dne 2026-09-17.**

---

## 1. Strava (Strava API v3)

### Dostupnost
**Veřejně samoobslužné.** Registrace vývojářské aplikace probíhá přímo na strava.com (Settings → API), bez schvalovacího procesu ze strany Stravy pro základní přístup. Jde tedy o integraci reálně proveditelnou hned, bez čekání na partnerství.

### Autentizace
OAuth 2.0, `authorization_code` flow:
- **Autorizační endpoint:** `GET https://www.strava.com/oauth/authorize` (pro web; `https://www.strava.com/oauth/mobile/authorize` pro mobilní klienty)
- **Token endpoint:** `POST https://www.strava.com/oauth/token`
- Parametry autorizačního požadavku: `client_id`, `redirect_uri`, `response_type=code`, `scope`, volitelně `approval_prompt` (`force`/`auto`) a `state`.
- Výměna kódu za token: `client_id`, `client_secret`, `code`, `grant_type=authorization_code` → vrací `access_token`, `refresh_token`, `expires_at`, `expires_in`, data sportovce, přiznané `scope`.
- **Refresh:** access token expiruje **6 hodin** po vydání. Refresh: `grant_type=refresh_token` s `client_id`, `client_secret`, `refresh_token` → vrací nový pár token. Starý refresh token přestává platit po vydání nového (rotace).
- **Scopes potřebné pro TrainCoach:**
  | Scope | Co umožňuje |
  |---|---|
  | `activity:read` | Aktivity viditelné "Everyone"/"Followers" (bez privacy zón) |
  | `activity:read_all` | Všechny aktivity včetně privátních a privacy zón — doporučeno pro TrainCoach, jinak trenér nevidí kompletní data sportovce |
  | `profile:read_all` | Profilová data bez ohledu na nastavení viditelnosti |
- Otázka "jeden Strava účet na uživatele TrainCoach, nebo více" **není v oficiální dokumentaci OAuth guide řešena** — nepodařilo se ověřit z oficiálního zdroje, zda Strava technicky brání propojení více Strava účtů s jednou aplikací per koncový uživatel. Doporučujeme na aplikační úrovni (TrainCoach) sami vynutit 1:1 vazbu uživatel↔Strava účet, protože to odpovídá doménovému modelu (`IntegrationCredential` per uživatel).

### Dostupná data
- **Aktivity** (`Activities`): distance, elevation gain, moving/elapsed time, typ aktivity atd.
- **Streams** (`activities/{id}/streams`): heart rate, power/watts, cadence, distance, elevation (altitude), pace/velocity (smoothed), temperature, time, grade, moving, lat/lng.
- **Athlete**: profil, statistiky, HR/power zóny.
- **Segments**: segmenty, leaderboardy, osobní úsilí na segmentech.
- **Žádná dostupná data pro spánek, HRV ani klidovou tepovou frekvenci** — Strava API v3 je čistě aktivitní/výkonnostní, ne wellness/recovery platforma. Pro tato data je nutná jiná integrace (Garmin, MySASY, ruční zápis).

### Webhooks vs. pull
- Reálně podporované **webhooks** (push): endpoint `https://www.strava.com/api/v3/push_subscriptions`.
- **Omezení: jedna subscription na aplikaci celkem** (pokrývá všechny autorizované sportovce, ne per-uživatel).
- Vytvoření: POST s `client_id`, `client_secret`, `callback_url` (max 255 znaků), `verify_token`; Strava ověří callback GET požadavkem s `hub.challenge`, na který musí appka odpovědět echo hodnotou do 200 OK.
- Události: `object_type` (`activity`/`athlete`), `aspect_type` (`create`/`update`/`delete`), `updates`, `owner_id`, `subscription_id`, `event_time`. Pokrývá vytvoření/smazání/úpravu aktivity (title, type, privacy) a odvolání autorizace sportovcem (deauthorize).
- Callback musí vrátit `200 OK` do **2 sekund**, jinak Strava opakuje doručení (max 3 pokusy).
- Pro čtení privátních aktivit v payloadu je nutný scope `activity:read_all`.

### Rate limity
- **Celkový limit:** 200 požadavků / 15 min, 2 000 požadavků / den.
- **Non-upload limit** (vše kromě POST activities/uploads): 100 požadavků / 15 min, 1 000 požadavků / den.
- Komunikováno přes hlavičky `X-RateLimit-Limit`/`X-RateLimit-Usage` (celkové) a `X-ReadRateLimit-Limit`/`X-ReadRateLimit-Usage` (read), formát `15min,denní`.
- 15minutové okno resetuje na :00, :15, :30, :45; denní limit o půlnoci UTC.
- Překročení → `429 Too Many Requests` s JSON chybou.
- Vyšší limity lze získat žádostí o navýšení (např. do 10 sportovců: 200/15min a 2000/den pro read, 400/15min a 4000/den celkově); další škálování vyžaduje review ze strany Stravy.

### Omezení ukládání/zpracování dat (API Agreement)
Podle `https://www.strava.com/legal/api` (API Agreement, verze 2026):
- **Data konkrétního uživatele smí aplikace zobrazit pouze tomu uživateli** — "Strava Data provided by a specific user can only be displayed or disclosed in your Developer Application to that user." Data jiných uživatelů (i veřejně viditelná na Stravě) se **nesmí** zobrazovat/sdílet dál. Pro TrainCoach to znamená: trenér nesmí přes TrainCoach vidět Stravu sportovce, pokud jde o "syrová" Strava data mimo kontext souhlasu — je nutné zajistit, že zobrazení dat trenérovi jde přes vlastní `CoachAthleteRelationship`/souhlas model TrainCoach, ne přímo přes Strava data cizího uživatele.
- **Branding:** povinné dodržení Strava Brand Guidelines při použití Strava Marks/loga v aplikaci (§8.1).
- **Storage doba** není v agreementu číselně omezena (žádné "max N dní cache"), ale po **ukončení** smlouvy/přístupu musí být veškerá Strava Data a API Materials **okamžitě trvale smazána** a písemně potvrzeno Stravě (§4.4). Nejde tedy o volné trvalé úložiště bez podmínek — je nutný proces smazání dat při odpojení integrace/ukončení účtu.
- **Zákaz replikace/konkurence:** appka nesmí replikovat funkčnost Stravy ani sloužit jako náhrada Strava platformy.
- **AI/ML:** Strava dle sekundárních zdrojů (tiskové zprávy/komunitní fórum k aktualizaci agreementu) explicitně zakazuje použití dat získaných přes API pro trénování AI/ML modelů — tento konkrétní bod se nepodařilo doslovně dohledat přímo v textu `/legal/api` v rámci tohoto ověření, ale je zmíněn v oficiálním Strava Press článku o aktualizaci agreementu. Pro TrainCoach to i tak není relevantní riziko (žádné trénování AI modelů na Strava datech se neplánuje), ale je nutné mít na paměti při budoucích AI featurech (viz `security.md` §13 — AI smí jen přeformulovat výstup pravidel).
- **Komerční použití:** aktuálně zdarma, Strava si vyhrazuje právo v budoucnu zpoplatnit API přístup (§3.2) — nutno sledovat.

### Použitelnost pro osobní/komerční projekt
Plně použitelné hned, zdarma, bez schvalovacího procesu pro standardní limity. Komerční použití je dovoleno v mezích API Agreement (branding, zobrazení dat jen vlastníkovi, zákaz replikace Stravy).

### Doporučená implementační strategie pro MVP
Reálný OAuth2 adaptér (již součást architektury — `TrainCoach.Integrations`, `IIntegrationProvider`): authorization_code flow se scope `activity:read_all,profile:read_all`, uložení `IntegrationCredential` šifrovaně (viz `security.md` §4), refresh token rotace při každém volání blížícím se expiraci, pull import aktivit + streamů při připojení a periodicky/na vyžádání, webhook subscription (jedna na appku) pro průběžnou synchronizaci nových aktivit bez pollingu. Respektovat rate limit hlavičky a limit zobrazení dat jen vlastníkovi dat.

### Ověřené zdroje
- https://developers.strava.com/docs/authentication/
- https://developers.strava.com/docs/rate-limits/
- https://developers.strava.com/docs/reference/
- https://developers.strava.com/docs/webhooks/
- https://www.strava.com/legal/api (API Agreement, 2026)

---

## 2. Garmin (Garmin Connect Developer Program / Health API)

### Dostupnost
**Vyžaduje schválení, není veřejně samoobslužné, a je explicitně omezeno na firemní/business použití.** Podle oficiálního Program FAQ (`https://developer.garmin.com/gc-developer-program/program-faq/`): program je "available for enterprise use" a "only for business use." **Žádná samostatná úroveň pro přístup k vlastním osobním datům jednotlivce ani pro hobby/nezávislé vývojáře není v oficiální dokumentaci zmíněna.**

Proces žádosti: podání žádosti přes Garmin Connect Developer Program, potvrzení stavu žádosti do 2 pracovních dnů; po schválení přístup do vývojářského portálu, typická plná integrace 1–4 týdny. Nebyla nalezena informace o pozastavení příjmu nových žádostí v oficiální dokumentaci samotné (sekundární, neoficiální zdroje z fór/třetích stran zmiňovaly dočasné potíže s přístupovým formulářem v roce 2026, ale toto **nebylo ověřeno z oficiálního zdroje Garmin** a proto se do faktů tohoto dokumentu nezahrnuje jako potvrzené).

### Autentizace
OAuth-based (Garmin Connect Developer Program používá OAuth pro propojení účtu koncového uživatele), ale **konkrétní autorizační/token endpointy a přesné scope názvy nebyly v této session ověřeny** — jsou dostupné až po schválení v developer portálu, který vyžaduje schválený přístup a nebyl v rámci veřejně přístupné dokumentace k dispozici k ověření. **Nepodařilo se ověřit z oficiálního zdroje** bez schváleného přístupu.

### Dostupná data
Z oficiálních přehledových stránek (Health API, Connect Developer Program):
- **Health API:** "all-day health summary metrics such as heart rate, sleep, steps, and more" — konkrétněji dle detailní stránky Health API: steps, intensity minutes, kalorie, heart rate, **sleep**, **stress**, **Body Battery**, body composition, respiration, **Pulse Ox**, blood pressure, "Enhanced Beat-To-Beat Interval" (může souviset s HRV, ale explicitní položka "HRV" jako samostatná metrika nebyla v přehledu jmenovitě uvedena — **nepodařilo se jednoznačně ověřit samostatnou HRV metriku z oficiálního zdroje** v rámci veřejně dostupné přehledové dokumentace).
- **Activity API:** "full activity data for over 30 activity types."
- **Women's Health API:** menstruační cyklus, těhotenství — mimo scope TrainCoach.
- Klidová tepová frekvence je pokryta v rámci heart rate dat Health API (obecně zmíněno), ale přesný název pole/endpointu nebyl ověřen bez schváleného přístupu do developer portálu.

### Push vs. pull
Oficiální dokumentace uvádí podporu **"Ping/Pull or Push Architecture"** — vývojář si může zvolit integrační model, který mu vyhovuje. Data jsou dodávána v JSON.

### Historický backfill
Dokumentace zmiňuje možnost **"backfill user data"** v rámci evaluačního prostředí, ale bez konkrétních detailů o délce zpětného období — **nepodařilo se ověřit přesný rozsah backfillu z oficiálního zdroje** bez schváleného přístupu.

### Rate limity
**Nepodařilo se ověřit z oficiálního zdroje** — konkrétní číselné rate limity nejsou na veřejně dostupných přehledových stránkách uvedeny; pravděpodobně jsou součástí dokumentace až po schválení přístupu.

### Omezení ukládání/zpracování dat
Nebylo možné ověřit detailní smluvní podmínky nad rámec veřejné agreement PDF (`GARMINCONNECTDEVELOPERPROGRAMAGREEMENT_EN.pdf`), která nebyla v této session obsahově fetchnuta/analyzována do detailu (jen nalezena jako existující oficiální odkaz). Doporučeno před reálnou implementací (až po schválení) tuto smlouvu důkladně přečíst.

### Použitelnost pro osobní/komerční projekt
**Realisticky nepoužitelné pro TrainCoach v jeho současné podobě nezávislého/malého projektu.** Program je oficiálně deklarován jako "only for business use" s formálním schvalovacím procesem, a "access to some metrics may require a license fee payment or minimum device order quantity for commercial use" (byť samotný vstup do programu nemá licenční/maintenance poplatek). Pro nezávislý/osobní projekt bez firemní entity a bez byznys case pro Garmin je schválení nejisté a proces netriviální.

### Doporučená implementační strategie pro MVP
**Fallback, ne reálná integrace.** V `TrainCoach.Integrations` existuje kontrakt `IIntegrationProvider` + **mock/demo provider** (simulovaná Garmin data pro demo/testování UI) + **souborový import** (např. import exportu, který si uživatel sám stáhne z Garmin Connect přes standardní uživatelské UI Garminu — ne scraping, jde o soubor, který si uživatel sám exportuje a nahraje). Reálný OAuth adaptér se doplní **pouze pokud/až** TrainCoach získá schválení do Garmin Connect Developer Program (typicky vyžaduje formální byznys žádost, případně budoucí komerční fázi projektu s jasným byznys casem pro Garmin).

### Ověřené zdroje
- https://developer.garmin.com/gc-developer-program/overview/
- https://developer.garmin.com/gc-developer-program/program-faq/
- https://developer.garmin.com/health-api/overview/
- https://developer.garmin.com/gc-developer-program/health-api/

---

## 3. MySASY (mysasy.cz / mysasy.com — myAPI)

### Dostupnost
MySASY **má** oficiálně zmiňované rozhraní nazvané **"myAPI"**, aktuálně v **beta** verzi, s veřejně dostupnou Swagger dokumentací:
- `https://www.mysasy.com/novinka-myapi-pro-vyvojare` (oficiální blog/novinka MySASY o myAPI pro vývojáře)
- `https://app.swaggerhub.com/apis-docs/mysasyofficial/myapi4developers` (Swagger dokumentace API — hostovaná na SwaggerHub pod účtem "mysasyofficial", odkazovaná z oficiální stránky MySASY, takže ji lze považovat za oficiální zdroj API kontraktu)

Doména `mysasy.cz` je nastavena jako 301 redirect na `https://www.mysasy.com/` — jde tedy o tutéž firmu/produkt.

**Přístup ale není plně veřejně samoobslužný v praktickém smyslu "zaregistruj se a hned volej produkční API".** Oficiální stránka výslovně vyzývá zájemce, aby **kontaktovali MySASY přímo** ("pokud máte zájem o další podrobnosti, kontaktujte nás") na `api@mysasy.com` (příp. `developer@mysasy.com`), aby dostali přístup/testovací přístup k endpointům. Jde tedy o model **"self-serve dokumentace, ale řízený/kontaktní přístup k reálným API klíčům"** — mezi plně otevřeným self-serve (Strava) a plně partnerským schvalovacím procesem (Garmin) je to blíže druhému, byť s nižší bariérou (e-mail, ne formální byznys aplikace).

### Autentizace
**Nepodařilo se ověřit z oficiálního zdroje** — konkrétní autentizační mechanismus (API klíč, OAuth2, apod.) nebyl z veřejně dostupného obsahu blog stránky jednoznačně zjištěn; přesný mechanismus by bylo nutné ověřit přímo ve Swagger dokumentaci (`myapi4developers`) po hlubší analýze nebo přímým dotazem na `api@mysasy.com`. Nehádá se zde žádný konkrétní endpoint ani schéma autentizace.

### Dostupná data
Dle oficiální stránky jde o **HRV data** ze systému mySASY, konkrétně kategorie zmíněné oficiálně:
- "Regeneration strength" (síla regenerace)
- "Activation strength" (síla aktivace)
- "Actual readiness" (aktuální připravenost — obdoba "training readiness")
- "Long-term trainability" (dlouhodobá trénovatelnost)
- Míry autonomního nervového systému (spotřeba/doplňování/celkový výkon větví ANS)
- Metoda měření: **SA HRV** (spektrální analýza HRV), vlastní proprietární algoritmus MySASY, měření cca 4 minuty.

Nejde tedy o obecná fitness/aktivitní data (kroky, tepovky při běhu) jako u Stravy/Garminu, ale o **specializovaná HRV/readiness data** — přesně to, co TrainCoach potřebuje pro wellness modul jako doplněk k Strava/Garmin aktivitám.

### Webhooks / rate limity / retence dat
**Nepodařilo se ověřit z oficiálního zdroje** — žádná z veřejně dostupných stránek (hlavní web, blog o myAPI) tyto detaily neuvádí; byly by dostupné až ve Swagger dokumentaci nebo po přímém kontaktu s MySASY.

### Použitelnost pro osobní/komerční projekt
**Nejednoznačné — nelze potvrdit ani vyvrátit bez přímého kontaktu s MySASY.** Firma je česká, myAPI je v beta a oficiálně "open for collaboration" ("otevřeno spolupráci"), což naznačuje ochotu jednat i s menšími/nezávislými projekty, ale nejde o formulářové samoobslužné vydání API klíče jako u Stravy. Licenční/cenové podmínky **nebyly nalezeny** na žádné z prověřených oficiálních stránek.

### Doporučená implementační strategie pro MVP
Stejně jako u Garminu: **kontrakt `IIntegrationProvider` + mock/demo provider + souborový import jako fallback** pro MVP. Souborový import konkrétního formátu, který MySASY skutečně nabízí (CSV/export z aplikace), **se v této rešerši nepodařilo ověřit z oficiálního zdroje** — na webu MySASY nebyla nalezena veřejná dokumentace exportního formátu dat pro koncové uživatele. Doporučení: před implementací souborového importu pro MySASY buď (a) kontaktovat `api@mysasy.com` s konkrétním dotazem na dostupnost testovacího myAPI přístupu pro TrainCoach a na formát/dostupnost ručního exportu dat z mySASY aplikace, nebo (b) v MVP nabídnout jen obecný CSV import s uživatelsky nakonfigurovatelným mapováním sloupců (řešení nezávislé na konkrétním exportním formátu MySASY), což je ostatně konzistentní s plánovaným genereckým importním modulem (`features/import`, viz `architecture.md` §9.2).

### Ověřené zdroje
- https://www.mysasy.com/novinka-myapi-pro-vyvojare
- https://app.swaggerhub.com/apis-docs/mysasyofficial/myapi4developers (odkazováno z výše uvedené oficiální stránky; obsah samotné Swagger specifikace nebyl v této session detailně analyzován endpoint po endpointu)
- https://mysasy.cz (301 redirect na https://www.mysasy.com/, ověřeno)
- https://www.mysasy.com/

---

## 4. Google Sheets (Google Sheets API v4)

### Dostupnost
**Veřejně samoobslužné.** Standardní Google Cloud Platform postup — žádné schvalování třetí stranou pro základní/nekomerční použití s vlastním malým počtem uživatelů; pro širší produkční nasazení s "sensitive"/"restricted" scopes je nutná Google OAuth verifikace aplikace (viz níže).

### Autentizace
OAuth 2.0 přes Google Cloud projekt:
1. Vytvoření/použití Google Cloud projektu a povolení Google Sheets API.
2. Konfigurace **OAuth consent screen** ("Configuring your app's OAuth consent screen defines what is displayed to users and app reviewers, and registers your app so you can publish it later").
3. Vytvoření OAuth credentials (client ID/secret) pro danou aplikaci.
4. **Scopes** (ověřeno přímo z referenční dokumentace metody `spreadsheets.get`, endpoint vyžaduje jeden z těchto scopes):
   - `https://www.googleapis.com/auth/spreadsheets.readonly` — **doporučený scope pro TrainCoach** (jen čtení, princip least privilege — dokumentace explicitně doporučuje "choose the most narrowly focused scope possible")
   - `https://www.googleapis.com/auth/spreadsheets` — plný přístup (číst/zapisovat/mazat)
   - `https://www.googleapis.com/auth/drive.readonly`, `https://www.googleapis.com/auth/drive.file`, `https://www.googleapis.com/auth/drive` — alternativní Drive-scoped varianty
5. Scopes jsou rozdělené do tříd (non-sensitive / sensitive / restricted) podle úrovně ověření, kterou Google vyžaduje před produkčním publikováním OAuth aplikace — `spreadsheets.readonly` spadá mezi scopes vyžadující Google verifikaci OAuth consent screen pro produkční (non-testing) použití mimo úzký okruh testovacích uživatelů.

### Dostupná data
Obsah listů (buňky, rozsahy, formátování v omezené míře) existující tabulky uživatele — přesně to, co potřebuje jednorázový/opakovaný import koučovací tabulky do TrainCoach.

### Webhooks / push
Google Sheets API v4 nemá nativní webhook mechanismus srovnatelný se Strava push_subscriptions v rámci prověřené dokumentace — pro TrainCoach to není relevantní, protože use-case je import na vyžádání, ne live sync.

### Rate limity (kvóty)
Dle `https://developers.google.com/sheets/api/limits`:
- **Read requests:** 300 / minutu / projekt, 60 / minutu / uživatel / projekt.
- **Write requests:** 300 / minutu / projekt, 60 / minutu / uživatel / projekt.
- Kvóty se obnovují každou minutu, **žádný denní limit** pokud se dodrží minutové kvóty.
- Doporučený max payload 2 MB (ne tvrdý limit).
- Timeout požadavku při zpracování nad 180 sekund.
- Překročení → `429 Too Many Requests`, doporučeno exponenciální backoff.

### Omezení ukládání/zpracování dat
Nebyly v rámci této rešerše prověřeny specifické smluvní podmínky Google API Services User Data Policy nad rámec obecně známého rámce Google Cloud/API — pro účel TrainCoach (jednorázový/opakovaný import dat, která uživatel sám vlastní a sám autorizuje) toto nepředstavuje blokující riziko, ale **detailní textaci Google API Services User Data Policy (limited use requirements) je vhodné ověřit samostatně před produkčním publikováním OAuth aplikace**, pokud by se v budoucnu přešlo na živé napojení nad rámec MVP.

### Použitelnost pro osobní/komerční projekt
Plně použitelné, zdarma v rámci standardních kvót, žádný schvalovací proces pro samotné volání API s malým počtem testovacích uživatelů. Pro širší veřejné nasazení (mnoho externích Google účtů autorizujících přístup) je nutná Google OAuth app verification (proces auditu citlivých scopes), což je časově netriviální, ale ne blokující pro MVP s omezeným počtem uživatelů.

### Jednodušší alternativa — CSV export/import (preferovaná MVP cesta)
Produktový plán (viz `mvp-scope.md` §4) už preferuje **jednorázový/opakovaný souborový import** namísto živého API napojení: uživatel v Google Sheets/Excelu udělá "Export → CSV" (žádné OAuth, žádná Google Cloud konfigurace, žádná kvóta, žádná Google app verification) a nahraje soubor do TrainCoach importního modulu (`features/import`), který provede mapování sloupců, validaci, náhled a idempotentní re-import. Toto je jednodušší, rychlejší k implementaci a nezávislé na Google infrastruktuře/OAuth review procesu — proto je to zvolená MVP cesta, zatímco reálné OAuth2 napojení na Google Sheets API zůstává zdokumentovanou, technicky ověřenou možností pro budoucí "živou synchronizaci", pokud si ji uživatelé vyžádají (viz `mvp-scope.md` §4, poslední řádek tabulky).

### Doporučená implementační strategie pro MVP
CSV import (bez OAuth) jako jediná cesta v MVP. Google Sheets API v4 OAuth2 adaptér (`spreadsheets.readonly` scope, `spreadsheets.get` + `spreadsheets.values.get`) zůstává zdokumentovanou a technicky ověřenou cestou pro "next version", pokud vznikne poptávka po opakované/živé synchronizaci bez ručního exportu.

### Ověřené zdroje
- https://developers.google.com/sheets/api/guides/authorizing
- https://developers.google.com/sheets/api/limits
- https://developers.google.com/identity/protocols/oauth2/scopes#sheets
- https://developers.google.com/sheets/api/reference/rest/v4/spreadsheets/get

---

## 5. intervals.icu (agregátor: Garmin, Polar, Suunto, Coros, Huawei, Amazfit, Oura, WHOOP, Strava)

### Dostupnost
**Zdarma, ale registrace OAuth aplikace (na rozdíl od dřívějšího tvrzení v této sekci) NENÍ plně self-serve.** intervals.icu je bezplatná platforma pro analýzu tréninku (žádný trial, žádný time limit, bez nutnosti platební karty) — placený "Supporter" tier ($4/měsíc) přidává jen pohodlnostní funkce (počasí, plánovač sezóny, plný import historie ze Strava), **ne** přístup k API samotnému. Osobní API klíč (Settings → Developer Settings) si každý uživatel vygeneruje okamžitě, bez schvalování. **OAuth aplikace pro multi-user appku jako TrainCoach se ale zakládá přes formulář "Požádejte o OAuth přístup"** (Settings → Developer Settings → apps), který intervals.icu tým ručně posoudí a teprve poté e-mailem pošle hotový OAuth klient (client id/secret) — ověřeno přímo z tohoto formuláře 2026-09-17 (viz oprava v `### Autentizace` a `### Ověřené zdroje` níže). Počítejte tedy s prodlevou (řádově dny), ne s okamžitým self-serve jako u Stravy.

**Klíčová vlastnost pro TrainCoach:** intervals.icu funguje jako **agregátor třetích stran** — uživatel si v rámci intervals.icu jednorázově propojí svá zařízení/služby (Garmin Connect, Polar, Suunto, Coros, Huawei, Amazfit, Oura, WHOOP, Apple Health přes 3rd party apps, Strava) přes jejich vlastní OAuth integrace, a TrainCoach pak čte **kombinovaná** data jediným API napojením na intervals.icu — bez nutnosti řešit Garminův byznysový schvalovací proces nebo budovat samostatné adaptéry pro Oura/WHOOP/Polar/Suunto/Coros.

### Autentizace
- **API klíč (Basic Auth)** — vhodné jen pro single-user scénář/prototyp: `Authorization: ApiKey API_KEY:<klíč>` nebo `curl -u API_KEY:<klíč> ...`. Klíč generuje a spravuje sám uživatel v Developer Settings, může ho kdykoliv regenerovat/zrušit.
- **OAuth 2.0 (Bearer token)** — **doporučeno pro TrainCoach**, protože jde o multi-user aplikaci; formulář "Požádejte o OAuth přístup" to i sám doporučuje ("Pokud vytváříte aplikaci, kterou bude používat více sportovců... je nutné vytvořit OAuth aplikaci"). Registrace **vyžaduje ruční schválení intervals.icu týmem** (žádost → e-mail s hotovým klientem, viz `### Dostupnost` výše) — to je oprava dřívějšího tvrzení o self-serve registraci v této sekci. Formulář vyžaduje: název, popis, webovou stránku, zásady ochrany osobních údajů, **Redirect URLs** (produkční callback URL — `http://localhost/*` je vždy povoleno bez přidání), a volitelně **Webhook URLs** + výběr konkrétních webhook typů.
- **6 kategorií scope, ne jen 2** (ověřeno na `forum.intervals.icu/t/intervals-icu-oauth-support/2759`, 2026-09-17): `ACTIVITY` (dokončené aktivity), `WELLNESS` (váha, klidová TF atd.), `CALENDAR` (naplánované tréninky), `CHATS` (chaty/skupiny/zprávy), `LIBRARY` (knihovna tréninků), `SETTINGS` (nastavení sportovce — FTP, zóny), každá s `:READ`/`:WRITE` variantou. TrainCoach používá jen `ACTIVITY:READ,WELLNESS:READ` (least privilege) — `SETTINGS:READ` (sync HR zón/FTP) a `CALENDAR`/`LIBRARY` (import tréninkových plánů z intervals.icu) jsou zdokumentované, ale zatím nevyužité možnosti pro budoucí rozšíření, viz `mvp-scope.md`-style poznámka v plánovacím souboru pro rozšíření dat.
- **Token endpoint dvakrát nezávisle potvrzen**: `POST https://intervals.icu/api/oauth/token` — jednak živým testováním proti reálné aplikaci (viz níže), jednak přímo textem na fóru "Intervals.icu OAuth support". Vysoká jistota správnosti.
- **Revokace tokenu**: `DELETE https://intervals.icu/api/v1/disconnect-app` (ověřeno na stejném fóru) — oficiální endpoint pro odvolání access tokenu při odpojení účtu. **Aktuálně TrainCoach tento endpoint nevolá** (`IntegrationConnectionService.DisconnectAsync` jen maže lokální credential) — doporučeno doplnit, viz plán rozšíření.
- **Autorizační kód vyprší za 2 minuty** (ověřeno na fóru) — nesouvisí s TrainCoach vlastním 15minutovým `state` timeoutem (`IntegrationConnectionService.DecodeState`), je to samostatné, přísnější omezení na straně intervals.icu. Náš OAuth callback flow běží řádově sekundy, takže by neměl být problém, ale je to reálné omezení k zapamatování při budoucím ladění.
- **Jeden aktivní token na aplikaci na sportovce** (ověřeno na fóru) — nová autorizace nahradí starý token. Odpovídá existujícímu chování `UpsertConnectionAsync` (přepisuje `IntegrationCredential` při reconnectu).
- **Coach/athlete model uvnitř intervals.icu:** komunitně zdokumentovaný (ne oficiálně z dokumentace, ale z fóra — potvrzeno více uživateli) mechanismus, kdy "kouč" (držitel API klíče/OAuth aplikace) může číst data athletea přes endpoint `/api/v1/athlete/{athleteId}/...`, pokud athlete uvnitř intervals.icu přijme jeho "coaching request" — bez nutnosti athletea sdílet svůj vlastní klíč. **Pro produkční TrainCoach nedoporučujeme spoléhat na sdílený "coach" účet** (jeden kompromitovaný klíč = přístup ke všem athletům); správné řešení je standardní OAuth2 `authorization_code` flow per TrainCoach-uživatel (athlete si sám autorizuje TrainCoach OAuth app nad svým intervals.icu účtem), analogicky ke stávajícímu Strava adaptéru.
- Athlete ID `0` v URL odkazuje na aktuálně autentizovaného uživatele (`/api/v1/athlete/0/...`).
- **OAuth endpointy (ověřeno živě 2026-09-17 proti reálné schválené aplikaci, po dvou chybných pokusech):** autorizační endpoint `GET https://intervals.icu/oauth/authorize` funguje jak zdokumentováno. **Token endpoint je ale `POST https://intervals.icu/api/oauth/token`** — ani jedna z dřívějších dvou variant nefungovala: `https://intervals.icu/api/v1/oauth/token` (tvrzeno komunitním zdrojem) vrací `404 Not Found` přímo z aplikace (Spring Boot backend odpoví, ale takovou route nezná); `https://intervals.icu/oauth/token` (analogie k `/oauth/authorize`) vrací `405 Method Not Allowed` už na úrovni edge/nginx vrstvy před aplikací — to je GET-only route pro konzentní UI stránku, ne API endpoint. Skutečný token endpoint `/api/oauth/token` nemá segment `/v1/`, na rozdíl od všech ostatních datových endpointů v této integraci (activities, wellness, streams) — nekonzistence, kterou stojí za to mít na paměti. Ověřeno tak, že s reálným `client_id`/`client_secret` a neplatným kódem appka vrátí `{"status":404,"error":"Code not found (expired?)"}` — přesně chování reálného OAuth tokenu endpointu odmítajícího neplatný/prošlý kód, ne routing chybu. Opraveno v `IntervalsIcuIntegrationProvider` po prvním reálném pokusu o připojení.

### Dostupná data
**Wellness endpoint** (`GET/PUT /api/v1/athlete/{id}/wellness/{date}`, `PUT /api/v1/athlete/{id}/wellness` pro bulk) — doložená pole: `id` (datum), `weight`, `restingHR`, `hrv`, `hrvSDNN`, `sleepSecs`, `sleepScore`, `sleepQuality`, `avgSleepingHR`, `readiness`, `soreness`, `fatigue`, `stress`, `mood`, `motivation`, `injury`, `hydration`, `ctl`, `atl`, `rampRate`, `vo2max`, `steps`, `spO2` (dle přehledové stránky Wellness Integration — steps, weight, sleep, HRV, readiness, glukóza, menstruační cyklus, stress, mood, hydratace, SpO2, tlak), `comments`, `locked`.

- **Toto přesně pokrývá díru, kterou má Strava** (žádný spánek/HRV/klidová TF) — a to bez nutnosti Garminova byznysového API.
- `readiness` pole je typicky populováno ze zdrojových služeb, které readiness/recovery samy počítají (Oura, HRV4Training a podobné) — intervals.icu ho nepočítá vlastním proprietárním algoritmem, jen ho protahuje dál.
- **Aktivity** (`GET /api/v1/athlete/{id}/activities`, `.csv` varianta, `GET /api/v1/activity/{id}?intervals=true` s intervalovou analýzou) — distance, čas, výkon, detekce intervalů/segmentů, CTL/ATL/forma (fitness/fatigue model), plus **upload/download** aktivit ve FIT/TCX/GPX/ZIP/GZ.
- **Streams** (`GET /api/v1/activity/{id}/streams?types=...`) — sekundové senzorové řady: `watts`, `heartrate` (korigovaná ořezáním nad max HR) i `raw_heartrate`/`fixed_heartrate`, `cadence`, `distance`, `altitude`, `latlng`, `velocity_smooth`, `temp`, `moving`, `grade_smooth`, `time`. Prakticky identický rozsah jako Strava streams.
- **Plánované tréninky/kalendář** (`GET/POST/PUT/DELETE /api/v1/athlete/{id}/events`) — umožňuje TrainCoach nejen číst, ale i **zapisovat naplánované tréninky zpět** do intervals.icu kalendáře athletea (obousměrná synchronizace), což jde nad rámec toho, co nabízí Strava.
- **External ID mapping** — obousměrné mapování ID mezi intervals.icu a externím systémem (TrainCoach), užitečné pro idempotentní sync bez nutnosti vlastní deduplikace podle timestampu/aktivity.

### Zdrojová zařízení/služby, ze kterých intervals.icu agreguje wellness+aktivity
Dle oficiální přehledové stránky (`intervals.icu/features/wellness/`): **přímé integrace** — Garmin, Polar, Suunto, Coros, Huawei, Amazfit, Oura, WHOOP; **nepřímo** Apple Health a další přes aplikace třetích stran. Plus samostatně Strava sync (aktivity) zmíněný na hlavní/pricing stránce. To znamená: jediný adaptér `IntervalsIcuIntegrationProvider` v `TrainCoach.Integrations` může nahradit (nebo doplnit) potřebu samostatných adaptérů pro Garmin/Oura/WHOOP/Polar/Suunto/Coros zvlášť.

### Webhooks vs. pull
**Přesný seznam nyní ověřen přímo z formuláře "Požádejte o OAuth přístup"** (na rozdíl od dřívějšího komunitně dohledaného seznamu v této sekci, který wellness event postrádal). Podporované webhook typy, s příslušným OAuth scope, který je pro daný typ nutný:

| Webhook typ | Vyžadovaný scope | Popis |
|---|---|---|
| `CONNECTED_SERVICE` | `SETTINGS` | Athlete připojil/odpojil Garmin, Polar, Suunto atd. — užitečné pro TrainCoach vědět, kdy sportovec rozšířil/změnil zdroje dat |
| `APP_SCOPE_CHANGED` | — | Athlete změnil oprávnění pro tuto appku |
| `CALENDAR_UPDATED` | `CALENDAR` | Aktuální standard pro události kalendáře vytvořené/smazané/upravené |
| `CALENDAR_EVENT_UPDATED` | `CALENDAR` | **Deprecated** — nepoužívat, viz `CALENDAR_UPDATED` |
| `CALENDAR_EVENT_DELETED` | `CALENDAR` | **Deprecated** — nepoužívat, viz `CALENDAR_UPDATED` |
| `ACTIVITY_UPLOADED` | `ACTIVITY` | Nová aktivita nahrána — **nedoručuje se pro aktivity synchronizované ze Stravy** (viz níže) |
| `ACTIVITY_ANALYZED` | `ACTIVITY` | Existující aktivita přeanalyzována |
| `ACTIVITY_UPDATED` | `ACTIVITY` | Aktivita upravena (např. přejmenování) |
| `ACTIVITY_DELETED` | `ACTIVITY` | Aktivita smazána |
| `ACTIVITY_ACHIEVEMENTS` | `ACTIVITY` | Sportovec dosáhl něčeho (např. nové FTP) |
| **`WELLNESS_UPDATED`** | `WELLNESS` | **Váha, klidová TF, HRV atd. aktualizovány** — přesně ten wellness event, jehož existenci se dříve nepodařilo ověřit |
| `FITNESS_UPDATED` | `WELLNESS` | Fitness, fatigue, eFTP atd. aktualizováno |
| `SPORT_SETTINGS_UPDATED` | `SETTINGS` | Nastavení sportu (FTP, zóny atd.) |
| `CHAT_UPDATE` | `CHATS` | Nová/upravená zpráva v chatu |

**Důležitá výjimka potvrzená ve formuláři:** "activity webhooks are not delivered for Strava activities. Please use CALENDAR_UPDATED and not CALENDAR_EVENT_UPDATED or CALENDAR_EVENT_DELETED" — pokud sportovec v intervals.icu synchronizuje aktivity ze Stravy (ne přímo z Garmin/Polar/atd.), `ACTIVITY_UPLOADED`/`ACTIVITY_UPDATED`/`ACTIVITY_DELETED` se pro tyto aktivity nespustí. Pro takové sportovce je nutné se spolehnout na pravidelný pull (`SyncOrchestrator`), ne na webhook.

Payload dle dřívějšího komunitního zdroje obsahuje `secret` (pro ověření pravosti) a pole `events` s `athlete_id`, `type`, `timestamp` — tato část nebyla formulářem samotným potvrzena, jen odvozena z fóra, takže přesný payload tvar je vhodné ověřit až s reálným OAuth klientem.

### Rate limity
**Ověřeno přímo na stránce "Limity rychlosti" v nastavení schválené OAuth aplikace** (nejautoritativnější dostupný zdroj — přímo z produkčního účtu TrainCoach/PeakForm, 2026-09-17), potvrzuje a upřesňuje dřívější komunitní odhad z fóra:
- **OAuth aplikace (multi-user):** výchozí denní limit je **100 požadavků/uživatele/den, až pro 500 uživatelů (maximálně 50 000 požadavků/den celkem), s minimem 8 000/den** — tj. i s malým počtem uživatelů appka vždy má aspoň 8 000 požadavků/den k dispozici. Denní limit se obnovuje o půlnoci UTC.
- **15minutové okno:** 1/8 denního limitu, s minimem **2 500 požadavků/15 min** (potvrzeno, souhlasí s dřívějším komunitním odhadem).
- **Další, nezávislý limit:** 10 volání/s na IP adresu (nevrací vlastní rate-limit hlavičky).
- Pokud appka poroste nad 500 uživatelů a potřebuje vyšší denní limit (nebo naopak nižší z bezpečnostních důvodů), je nutné kontaktovat `support@intervals.icu`.
- **API klíč (single-user, jen pro referenci — TrainCoach ho nepoužívá):** dle dřívějšího komunitního zdroje (fórum) 5 000 požadavků/den, 2 500/15min, 10 req/s na IP — nebylo ověřeno stejným způsobem jako OAuth limity výše.
- Pro TrainCoach v MVP fázi (řádově jednotky až nízké desítky athletů) jsou tyto limity dostatečné bez nutnosti žádat o navýšení.

### Omezení ukládání/zpracování dat (API Terms and Conditions, účinné od 23. 10. 2025)
Doloženo z oficiálního fóra intervals.icu (vlákno "Intervals.icu API Terms and Conditions"):
- **Licence:** nevýhradní, celosvětová, bezplatná (royalty-free), trvalá licence k přístupu a použití API pro jakýkoliv zákonný účel **včetně komerčního použití** — bez nutnosti dalšího schvalování, na rozdíl od Garmina.
- **Garmin attribution povinnost:** protože intervals.icu sama čerpá data z Garmin Connect, přenáší se na TrainCoach povinnost dodržet Garmin brand/attribution guidelines při zobrazení dat, která pocházejí z Garmin zařízení — pole `device_name` (obsahuje "garmin" u Garmin aktivit) slouží k identifikaci; pro wellness data bez jasného zdroje stačí obecné upozornění typu "Grafy mohou obsahovat data ze zařízení Garmin". **Toto je přímý dopad na TrainCoach UI** (viz `security.md`/branding sekce) — je nutné do UI doplnit odpovídající attribution text.
- **Zákaz zneužití API** (malware, nelegální aktivity) — standardní klauzule.
- intervals.icu smí použít **agregovaná, anonymizovaná** usage data ke zlepšení služby.
- **Ukončení:** kterákoliv strana může ukončit přístup, s snahou o 7denní předchozí upozornění při porušení podmínek.
- **Odpovědnost:** API poskytováno "AS IS", bez záruk, vyloučení odpovědnosti za nepřímé/následné škody.
- **Změny podmínek:** 30denní emailové upozornění před změnou.
- **Rozhodné právo: jihoafrické právo** (na rozdíl od Stravy/Google, kde nebylo explicitně zmíněno) — relevantní pro TrainCoach právní/DPA posouzení, pokud by šlo o zpracování osobních/zdravotních dat EU občanů přes API poskytovatele mimo EU/UK.
- **Nedoloženo/mezera:** žádná explicitní politika rate-limit navýšení, žádná zmínka o AI/ML omezeních (na rozdíl od Stravy, kde je nepřímo zmíněno v tiskových zprávách), a žádné explicitní datové retenční lhůty ani povinnost mazat data po ukončení (na rozdíl od Stravy §4.4) — **nepodařilo se ověřit z oficiálního zdroje**, doporučeno ověřit přímo s intervals.icu před produkčním nasazením zpracovávajícím zdravotní data (HRV/spánek = citlivá kategorie dat dle GDPR).

### Použitelnost pro osobní/komerční projekt
**Použitelné zdarma, ale se schvalovací prodlevou pro OAuth aplikaci** (viz oprava v `### Dostupnost` výše — formulář žádosti, ruční review, e-mail s klientem) — přesto stále nejlepší dostupná cesta pro TrainCoach k Garmin/Oura/WHOOP/Polar/Suunto/Coros datům v MVP fázi, protože obchází Garminovo "only for business use" schvalování (sekce 2 výše, kde navíc není jisté, zda vůbec bude schváleno) i nejistý kontaktní model MySASY (sekce 3 výše). Další nutná podmínka: TrainCoach athlete musí mít (nebo si založit) vlastní bezplatný intervals.icu účet a v něm connectnout svá zařízení — to je jednorázový setup krok mimo TrainCoach, analogický tomu, co athlete stejně dělá dnes se Stravou.

### Doporučená implementační strategie
**Reálný OAuth2 adaptér, na stejném architektonickém vzoru jako Strava** (`IIntegrationProvider`, `TrainCoach.Integrations`) — implementováno, viz `IntervalsIcuIntegrationProvider`: `authorization_code` flow se scopy `ACTIVITY:READ`, `WELLNESS:READ`; uložení `IntegrationCredential` šifrovaně stejně jako u Strava; pull importu wellness dat (spánek/HRV/RHR/readiness/stress) + aktivit + streamů při připojení a periodicky. Webhooky (viz tabulka výše, zejména `WELLNESS_UPDATED` a `ACTIVITY_*`) jsou k dispozici pro budoucí průběžný sync bez pollingu, ale **zatím nejsou implementovány** — je potřeba vyplnit Webhook URLs/typy ve formuláři žádosti a doplnit endpoint na přijetí webhooku (obdoba Strava webhook handleru, který v TrainCoach také zatím chybí). Doplnit Garmin attribution text do UI wellness/aktivit karet, pokud `device_name` obsahuje "garmin" — **zatím neimplementováno**, `IntervalsIcuActivity` DTO pole `device_name` nenačítá. Toto **nahrazuje** dosavadní plán "Garmin = mock provider + souborový import" jako primární cestu k Garmin/wellness datům — mock/souborový import u Garminu a MySASY zůstávají jako fallback pro athlety, kteří si intervals.icu účet založit nechtějí/nemohou.

### Ověřené zdroje
- https://www.intervals.icu/features/open-api/
- https://forum.intervals.icu/t/api-access-to-intervals-icu/609
- https://forum.intervals.icu/t/intervals-icu-api-integration-cookbook/80090
- https://forum.intervals.icu/t/intervals-icu-api-terms-and-conditions/114087
- https://www.intervals.icu/features/wellness/
- https://www.intervals.icu/features/app-integrations/
- https://www.intervals.icu/features/extend/
- https://www.intervals.icu/pricing/
- https://forum.intervals.icu/t/coach-api-access-to-athlete-wellness-data-hrv-sleep-stress-readiness/129472
- https://forum.intervals.icu/t/access-activities-streams-via-api/101065
- https://forum.intervals.icu/t/readiness-field-added-to-wellness/4003
- https://py-intervalsicu.readthedocs.io/
- Pozn.: `https://intervals.icu/api/v1/docs/swagger-ui-index.html` (oficiální Swagger UI) vrátil v této session HTTP 500 při automatizovaném fetchi — doporučeno ověřit ručně v prohlížeči před finální implementací, protože jde o nejautoritativnější zdroj přesného schématu.
- Formulář "Požádejte o OAuth přístup" (`intervals.icu/settings/apps` → "Request OAuth access"), ověřeno přímo uživatelem TrainCoach v prohlížeči 2026-09-17 (screenshot) — zdroj pravdy pro schvalovací proces, Redirect/Webhook URL pole a přesný seznam webhook typů se scopy v tabulce výše. Tento zdroj má vyšší váhu než dřívější komunitně dohledané informace z fóra, které toto místy zpřesňuje/opravuje.
- https://forum.intervals.icu/t/intervals-icu-oauth-support/2759 — oficiální staff vlákno (uživatel "david", provozovatel intervals.icu) k OAuth API: token endpoint, `disconnect-app` revoke endpoint, 6 scope kategorií, 2minutová platnost autorizačního kódu, chování "jeden token na appku na sportovce" — viz `### Autentizace` výše.
- Živé ověření proti reálné, schválené TrainCoach OAuth aplikaci (2026-09-17): potvrzeny/opraveny přesné OAuth endpointy (`/oauth/authorize` funguje, `/api/v1/oauth/token` je 404, `/oauth/token` je 405 na edge vrstvě, skutečný token endpoint je `/api/oauth/token`) — viz `### Autentizace` výše a poznámka u `IntervalsIcuIntegrationProvider.TokenUrl` v kódu.

**Datum ověření zdrojů v této sekci: sekce založena 2026-09-17, opravena a doplněna o přímé ověření z produkčního UI 2026-09-17 (stejný den).**

---

## 6. TrainingPeaks (Partner API + Garmin Connect AutoSync)

**Datum ověření zdrojů v této sekci: 2026-09-21.** Rešerše provedena v kontextu otázky "jak TrainingPeaks dosahuje obousměrné synchronizace s Garminem (včetně pushování naplánovaných tréninků na hodinky)" a zda je to relevantní vzor/cesta pro PeakForm.

### Kontext: Garmin koupil TrainingPeaks (červenec 2026)

Garmin v červenci 2026 **akvírovalo TrainingPeaks i TrainHeroic** (cca 120 zaměstnanců obou firem, finanční podmínky nezveřejněny). Deklarovaný důvod: Garmin chce ke svým datům ze zařízení přidat vrstvu, která sportovci pomůže rozhodnout "co dál" — TrainingPeaks (vytrvalostní sporty) a TrainHeroic (silový trénink, kde Garmin byl slabší). Podle dostupných zdrojů **nebylo oznámeno sloučení do Garmin Connect ani změna cen** a TrainingPeaks má nadále deklarovaně podporovat i konkurenční zařízení (Apple, Polar, COROS, Wahoo, Suunto, Amazfit) — udržení této multi-platformní kompatibility je ale citováno jako otevřená otázka do budoucna, ne jistota. Pro PeakForm to znamená: TrainingPeaks dnes **není** "jen Garmin appka", ale její budoucí nezávislost na Garminu není zaručená.

### Mechanismus obousměrné synchronizace s Garminem ("Garmin Connect AutoSync")

Toto přesně odpovídá otázce, která vyvolala tuto rešerši:

- **Směr TrainingPeaks → Garmin (push naplánovaného tréninku na hodinky):** Naplánovaný strukturovaný trénink z TrainingPeaks kalendáře se pošle do **Garmin Connect kalendáře** athletea. Při dalším sync hodinek s Garmin Connect (Bluetooth/WiFi/LTE/USB/ANT+) se trénink stáhne přímo do zařízení a zobrazí se jako krokovaný trénink (warm-up/intervaly/cool-down/cíle). Úpravy plánu v TrainingPeaks se promítnou "instantně" do Garmin Connect kalendáře.
- **Směr Garmin → TrainingPeaks (dokončená aktivita zpět):** Po dokončení tréninku (v dosahu telefonu s Bluetooth) se aktivita nahraje do Garmin Connect a odtud "instantně" do TrainingPeaks k analýze.
- **Technický základ na straně Garminu:** Toto **není** totéž jako Garmin Health API (které PeakForm už zkoumal pro wellness data) — jde o samostatné **Garmin Training API**, součást Garmin Connect Developer Programu, určené výslovně k "publikaci tréninků a tréninkových plánů do Garmin Connect kalendáře". Program zahrnuje 5 API: Health, Activity, Women's Health, **Training**, Courses — dle dostupné dokumentace jde o jeden zastřešující Connect Developer Program (stejná "only for business use" bariéra popsaná v sekci 2 se dle všech dostupných indicií vztahuje na program jako celek, ne jen na Health API zvlášť — nebylo ale explicitně potvrzeno, že schválení pro jedno API automaticky znamená schválení pro Training API zvlášť).
- **Setup na straně athletea:** jednorázová autorizace v TrainingPeaks účtu (OAuth-like autorizační tok proti Garmin Connect), poté plně automatický obousměrný běh bez manuálních kroků.

### TrainingPeaks Partner API (nezávisle na Garmin AutoSync)

- **Model přístupu:** stejně restriktivní jako Garmin — **"access to the API is not available for personal use"**, jen pro schválené komerční vývojáře fitness aplikací/zařízení, žádost přes formulář (`api.trainingpeaks.com/request-access`), posouzení TrainingPeaks týmem. Historicky (od cca 2005) měl TrainingPeaks otevřenější/neregistrované API; přechod na řízený partnerský model proběhl v rámci dvouletého přepracování API (oznámeno 2017, dle dostupného zdroje aktualizováno naposledy 2026-09-12).
- **Autentizace:** OAuth 2.0, Bearer token, REST/JSON, endpoint `api.trainingpeaks.com` (+ sandbox `api.sandbox.trainingpeaks.com`).
- **Datový model (`Workouts Object`, `Workout Structure Object`):** koncepčně velmi podobný vlastnímu modelu PeakForm (`PlannedWorkout`/`WorkoutSegment`) — strukturovaný trénink je JSON s polem `Structure`, obsahujícím pole kroků typu `Step` nebo `Repetition`, každý s `Length` (jednotka Meter/Second + hodnota), volitelným `IntensityClass` (WarmUp/CoolDown/Active/Rest), `IntensityTarget` (jednotka např. `PercentOfFtp`, `PercentOfMaxHr`, `PercentOfThresholdHr`, `PercentOfThresholdSpeed`, `Rpe`, s `Value`/`MinValue`/`MaxValue`), `CadenceTarget`. `Repetition` obsahuje vnořené pole `Steps` + počet opakování. Toto je užitečná reference, pokud by PeakForm chtěl v budoucnu navrhnout vlastní export/import formát strukturovaných tréninků kompatibilní s tímto standardem.
- **Existující partneři** (dle veřejně dostupných zdrojů): Garmin, Polar, MyFitnessPal, HRV4Training, FitnessSyncer, Wahoo, Zwift a další — TrainingPeaks funguje obdobně jako Intervals.icu, tj. jako centrální bod, který jednotliví partneři čtou/zapisují.
- **Další integrace TrainingPeaks** (nezávisle na Garminu, dle veřejné integrace stránky `trainingpeaks.com/upload`): Wahoo (ELEMNT/BOLT auto-upload), Zwift (obousměrně — dokončené tréninky nahoru, plánované indoor tréninky dolů do Zwift), TrainerRoad (import plánovaných indoor tréninků), Polar (Polar Flow autosync), Suunto (Suunto App), Strava, WHOOP, Oura, COROS, Apple Watch, FORM Goggles, iGPSport, Hammerhead — přes 100 zařízení/aplikací celkem dle vlastního tvrzení TrainingPeaks.

### Srovnání s cestou, kterou PeakForm už má k dispozici (intervals.icu)

**Důležité zjištění přímo relevantní pro PeakForm:** intervals.icu (na které je PeakForm už OAuth-napojený) má **prakticky identickou** schopnost push naplánovaných tréninků na Garmin hodinky, jakou má TrainingPeaks — a to jako **samostatnou, na PeakForm nezávislou autorizaci**, kterou si athlete udělá přímo ve svém intervals.icu účtu (zaškrtnutí "Upload planned workouts" → OAuth autorizace intervals.icu vůči Garmin Connect, mimo PeakForm). Jakmile je toto jednou nastavené:
- Trénink naplánovaný v intervals.icu kalendáři se automaticky (typicky ráno v den/den před tréninkem) pošle do Garmin Connect kalendáře a odtud na hodinky, včetně plné intervalové struktury a cílů. Podporovaná zařízení: Forerunner 255/265/955/965, Fenix 6/7/8, Epix 2, Venu 3, Edge 530/540/830/840/1040 (starší modely typu Forerunner 235/Vivoactive 3 strukturované tréninky nepřijímají).
- Směr je **jednosměrný** (jen upload do Garminu) s oknem cca 7 dní dopředu — **stažení Garmin-vytvořených plánů zpět není možné** ("Garmin doesn't allow to download their workouts" — potvrzeno na oficiálním fóru intervals.icu jako obecné omezení Garmin platformy, ne chyba intervals.icu).
- **Toto přesně odpovídá endpointu, který PeakForm už má zdokumentovaný, ale nikdy nevolá**: `POST/PUT/DELETE /api/v1/athlete/{id}/events` s OAuth scope `CALENDAR` (viz sekce 5 výše, "Doporučené další kroky" v `docs/integrations/activity-matching.md` a §11 v `docs/integrations/canonical-data-and-deduplication-plan.md`). Nepodařilo se najít oficiální dokumentaci přesného JSON tvaru pro strukturovaný trénink v `events` payloadu (Swagger byl při dřívější rešerši nedostupný) — bylo by nutné ověřit against reálný účet/komunitní zdroje (`py-intervalsicu` knihovna) před implementací.

### Použitelnost pro PeakForm

**TrainingPeaks samotné (jako datový zdroj nebo cíl) není pro PeakForm realisticky dostupné** — stejně restriktivní "jen pro schválené komerční partnery" model jako Garmin, žádná self-serve cesta, a nyní navíc vlastněné přímým konkurentem v Garmin ekosystému (menší motivace TrainingPeaksu schvalovat konkurenční tréninkovou platformu).

**Praktický důsledek:** Pokud PeakForm chce nabídnout "vytvoř trénink v PeakForm → objeví se na Garmin hodinkách" (funkce, kterou má TrainingPeaks a která vyvolala tuto rešerši), **nejkratší reálná cesta nevede přes TrainingPeaks ani přímo přes Garmin Training API** (obojí vyžaduje formální byznysové schválení, viz sekce 2), **ale přes již existující intervals.icu OAuth napojení** — rozšířením scope o `CALENDAR:WRITE` a implementací volání na `/api/v1/athlete/{id}/events`, které intervals.icu adaptér dnes vůbec nevolá. Toto by mělo být ověřeno technickým experimentem (vytvořit testovací event přes API, zkontrolovat, zda a v jaké podobě se propíše na připojený Garmin účet) dřív, než se do toho investuje produkční implementace.

### Experiment: potvrzeno živě (2026-09-21)

Hypotéza výše byla ověřena proti reálnému, produkčnímu Intervals.icu účtu (ne mock/demo):

1. `IntervalsIcuIntegrationProvider.Scopes` rozšířen o `CALENDAR:WRITE` (`ACTIVITY:READ,WELLNESS:READ,CALENDAR:WRITE`).
2. Athlete provedl reconnect (nová OAuth autorizace) — nutné, protože Intervals.icu nahrazuje celou sadu scope při každé nové autorizaci, starý token novou nezíská automaticky.
3. **Nalezen a opraven reálný bug** objevený právě tímto reconnectem: `IntegrationConnectionService.UpsertConnectionAsync` selhávalo na `DbUpdateConcurrencyException`, pokud athlete reconnectuje po předchozím odpojení (Disconnect smaže `IntegrationCredential`, ale nový `IntegrationCredential` vytvořený jen přes navigační vlastnost na už trackovaném/needded `IntegrationConnection` byl EF Core mylně vyhodnocen jako existující řádek — `Entity.Id` je generováno na klientovi (`Guid.NewGuid()` v property inicializeru), takže neprázdný klíč bez explicitního `Add()` vedl k `UPDATE` místo `INSERT`). Opraveno explicitním `db.IntegrationCredentials.Add(...)` v `IntegrationConnectionService.cs`. Bez této opravy by **žádný athlete nemohl znovu připojit Intervals.icu po odpojení** — nezávislé, produkčně relevantní zjištění nad rámec původní otázky.
4. Testovací POST na `POST https://intervals.icu/api/v1/athlete/0/events` s payloadem `{category: "WORKOUT", start_date_local, type: "Run", name, description: "- 10m Z1\n- 5m Z2\n- 5m Z1", moving_time}` vrátil `200 OK` a vytvořil událost (potvrzeno: Intervals.icu textový popis samo rozparsovalo do strukturovaných kroků s cílovými zónami v `workout_doc.steps` — formát z komunitní dokumentace v sekci výše je funkční).
5. **Athlete potvrdil vizuálně**: testovací trénink naplánovaný na den+2 se objevil v **Garmin Connect kalendáři** (cloudová strana, ověřeno nezávisle na fyzickém přesunu hodinek). Propagace z Intervals.icu do Garmin Connect je tedy živě potvrzená, ne jen teoretická.
6. Testovací událost po ověření smazána (`PUT`/`DELETE` na stejném endpointu), dočasné diagnostické CLI příkazy použité pro test odstraněny z kódu. Scope `CALENDAR:WRITE` ponechán na žádost athletea pro budoucí implementaci.
7. **Vedlejší zjištění**: Intervals.icu po reconnectu vrátilo `GrantedScope = "ACTIVITY:WRITE,WELLNESS:WRITE,CALENDAR:WRITE"` — širší, než appka v `scope` parametru žádala (`ACTIVITY:READ,WELLNESS:READ,CALENDAR:WRITE`). Vysvětleno athletem: na OAuth consent obrazovce Intervals.icu si sám zaškrtl i WRITE varianty pro Activity/Wellness (Intervals.icu nabízí scope jako uživatelem volitelné zaškrtávátka, ne jako appkou pevně vynucený seznam). Aplikační kód dnes žádný write endpoint pro Activity/Wellness nevolá, takže z toho neplyne bezprostřední riziko, ale token má reálně širší oprávnění, než appka potřebuje — k zvážení při budoucím affects review.
8. **Neověřeno v tomto experimentu**: skutečné doručení na fyzická hodinky (jen Garmin Connect cloud kalendář, ne watch-level sync) — události byla naplánovaná na den+2, mimo dříve zdokumentované "dnes/zítra" push okno, a byla smazána dřív, než mohlo dojít k reálnému device syncu. Přesný časový mechanismus (batch push každé ráno vs. okamžitý push při vytvoření) zůstává neověřený.

### Ověřené zdroje

- https://gadgetsandwearables.com/2026/07/22/garmin-acquires-trainingpeaks-trainheroic/ (akvizice, 2026-07-22)
- https://www.trainingpeaks.com/coach-blog/garmin-connect-autosync-integration/
- https://www.trainingpeaks.com/partners/garmin/
- https://help.trainingpeaks.com/hc/en-us/articles/204070864-Garmin-Connect-AutoSync-FAQ-and-tips-activities-workouts-and-daily-health-metrics
- https://www.trainingpeaks.com/blog/an-update-on-trainingpeaks-partner-api/ (publikováno 2017-02-08, aktualizováno 2026-09-12)
- https://github.com/TrainingPeaks/PartnersAPI/wiki (a podstránky `Workout-Structure-Object`, `Workouts-Object`)
- https://www.trainingpeaks.com/upload/ (seznam integrací)
- https://developer.garmin.com/gc-developer-program/training-api/
- https://developer.garmin.com/gc-developer-program/overview/
- https://forum.intervals.icu/t/upload-planned-workouts-to-garmin-connect/1521
- https://forum.intervals.icu/t/solved-issue-garmin-planned-workouts-not-syncing-to-calendar-ans-garmin-doesnt-allow/117827
- https://stas.run/en/guides/intervals-icu-garmin-sync

**Omezení této rešerše:** Nepodařilo se ověřit, zda Garmin Training API (na rozdíl od Health API) má jiný/mírnější schvalovací proces — oficiální stránka na to přímo neodpovídá. Přesný JSON formát intervals.icu `events` endpointu pro strukturovaný trénink nebyl ověřen proti Swaggeru ani proti reálné odpovědi (mimo rozsah této rešerše). Interní implementace intervals.icu → Garmin (zda používá zrovna Garmin Training API, nebo jiný mechanismus) nebyla staff komentářem na fóru potvrzena.

---

## 7. Souhrnná tabulka

| Poskytovatel | Dostupnost reálné integrace nyní | Schvalovací proces | Doporučený MVP fallback |
|---|---|---|---|
| **Strava** | **Ano** — plně samoobslužné, zdarma | Ne, jen standardní registrace aplikace | Není potřeba fallback — reálný OAuth2 adaptér je přímo v MVP |
| **Garmin** (Connect Developer Program / Health API + Training API) | **Ne** — oficiálně "only for business use", formální schválení, nejisté pro nezávislý projekt | Ano, formální žádost + review Garminem, možné licenční poplatky pro komerční metriky | Kontrakt `IIntegrationProvider` + mock/demo provider + souborový import z uživatelského exportu; **od 2026-09-17 preferovaná nepřímá cesta = intervals.icu adaptér (sekce 5)**, který Garmin data agreguje bez nutnosti Garmin schválení — **totéž platí pro push naplánovaných tréninků na hodinky (sekce 6): intervals.icu `/events` endpoint místo Garmin Training API** |
| **MySASY** | **Omezeně** — myAPI existuje a má veřejnou Swagger dokumentaci, ale reálný přístup je řízen přes kontakt s MySASY (`api@mysasy.com`), ne okamžitý self-serve | Ano, kontaktní/partnerský model (nižší bariéra než Garmin, ale ne plně automatické) | Kontrakt `IIntegrationProvider` + mock/demo provider + generický CSV import (konkrétní MySASY export formát nepodařilo se ověřit z oficiálního zdroje) |
| **Google Sheets** | **Ano** (OAuth API), ale **MVP zvolil jednodušší cestu** | Ne pro API samotné (jen Google Cloud projekt); ano (verifikace) jen pro širší produkční publikování OAuth aplikace mimo testovací uživatele | CSV export/import bez OAuth — zvolená MVP cesta; živé OAuth API napojení zdokumentováno jako budoucí možnost |
| **intervals.icu** | **Ano** — plně samoobslužné, zdarma, agreguje Garmin/Polar/Suunto/Coros/Huawei/Amazfit/Oura/WHOOP/Strava, **umí i push plánovaných tréninků zpět na Garmin hodinky** | Ne, jen standardní self-serve registrace OAuth aplikace | Není potřeba fallback — reálný OAuth2 adaptér doporučen jako primární cesta k wellness+Garmin datům i k budoucímu pushování tréninků na zařízení |
| **TrainingPeaks** | **Ne** — jen pro schválené komerční partnery, žádná self-serve cesta, nyní vlastněné Garminem (od 2026-07) | Ano, formální žádost, není pro osobní použití | Není relevantní jako datový zdroj/cíl pro PeakForm; hodnotné jen jako **architektonická reference** (formát `Workout Structure Object`) a jako důkaz, že push-to-device přes intervals.icu je reálně fungující, ověřený vzor |
