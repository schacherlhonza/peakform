# 0005. Authentication: ASP.NET Core Identity + custom JWT access tokens + refresh token rotation

## Stav

Přijato.

## Kontext

TrainCoach potřebuje autentizaci pro dvě role (sportovec, trenér) s podporou pro více zařízení/session na uživatele (mobil i web), bezstavové ověřování API požadavků (vhodné pro SPA frontend a budoucí mobilní klient) a možnost revokovat přístup (odhlášení konkrétního zařízení, reakce na podezřelou aktivitu, reset hesla).

Čistě session-cookie přístup komplikuje budoucí nativní mobilní klienta; čistě dlouhodobý JWT bez rotace zvyšuje dopad úniku tokenu, protože token nelze snadno invalidovat před vypršením.

## Rozhodnutí

- Správu uživatelů, hesel a reset hesla řeší **ASP.NET Core Identity** (`UserManager`, `SignInManager`).
- Po přihlášení backend vydává vlastní **krátkodobý JWT access token** pro autorizaci API požadavků.
- Souběžně se vydává **refresh token**, reprezentovaný entitou `RefreshToken`: v databázi je uložena pouze **hashovaná hodnota**, s vazbou na relaci/zařízení, datem expirace a stavem (aktivní/nahrazený/revokovaný).
- Při každém použití refresh tokenu dochází k **rotaci**: starý token se invaliduje, vydá se nový pár access+refresh token. Opakované použití již nahrazeného tokenu je považováno za signál úniku a vede k revokaci celé rodiny tokenů dané relace.

## Konsekvence

**Pozitivní:**

- Krátká životnost access tokenu omezuje dopad jeho úniku (např. zachyceného v logu nebo XSS scénáři) — token brzy vyprší i bez explicitní revokace.
- Rotace + detekce reuse refresh tokenu umožňuje odhalit a zastavit zneužití ukradeného refresh tokenu.
- Jeden řádek `RefreshToken` na relaci/zařízení umožňuje uživateli vidět a jednotlivě odvolat přístup konkrétního zařízení ("odhlásit toto zařízení" / "odhlásit všude"), což je užitečné i bezpečnostně žádoucí u aplikace se zdravotními daty.
- Stejný mechanismus funguje pro webový SPA klient i pro budoucí mobilní/nativní klienta bez architektonické změny.

**Negativní / rizika:**

- Vyžaduje pečlivou implementaci rotace a ukládání pouze hashované hodnoty (nikdy tokenu v čitelné podobě) — chyba v této logice by oslabila celý bezpečnostní přínos.
- Klient (frontend) musí správně implementovat tichou obnovu (silent refresh) access tokenu při jeho expiraci, včetně zpracování stavu "refresh token byl revokován → nutné nové přihlášení".
- O něco komplexnější než jednoduchá session cookie, ale nutné pro plánovanou podporu více zařízení a budoucího mobilního klienta.
