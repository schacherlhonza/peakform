# 0004. UI library: Mantine

## Stav

Přijato.

## Kontext

Frontend TrainCoach (React + TypeScript + Vite) potřebuje sadu UI komponent pro formuláře, date pickery (tréninkové plány, check-iny, závody), notifikace/toasty a obecné rozvržení. Vzhledem k tomu, že sportovec bude aplikaci často používat na mobilu (ranní/večerní check-in) a data obsahují citlivé zdravotní informace, je důležitá **přístupnost** (accessibility) formulářových prvků a konzistentní chování napříč komponentami bez nutnosti skládat vlastní date picker/notifikační systém od nuly.

## Rozhodnutí

Používáme **Mantine** jako primární UI knihovnu na frontendu, ve spojení s React Hook Form + Zod pro formuláře a validaci.

## Konsekvence

**Pozitivní:**

- Mantine nabízí přístupné (accessible) formulářové komponenty, date/time pickery a notifikační systém "out of the box", což pokrývá velkou část potřeb TrainCoach (plánování tréninků s daty, check-iny, upozornění na reporty) bez vlastní implementace.
- Dobrá podpora TypeScript a integrace s React Hook Form.
- Aktivně udržovaná knihovna s rozumnou velikostí bundle a themovacími možnostmi (lze přizpůsobit vzhled značce TrainCoach).

**Negativní / rizika:**

- Vázanost na konkrétní knihovnu — případná budoucí změna vizuálního stylu nebo migrace na jinou knihovnu by znamenala přepis komponent ve `shared/components` a feature-folderech, které Mantine přímo používají.
- i18n textů uvnitř samotných komponent Mantine (např. formát datumu v date pickeru) je nutné explicitně nakonfigurovat pro češtinu jako výchozí jazyk, aby korespondovalo s react-i18next nastavením zbytku aplikace.
