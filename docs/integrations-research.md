# TrainCoach — Rešerše externích integrací (Strava, Garmin, MySASY, Google Sheets)

Tento dokument je podkladová rešerše pro `architecture.md`, `security.md` a `mvp-scope.md` — vysvětluje **proč** je zvolený integrační přístup takový, jaký je (Strava = reálný OAuth2 adaptér, Garmin/MySASY = kontrakt + mock provider + souborový import, Google Sheets = souborový import). Obsahuje jen fakta ověřená v této session z oficiálních zdrojů poskytovatelů, s přesnými odkazy a datem ověření. Kde se fakt nepodařilo ověřit z oficiálního zdroje, je to výslovně uvedeno — nic není odhadováno.

**Datum ověření všech zdrojů v tomto dokumentu: 2026-09-15.**

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

## 5. Souhrnná tabulka

| Poskytovatel | Dostupnost reálné integrace nyní | Schvalovací proces | Doporučený MVP fallback |
|---|---|---|---|
| **Strava** | **Ano** — plně samoobslužné, zdarma | Ne, jen standardní registrace aplikace | Není potřeba fallback — reálný OAuth2 adaptér je přímo v MVP |
| **Garmin** (Connect Developer Program / Health API) | **Ne** — oficiálně "only for business use", formální schválení, nejisté pro nezávislý projekt | Ano, formální žádost + review Garminem, možné licenční poplatky pro komerční metriky | Kontrakt `IIntegrationProvider` + mock/demo provider + souborový import z uživatelského exportu |
| **MySASY** | **Omezeně** — myAPI existuje a má veřejnou Swagger dokumentaci, ale reálný přístup je řízen přes kontakt s MySASY (`api@mysasy.com`), ne okamžitý self-serve | Ano, kontaktní/partnerský model (nižší bariéra než Garmin, ale ne plně automatické) | Kontrakt `IIntegrationProvider` + mock/demo provider + generický CSV import (konkrétní MySASY export formát nepodařilo se ověřit z oficiálního zdroje) |
| **Google Sheets** | **Ano** (OAuth API), ale **MVP zvolil jednodušší cestu** | Ne pro API samotné (jen Google Cloud projekt); ano (verifikace) jen pro širší produkční publikování OAuth aplikace mimo testovací uživatele | CSV export/import bez OAuth — zvolená MVP cesta; živé OAuth API napojení zdokumentováno jako budoucí možnost |
