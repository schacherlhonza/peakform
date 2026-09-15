# 0001. Modular monolith instead of microservices

## Stav

Přijato.

## Kontext

TrainCoach je nová aplikace vyvíjená malým týmem pro MVP s dosud neověřenou zátěží (počet trenérů/sportovců, objem synchronizací). Potřebujeme architekturu, která:

- je rychlá na vývoj a nasazení pro jeden tým,
- udržuje jasné hranice mezi doménovými oblastmi (identita, plánování, realizace, wellness, reporting, integrace),
- neuzavírá cestu k budoucímu škálování nebo rozdělení, pokud se ukáže potřeba.

Mikroslužby by přinesly síťovou latenci mezi voláními, nutnost řešit distribuované transakce a konzistenci napříč službami, provozní režii (service discovery, orchestrace, observabilita napříč více nasazeními) a duplicitní infrastrukturu (CI/CD, monitoring) pro každou službu zvlášť — to vše bez zátěže, která by tyto náklady pro MVP ospravedlnila.

## Rozhodnutí

Stavíme **modulární monolit**: jeden nasaditelný backend rozdělený do pěti .NET projektů podle vrstev (`Api`, `Application`, `Domain`, `Infrastructure`, `Integrations`) se striktním směrem závislostí (`Api → Application → Domain`, `Infrastructure`/`Integrations` implementují rozhraní z `Application`). Hranice mezi doménovými oblastmi (Identity, Planning, Execution, Wellness, Nutrition, Reporting, Integrations) se drží na úrovni namespace/složek a rozhraní, ne na úrovni samostatných procesů/sítě.

## Konsekvence

**Pozitivní:**

- Jedno nasazení, jedna databáze, jednoduchý lokální vývoj (`docker compose up`).
- Transakční konzistence v rámci jedné databáze bez sagy/distribuovaných transakcí.
- Nižší provozní náklady a kognitivní zátěž pro malý tým.
- Jasné hranice na úrovni kódu (projekty, rozhraní) usnadňují případné budoucí vytěžení modulu (např. `Integrations` nebo `Reporting`) do samostatné služby, pokud si to vyžádá reálná zátěž.

**Negativní / rizika:**

- Vyžaduje disciplínu — bez jasných pravidel (směr závislostí, žádné "zkratky" mezi moduly mimo definovaná rozhraní) monolit časem degraduje na "big ball of mud".
- Celá aplikace se škáluje horizontálně jako jeden celek (více instancí stejného procesu), ne po jednotlivých modulech — pro očekávanou zátěž MVP je to přijatelné.
- Nasazení jedné změny znamená nasazení celého backendu (mitigováno automatizovanými testy a CI).
