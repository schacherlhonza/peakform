# TrainCoach — Bezpečnost a ochrana soukromí

## 1. Proč je toto kritické

TrainCoach ukládá citlivá zdravotní a wellness data — spánek, HRV, klidovou tepovou frekvenci, hlášení bolesti a nemoci. Tato data mají vyšší citlivost než běžná provozní data SaaS aplikace a vyžadují odpovídající úroveň ochrany: striktní autorizaci na úrovni jednotlivého záznamu, explicitní souhlas sportovce s jejich sdílením, šifrování citlivých přihlašovacích údajů a auditovatelnost přístupu.

## 2. Model hrozeb (shrnutí)

| Hrozba | Dopad | Zmírnění |
|---|---|---|
| Trenér/uživatel si upraví ID v URL a přečte cizí data (IDOR) | Únik zdravotních dat jiného sportovce | Autorizační politika vázaná na `CoachAthleteRelationship`/vlastnictví, viz §3 |
| Únik integračního tokenu (Strava apod.) z databáze | Útočník získá přístup k externímu účtu sportovce | Šifrování `IntegrationCredential` at rest, viz §4 |
| Krádež/replay refresh tokenu | Trvalý neoprávněný přístup k účtu | Hashované uložení, rotace, detekce reuse, revokace, viz §5 |
| Hrubá síla na login / API zneužití | Kompromitace účtů, DoS | Rate limiting, uzamykání účtu, viz §6 |
| Trenér vidí data sportovce bez jeho souhlasu | Porušení soukromí, právní riziko | Consent-gated přístup, viz §9 |
| Report interpretován jako lékařská diagnóza | Zdravotní riziko pro sportovce, právní riziko | Explicitní no-diagnosis pravidlo, viz §10 |
| XSS/CSRF/clickjacking na frontendu | Krádež session, neautorizované akce | Security headers, CORS, viz §7–8 |
| Uživatel chce data smazat/exportovat a systém to neumožní | Porušení GDPR | Export/erasure podpora, viz §11 |

## 3. Ochrana proti IDOR (Insecure Direct Object Reference)

- Žádný endpoint nesmí vracet ani upravovat data sportovce jen na základě ID předaného v požadavku.
- Autorizace je řešena jako **sdílená politika/handler** na Application/Api vrstvě (ne opakovaně ručně v každém kontroleru), který pro daný požadavek ověří jedno z:
  1. **Přímé vlastnictví** — přihlášený uživatel žádá o svá vlastní data.
  2. **Aktivní `CoachAthleteRelationship`** ve stavu "aktivní" mezi trenérem a sportovcem, jehož data jsou požadována, **a** odpovídající `RelationshipPermission` pro danou kategorii dat (tréninková data odděleně od zdravotních/wellness dat).
- Kontrola se vyhodnocuje při každém přístupu (ne jen jednou při vytvoření session) — odvolaný souhlas nebo zúžené oprávnění se projeví okamžitě.
- Testováno integračními testy, které cíleně zkoušejí přístup jednoho sportovce/trenéra k datům jiné dvojice (negativní testovací scénáře jsou povinnou součástí test suite, ne jen šťastná cesta).

## 4. Šifrované úložiště integračních přihlašovacích údajů

- `IntegrationCredential` (OAuth tokeny Strava, budoucí Garmin/MySASY) se v databázi ukládá **šifrovaně at rest** (aplikační šifrování před zápisem, ne spoléhání jen na šifrování disku).
- Šifrovací klíč je spravován odděleně od databáze (konfigurace/secret manager prostředí, ne v repozitáři, ne v `appsettings.json` s reálnou hodnotou).
- Nikdy se neukládají hesla uživatele k třetí straně (žádné scraping přihlašování jménem/heslem) — pouze OAuth tokeny vydané poskytovatelem v rámci standardního OAuth2 flow.
- Dešifrovaná hodnota se drží v paměti jen po dobu nutnou k volání externího API a nikdy se neloguje.

## 5. Refresh token rotace a revokace

- Access token (JWT) má krátkou životnost a nese jen nutné claims (uživatel, role) — sám o sobě neumožňuje dlouhodobý přístup po odcizení.
- `RefreshToken` entita ukládá **hash** tokenu (ne token samotný), datum vydání/expirace, informaci o zařízení/relaci a stav (aktivní/revokovaný/nahrazený).
- Při každém použití refresh tokenu dojde k **rotaci**: starý token se označí jako nahrazený, vydá se nový pár access+refresh token.
- Pokus o opakované použití již nahrazeného (tedy neplatného) refresh tokenu je signálem možného úniku — vede k revokaci celé rodiny tokenů dané relace a vynucení nového přihlášení.
- Uživatel může v nastavení vidět aktivní relace/zařízení a jednotlivé odvolat ("odhlásit toto zařízení" / "odhlásit všude").

## 6. Rate limiting

- Přihlašovací endpoint (a reset hesla) má přísný rate limit podle IP i podle e-mailu/uživatele, aby se omezily útoky hrubou silou.
- Obecné API endpointy mají rozumný rate limit na uživatele/token, aby jednotlivá integrace/chyba na klientovi nemohla zahltit backend.
- Po překročení limitu se vrací standardní `429 Too Many Requests` s `Retry-After`.

## 7. CORS

- Povolené origins jsou explicitně nakonfigurované (frontend produkční doména, lokální dev origin) — žádné `AllowAnyOrigin` v kombinaci s credentials.
- Pouze nutné HTTP metody a hlavičky jsou povoleny; preflight odpovědi jsou cachované na rozumnou dobu.

## 8. Bezpečnostní hlavičky

- `Content-Security-Policy` omezující zdroje skriptů/stylů.
- `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` (nebo `frame-ancestors` v CSP) proti clickjackingu.
- `Strict-Transport-Security` v produkci (HTTPS only).
- `Referrer-Policy` omezující únik URL s citlivými parametry.
- API odpovědi nikdy neobsahují citlivé hlavičky/detaily výjimek v produkčním prostředí (žádné stack trace uživateli).

## 9. Heslo a reset flow

- Hesla se ukládají výhradně jako hash spravovaný ASP.NET Core Identity (PBKDF2/Argon2 podle konfigurace Identity), nikdy v čitelné podobě.
- Reset hesla probíhá přes jednorázový, časově omezený token zaslaný na ověřený e-mail; token je jednorázový a invaliduje se po použití nebo vypršení.
- Po resetu hesla se revokují všechny existující refresh tokeny uživatele (vynucené odhlášení ze všech zařízení).
- Volitelně (podle rozsahu MVP) uzamykání účtu po opakovaných neúspěšných pokusech o přihlášení (Identity lockout).

## 10. Souhlas sportovce a přístup trenéra (consent model)

- Trenér nemůže vytvořit ani aktivovat `CoachAthleteRelationship` jednostranně — vztah vzniká **pozvánkou** a vyžaduje explicitní **potvrzení sportovcem**.
- Sportovec při potvrzení vidí a může upravit, jaké kategorie dat trenérovi zpřístupňuje (`RelationshipPermission`) — např. tréninková data ano, wellness/zdravotní data ne.
- Sportovec může kdykoliv oprávnění zúžit nebo vztah ukončit; ukončení/zúžení má okamžitý efekt na autorizaci (viz §3).
- Historie souhlasu (kdy byl udělen, kdy případně odvolán) je auditovatelná přes `AuditLog` a `CoachAthleteRelationship` (start/end date, stav).

## 11. Export dat a právo na výmaz

- Sportovec i trenér mohou požádat o **export** svých dat ve strojově čitelném formátu (naplnění principu přenositelnosti dat dle GDPR).
- Na žádost o **smazání účtu** dojde k soft delete uživatele; navázané citlivé záznamy jsou anonymizovány/omezeny v souladu s právními požadavky, přičemž agregovaná/anonymizovaná historie nezbytná pro integritu dat druhé strany (např. historie komentářů trenéra) může být zachována bez osobní identifikace.
- Proces exportu/výmazu je auditovaný (`AuditLog`) a dostupný přes samoobslužné UI, ne jen ruční zásah administrátora.

## 12. Auditní log

- `AuditLog` zaznamenává citlivé operace: přístup trenéra ke zdravotním datům sportovce, změny oprávnění, export/smazání dat, změny hesla, revokaci relací.
- Auditní log je určen primárně pro zpětnou dohledatelnost (kdo, kdy, co), ne pro běžné provozní logování — ukládá se odděleně od aplikačních logů.

## 13. Reporty nejsou lékařská diagnóza

- `GeneratedReport` je vždy výstupem **pravidlového enginu** (rule-based), postaveného na měřitelných metrikách a konfigurovatelných prazích/pravidlech — nikdy výstupem AI modelu, který by "diagnostikoval" zdravotní stav.
- Pokud existuje AI komponenta, která report doprovází, smí výhradně **přeformulovat/vysvětlit v přirozeném jazyce** výstup, který už pravidlový engine vypočítal — nikdy nesmí:
  - sama vyhodnocovat zdravotní stav mimo definovaná pravidla,
  - vydávat se za lékařské doporučení nebo diagnózu,
  - autonomně měnit tréninkový plán sportovce.
- Každý report obsahuje viditelné upozornění, že nejde o lékařské posouzení a že při zdravotních potížích je třeba vyhledat odborníka.
- Změna tréninkového plánu na základě reportu je vždy akcí trenéra (člověka), report nanejvýš doporučuje/upozorňuje.
