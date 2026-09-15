# 0006. Integration strategy: common provider contract, real Strava, mock/import fallback for the rest

## Stav

Přijato.

## Kontext

TrainCoach potřebuje získávat data o dokončených aktivitách a případně dalších metrikách z více externích zdrojů: Strava, Garmin, MySASY a existující Google Sheets tabulky, ze kterých trenér a sportovci dnes vycházejí. Tyto zdroje mají zásadně odlišnou dostupnost:

- **Strava** má veřejné, samoobslužné OAuth2 API pro vývojáře bez nutnosti schvalování pro základní scope.
- **Garmin** (Health API / Connect Developer Program) vyžaduje schválení přístupu vývojářským programem — není samoobslužné.
- **MySASY** nemá veřejně dokumentované API; přístup by vyžadoval individuální domluvu s poskytovatelem.
- **Google Sheets** jako zdroj dat je typicky jednorázový/opakovaný export existující tabulky uživatele, ne nutně kandidát na živé obousměrné API propojení pro MVP.

Nechceme, aby chybějící/opožděný přístup ke Garmin nebo MySASY API blokoval zbytek vývoje, a nechceme řešit tyto zdroje ad-hoc scrapingem nebo ukládáním hesel uživatelů k třetím stranám (bezpečnostní a often i smluvní riziko).

## Rozhodnutí

Všechny externí zdroje dat implementujeme za jednotným kontraktem **`IIntegrationProvider`** v `TrainCoach.Integrations`, na kterém Application vrstva nezávisí na konkrétním poskytovateli.

- **Strava**: reálný, plně funkční OAuth2 adaptér — bude fungovat, pokud si uživatel/provozovatel doplní vlastní Strava API klíče (client id/secret) do konfigurace.
- **Garmin, MySASY**: implementace kontraktu + **mock/demo poskytovatel** pro vývoj a demo účely + **souborový import** jako praktický fallback pro reálné použití, dokud není k dispozici oficiální přístup. **Žádný scraping, žádné ukládání hesel uživatele k třetí straně.**
- **Google Sheets**: **souborový import** (upload exportu, mapování sloupců, validace, náhled, idempotentní re-import), ne živá obousměrná synchronizace přes Google API v MVP.

## Konsekvence

**Pozitivní:**

- Application a zbytek systému znají integrace pouze přes `IIntegrationProvider` — přidání reálného Garmin/MySASY adaptéru po získání přístupu je izolovaná změna v `TrainCoach.Integrations`, bez zásahu do use-case logiky, kontrolerů nebo frontendové vrstvy.
- Mock/demo poskytovatel umožňuje vyvíjet a testovat celý tok (propojení účtu, synchronizace, zobrazení dat) i bez reálného přístupu ke Garmin/MySASY API.
- Souborový import (Google Sheets, i jako fallback pro Garmin/MySASY) pokrývá reálnou potřebu migrace z existující tabulky hned od MVP, bez čekání na schválení jakéhokoli API přístupu.
- Vyhýbáme se bezpečnostnímu a smluvnímu riziku scrapingu nebo ukládání přihlašovacích údajů uživatele k třetím stranám.

**Negativní / rizika:**

- Dokud není Garmin/MySASY přístup schválen, uživatelé závislí na těchto zdrojích musí používat souborový import nebo manuální zápis — méně pohodlné než živá synchronizace u Strava.
- Souborový import u Google Sheets vyžaduje uživatele, aby si sám opakovaně exportoval a nahrával soubor — nejde o "nastav jednou a zapomeň" zkušenost, kterou by nabídla živá API synchronizace (viz `mvp-scope.md` pro plán případného budoucího rozšíření).
- Kontrakt `IIntegrationProvider` musí být navržen dostatečně obecně, aby pokryl jak "push" API stahování dat (Strava), tak souborový import — je nutné mu věnovat pozornost při návrhu, aby nebyl zbytečně úzký nebo naopak příliš obecný na to, aby byl užitečný.
