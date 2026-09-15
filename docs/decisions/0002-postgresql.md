# 0002. PostgreSQL as the primary database

## Stav

Přijato.

## Kontext

TrainCoach potřebuje relační databázi pro strukturovaná data s mnoha vzájemnými vztahy (uživatelé, vztahy trenér–sportovec, tréninkové plány se týdny/tréninky/segmenty, wellness záznamy, reporty). Data musí být dobře dotazovatelná, konzistentní (cizí klíče, transakce) a databáze musí mít dobrou podporu v .NET/EF Core ekosystému i solidní open-source dostupnost bez licenčních nákladů.

Zvažovali jsme použití pokročilých Postgres-specifických typů (např. `jsonb`) pro polostrukturovaná data, jako jsou segmenty tréninku.

## Rozhodnutí

Používáme **PostgreSQL** přes Npgsql/EF Core jako primární databázi. Doménový model je vědomě navržen tak, aby **nezávisel na Postgres-specifických typech** — konkrétně `WorkoutSegment` je normalizovaná dceřiná tabulka s explicitními sloupci, ne `jsonb` blob. Stejný princip platí pro ostatní strukturovaná data v doméně.

## Konsekvence

**Pozitivní:**

- PostgreSQL je vyzrálý, open-source, se silnou podporou v EF Core (Npgsql provider) a širokou dostupností u cloud poskytovatelů i pro self-hosting.
- Bez `jsonb` a jiných Postgres-specifických konstrukcí zůstává model **portable** — jde dotazovat, validovat a hlavně **testovat i proti SQLite** (relational provider) v integračních testech, kde Docker/Testcontainers nejsou v autorském sandboxu k dispozici.
- Normalizovaná struktura (`WorkoutSegment` jako tabulka) umožňuje standardní EF Core migrace, indexy a dotazy nad jednotlivými segmenty (např. agregace typů segmentů napříč tréninky), což by s JSON blobem bylo výrazně těžkopádnější.

**Negativní / rizika:**

- Připravujeme se o některé pokročilé Postgres funkce (např. nativní `jsonb` dotazování), které by mohly zjednodušit ukládání skutečně nestrukturovaných dat — pokud taková potřeba v budoucnu vznikne (např. syrová data z externího API před namapováním), řeší se explicitně jako samostatný, jasně označený sloupec/tabulka, ne jako implicitní součást jádra domény.
- SQLite v testech nemá 1:1 stejné chování jako Postgres ve všech ohledech (např. case-sensitivity řetězců, některé datumové funkce) — CI proto navíc spouští reálný Postgres kontejner pro finální validaci.
