# Oura / WHOOP — Activation Guide

Architektura je připravena (`IIntegrationProvider` + `IWellnessDataProvider` implementace, `ConnectorDomainPolicy` defaulty, DB enum hodnoty, konfigurace), ale **žádné reálné volání API nebylo implementováno** — v tomto repository neexistují reálné vývojářské credentials pro Oura ani WHOOP, a žádné endpointy nebyly vymýšleny bez ověření v aktuální oficiální dokumentaci.

## Stávající stav

- `TrainCoach.Integrations.Oura.OuraIntegrationProvider` / `TrainCoach.Integrations.Whoop.WhoopIntegrationProvider` — registrované v DI (`TrainCoach.Integrations/DependencyInjection.cs`), implementují oba potřebné kontrakty, ale každá metoda vyhazuje `BusinessRuleException` s jasnou zprávou, dokud nejsou vyplněné `ClientId`/`ClientSecret`.
- `OuraOptions`/`WhoopOptions` — stejný tvar jako `StravaOptions`/`IntervalsIcuOptions`, `IsConfigured` je vždy `false` bez reálných hodnot.
- `IntegrationProviderType.Oura = 5`, `.Whoop = 6`; `DataSource.Oura = 7`, `.Whoop = 8` — append-only, nikdy nepřečíslovávat existující hodnoty.
- `appsettings.json`/`.env.example`/`docker-compose.yml` mají prázdné placeholdery (`Integrations:Oura`, `Integrations:Whoop`).
- Výchozí `ConnectorDomainPolicy` (v `ConnectorPolicyService.DefaultMode`, pro budoucí použití): `Activities = EnrichmentOnly`, `Sleep/Hrv/RestingHeartRate = Secondary`, `VendorScores = Primary`, ostatní `Disabled` — odráží zamýšlenou roli obou zdrojů (přímá wellness/recovery data, nikdy primární zdroj aktivit).

## Jak aktivovat, až budou credentials k dispozici

1. **Zaregistrovat vývojářskou aplikaci** u Oura (https://cloud.ouraring.com/oauth/applications) a/nebo WHOOP (https://developer.whoop.com) — mimo rozsah tohoto repozitáře, vyžaduje reálný účet u poskytovatele.
2. **Nastavit `.env`**: `OURA_CLIENT_ID`, `OURA_CLIENT_SECRET`, `WHOOP_CLIENT_ID`, `WHOOP_CLIENT_SECRET` (redirect URI defaulty jsou už v `.env.example`).
3. **Ověřit aktuální API dokumentaci** poskytovatele k datu implementace — endpointy, scopes, rate limity a datové typy se mohou od doby psaní tohoto dokumentu změnit. Nikdy nepředpokládat tvar odpovědi bez ověření.
4. **Implementovat reálné OAuth/fetch metody** v `OuraIntegrationProvider`/`WhoopIntegrationProvider`, podle vzoru `IntervalsIcuIntegrationProvider` (nejnovější reálný adaptér v tomto repu — `TrainCoach.Integrations/IntervalsIcu/IntervalsIcuIntegrationProvider.cs`):
   - `BuildAuthorizationUrl`/`ExchangeCodeAsync`/`RefreshTokenAsync` — standardní OAuth2 authorization code flow.
   - `FetchWellnessAsync` — namapovat na `ExternalWellnessSample` (viz `TrainCoach.Application.Integrations.IWellnessDataProvider`).
   - `FetchRecentActivitiesAsync` — jen pokud a v rozsahu, ve kterém to `ConnectorDomainPolicy` (viz výše, `EnrichmentOnly`) skutečně využije.
5. **WHOOP specificky**: pokud a až budou implementovány webhooky (mimo rozsah tohoto MVP, viz `docs/integrations/activity-matching.md` "Doporučené další kroky"), deduplikovat podle `trace_id`, webhook jen zařadit do fronty a rychle vrátit `2xx`, aktuální objekt načíst z API až následně — nikdy důvěřovat webhook payloadu jako plnému zdroji dat.
6. **Aktualizovat `ConnectorDomainPolicy` defaulty**, pokud se zamýšlená role liší od tabulky výše.
7. **Odstranit `RequireConfigured()` guard** až po ověření, že reálné volání funguje end-to-end (manuální test připojení + jedné synchronizace).
8. **Zvážit frontend**: `IntegrationsPage.tsx`'s provider grid dnes Oura/WHOOP nezobrazuje vůbec (žádné karty) — přidat je až bude reálná integrace funkční, ne dřív, aby uživatelé nezkoušeli připojit něco, co ještě nefunguje.

## Co nedělat

- Nevymýšlet endpointy bez ověření v aktuální oficiální dokumentaci.
- Nescrapovat, nesimulovat přihlášení uživatelským jménem/heslem.
- Neukládat OAuth tokeny jinak než přes existující `ITokenEncryptor` (stejný mechanismus jako Strava/intervals.icu).
- Nepředstírat dokončenou integraci, která ve skutečnosti čeká na schválení/registraci u poskytovatele.
