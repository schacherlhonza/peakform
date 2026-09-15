# 0003. Background jobs via BackgroundService + Channel<T>, not Hangfire/Quartz

## Stav

Přijato.

## Kontext

TrainCoach potřebuje zpracovávat asynchronní práci mimo HTTP request-response cyklus: noční/na-vyžádání generování reportů a spouštění synchronizačních běhů s integracemi (Strava apod.). Tato práce nesmí blokovat API požadavek a měla by přežít drobné zpoždění zpracování (fronta), ale pro MVP nepotřebujeme cron-like plánování napříč více instancemi, perzistentní dashboard úloh ani distribuované zpracování.

Zvažovali jsme Hangfire a Quartz.NET jako zavedená řešení pro background joby v .NET.

## Rozhodnutí

Pro MVP používáme **žádnou externí infrastrukturu** pro joby. Místo toho: `BackgroundService` (hostovaná služba v rámci ASP.NET Core procesu) konzumuje úlohy z in-process fronty `System.Threading.Channels.Channel<T>`. Producent (Application/Api vrstva) zapíše požadavek na práci do kanálu; `BackgroundService` v `TrainCoach.Infrastructure` jej zpracuje asynchronně.

## Konsekvence

**Pozitivní:**

- Žádná další infrastrukturní závislost (databáze pro Hangfire, Redis, samostatný worker proces) — méně provozní režie pro MVP.
- `Channel<T>` je součást .NET, dobře otestovaná, s nízkou latencí pro in-process producer/consumer scénář.
- Rozhraní použité voláním (např. `IJobQueue.EnqueueAsync(...)`) je stabilní abstrakce — implementaci lze později vyměnit bez zásahu do volajícího kódu.

**Negativní / rizika:**

- Fronta je **in-memory** — při restartu procesu se nezpracované úlohy ztratí. Pro MVP je to přijatelné riziko (joby jsou buď idempotentní/opakovatelné, nebo jde o nekritické generování reportů, které lze spustit znovu), ale není to vhodné pro úlohy vyžadující zaručené doručení.
- Nepodporuje nativně plánování "spusť v čase X" napříč restarty ani horizontální škálování (více instancí backendu by si úlohy neškálovaly/nesdílely frontu) — pro MVP s jednou instancí backendu to není blokující.
- Toto řešení je **vědomě dočasné a nahraditelné**: pokud reálná zátěž nebo požadavky na spolehlivost/plánování překročí možnosti in-process řešení, migrace na Hangfire nebo Quartz.NET je izolovaná změna v `TrainCoach.Infrastructure` za stávajícím rozhraním fronty, bez dopadu na Application vrstvu.
