# Čas v tepových zónách

U každé aktivity s uloženým tepovým streamem se počítá, kolik času sportovec strávil v jednotlivých tepových
zónách. Výsledek se zobrazuje na detailu aktivity a jako týdenní souhrn na přehledu.

| Část | Soubor |
|---|---|
| Výpočet (bez DB) | `backend/src/TrainCoach.Application/Execution/Streams/HrZoneTimeCalculator.cs` |
| Uložení a přepočet | `backend/src/TrainCoach.Application/Execution/HrZoneRecomputeJob.cs` |
| UI | `frontend/src/activities/HrZoneBars.tsx`, `frontend/src/features/athlete-dashboard/WeekHrZonesCard.tsx` |

## Výpočet

- Každý vzorek streamu platí až do dalšího vzorku. Uložený stream je zmenšený, takže vzorky jsou od sebe 1–9 s.
  Mezera delší než 30 s je pauza a do žádné zóny se nepočítá.
- **Jen čas v pohybu**, pokud má stream rychlost (nad 0,5 m/s). Zobrazené trvání aktivity je čas v pohybu, a hodinky
  často nahrávají i stání: 30minutová chůze v 76minutovém záznamu by jinak měla v zónách 1:04 h. Bez rychlosti
  (posilování, indoor) se počítá celý záznam kromě pauz. Rozdíl několika minut proti trvání od Garminu je normální,
  protože Garmin určuje pohyb vlastní automatickou pauzou nad plnými daty.
- **Tep pod nejnižší zónou se počítá zvlášť** (`TimeBelowHrZones`, v UI řádek „Pod Z1“), ne do zóny 1. Když zóna 1
  začíná na 130, chůze na 84 tepech v žádné zóně není. Tep nad nejvyšší zónou se počítá do nejvyšší zóny, tep
  v mezeře mezi rozsahy dvou zón do zóny pod ní.
- Použijí se zóny platné v den aktivity (`HeartRateZone.EffectiveFromDate`). **Aktivity starší než první sada zón
  používají tu nejstarší sadu**: archiv sahá roky zpět, zóny se nastavují až teď, a přesnější odhad nemáme.
- Výsledek je metrika aktivity `TimeInHrZone1..7` a `TimeBelowHrZones` (v sekundách) se zdrojem `DataSource.PeakForm`.

## Kdy se přepočítává

Přepočet běží na pozadí:

- po uložení zón: všechny aktivity, protože se mohly změnit hranice,
- po importu archivu Stravy, který přidal streamy: jen aktivity, které zóny ještě nemají,
- po stažení streamů z intervals.icu: jen aktivity, které zóny ještě nemají.

Aktivity bez uloženého streamu (graf se stahuje živě ze Stravy) čas v zónách nemají.

## Graf

Zóny jsou pořadová škála, takže graf používá jeden odstín v pěti stupních od tlumeného po jasný (Z1 → Z5), ne
různé barvy. Paleta prošla validátorem (`--ordinal --mode dark`, povrch `#111f1b`). Délka pruhu je podíl na
celkovém čase. Řádek „Pod Z1“ je neutrální šedá (`#7d8984`) mimo škálu zón. Hodnota a podíl jsou u konce pruhu v barvě textu, při najetí myší se ukáže tooltip.
