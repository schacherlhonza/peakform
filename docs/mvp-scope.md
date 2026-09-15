# TrainCoach — Rozsah MVP a další vývoj

## 1. MVP (tato fáze vývoje)

| Oblast | Co je v MVP |
|---|---|
| Identita a přístup | Registrace/přihlášení (ASP.NET Core Identity + JWT), role sportovec/trenér, pozvánka a potvrzení vztahu trenér–sportovec, granulární oprávnění, odvolání souhlasu |
| Plánování | Sezóny, cíle, závody, tréninkové plány s týdny a strukturovanými tréninky (segmenty), vlastní zkratky (CustomAbbreviation) |
| Realizace | Ruční zápis aktivity, import souborů, propojení se Strava (OAuth2, reálná synchronizace), párování plán vs. skutečnost, komentáře, zpětná vazba k tréninku |
| Wellness | Ranní/večerní check-in, hlášení bolesti/nemoci, záznam spánku, HRV, klidová tepovka, osobní rekordy |
| Výživa | Základní zápis jídla a hydratace |
| Reporty | Denní/týdenní pravidly řízený report (výpočet metrik → pravidla → text → doručení v aplikaci) |
| Integrace | Strava (plně funkční), Garmin/MySASY (kontrakt + mock provider, viz níže), Google Sheets (souborový import) |
| Bezpečnost | IDOR ochrana, šifrované credentials, rotace refresh tokenů, audit log, export/smazání dat |
| Platforma | Web aplikace (React), responzivní/mobil-friendly, čeština jako výchozí jazyk |

## 2. Další verze (Next version — krátkodobě po MVP)

- Notifikace e-mailem (nad rámec in-app notifikací z MVP).
- Rozšířené filtrování a hledání v historii tréninků/reportů pro trenéra s velkým počtem sportovců.
- Hromadné akce trenéra (např. komentář/oznámení pro více sportovců najednou).
- Konfigurovatelnost pravidlového enginu reportů přes UI (ne jen přes konfigurační soubor/kód).
- Detailnější grafy trendů (spánek, HRV, tréninková zátěž v čase) na dashboardu.
- AI vysvětlení reportu v přirozeném jazyce (striktně jako přeformulování výstupu pravidel — viz `security.md`, §13).

## 3. Budoucí možnosti (Future — bez závazku k termínu)

- Nativní/mobilní klient (PWA nebo nativní aplikace) využívající stávající REST API bez přepisu backendu.
- Push notifikace na mobilní zařízení.
- Pokročilá analytika tréninkové zátěže (např. ACWR, periodizační doporučení) nad rámec základních pravidel MVP.
- Vytěžení vybraných modulů (Integrations, Reporting) do samostatných služeb, pokud si to vyžádá reálná zátěž (viz `decisions/0001-modular-monolith.md`).
- Vícejazyčná lokalizace nad rámec češtiny (architektura je i18n-ready od začátku).

## 4. Blokované/čekající externí integrace

| Integrace | Stav v MVP | Důvod blokace | Co ji odblokuje |
|---|---|---|---|
| **Garmin** (Health API / Connect Developer Program) | Kontrakt `IIntegrationProvider` + mock/demo provider + souborový import fallback | Vyžaduje schválení přístupu vývojářským programem Garmin (není veřejně samoobslužné jako Strava) | Podání žádosti o přístup do Garmin Connect Developer Program a její schválení; poté implementace reálného adaptéru za existující kontrakt |
| **MySASY** | Kontrakt `IIntegrationProvider` + mock/demo provider + souborový import fallback | Neexistuje veřejně dokumentované API; vyžaduje individuální domluvu s poskytovatelem | Získání přístupu/dokumentace API od MySASY (obchodní/partnerská domluva) |
| **Google Sheets — živá obousměrná synchronizace** | Pouze jednorázový/opakovaný **souborový import** (upload exportu, mapování, validace, náhled, idempotentní re-import) | Živé API napojení na Google Sheets zvyšuje komplexitu (autorizace Google účtu, obousměrná synchronizace, řešení konfliktů) bez jasné MVP potřeby — souborový import pokrývá primární use-case migrace z existující tabulky | Poptávka od uživatelů po živé synchronizaci a alokace kapacity na implementaci Google API OAuth flow a řešení konfliktů zápisu |

### Poznámka k principu

Ve všech třech blokovaných případech platí stejný architektonický přístup: `IIntegrationProvider` kontrakt existuje od začátku, takže po odblokování (schválení přístupu, získání API) stačí doplnit konkrétní implementaci adaptéru — Application vrstva a zbytek systému se nemění (viz `architecture.md`, `decisions/0006-integration-strategy.md`). Nikdy se nepoužívá scraping ani ukládání hesel uživatele k třetí straně jako náhrada za chybějící oficiální API.
