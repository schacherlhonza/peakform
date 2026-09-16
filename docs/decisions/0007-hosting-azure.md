# 0007. Hosting a release pipeline na Azure (rozhodnutí bez implementace)

## Stav

Přijato jako záměr. **Infrastruktura zatím není provisionovaná ani napojená** — tento ADR
zaznamenává rozhodnutí o cílové architektuře, ne hotový stav. Implementace (IaC, CD workflow)
proběhne až v samostatné relaci, jakmile bude založená Azure subscription.

## Kontext

Repozitář (`https://github.com/schacherlhonza/peakform.git`) je založený, `.github/workflows/ci.yml`
běží (build + test backendu proti reálnému Postgres kontejneru a frontendu). Dalším krokem je
navázat na skutečné hostování — backend API, frontend a Postgres databázi — a release pipeline,
která tam bude z GitHub Actions nasazovat.

Požadavky:

- Backend je čistě kontejnerizovaný (`backend/Dockerfile`), stejně frontend (`frontend/Dockerfile`
  + `nginx.conf`), Postgres se v lokálním vývoji spouští přes `docker-compose.yml`.
- Vlastník chce založit **novou, samostatnou Azure subscription** vyhrazenou pro tento a další
  vlastní (osobní) projekty — ne sdílet existující firemní/jinou subscription. Volba architektury
  proto musí počítat s tím, že v téže subscription časem přibudou další, nezávislé projekty.
- GitHub (ne Azure DevOps) už je zvolený jako místo repozitáře a CI (viz commit historie) — Azure
  DevOps Repos/Pipelines jako celá platforma se proto nezvažuje, řeší se jen cílová hostingová
  vrstva na Azure straně, napojená přes GitHub Actions.
- **Lokální vývoj a testování musí zůstat funkční přesně jako dosud** (`docker compose up`) —
  volba Azure služby pro produkční hosting nesmí vyžadovat změnu lokálního workflow.

## Rozhodnutí

| Komponenta | Azure služba | Důvod |
|---|---|---|
| Backend API | **Azure Container Apps** | Consumption-based serverless kontejnery se scale-to-zero — pro provoz s nízkým/nárazovým provozem výrazně levnější než App Service s "always on". Přímo spotřebovává existující `backend/Dockerfile`, žádná změna backend kódu. |
| Frontend | **Azure Static Web Apps** | Frontend je po `npm run build` čistě statický bundle (žádný SSR) — SWA má štědrý free tier, vestavěné CDN a vlastní GitHub Action pro build+deploy jedním krokem. Produkční nasazení frontendu tak **nebude** používat `frontend/Dockerfile`/`nginx.conf`. |
| Databáze | **Azure Database for PostgreSQL – Flexible Server** (Burstable B1ms pro start) | Managed Postgres bez vlastní správy patchování a zálohování; snadný upgrade tieru, až/pokud provoz poroste. |
| Container registry | **Azure Container Registry** | Cíl pro backend image z GitHub Actions; odsud si Container App stahuje novou revizi. |
| Tajemství | **Azure Key Vault** | JWT signing key, connection string k DB, Strava client secret — nikdy v repu ani v plaintext app settings. |
| Subscription/organizace | **Nová, samostatná subscription** vyhrazená pro osobní projekty, s konvencí **jedna resource group na projekt** (např. `rg-peakform-prod`, `rg-peakform-dev`) | Umožňuje v budoucnu přidávat další nezávislé osobní projekty do stejné subscription bez kolizí pojmenování/nákladového sledování — náklady i zdroje jdou filtrovat per-projekt přes resource group i tagy (`project=peakform`). |

### Lokální vývoj zůstává beze změny

`frontend/Dockerfile` a `nginx.conf` **zůstávají v repozitáři** i po přechodu na Static Web Apps —
neslouží produkčnímu nasazení, ale zachovávají možnost spustit celý stack (`postgres` + `backend`
+ `frontend`) jedním `docker compose up`, přesně jako dosud. Volba hostingové služby na Azure
straně je nezávislá na lokálním vývojovém workflow; Docker Compose zůstává jediným způsobem
lokálního běhu, dokud se nerozhodne jinak.

### Release pipeline (návrh pro pozdější implementaci, zatím neexistuje)

1. Push na `master`/tag → stávající `ci.yml` (build + test) → při úspěchu build & push backend
   image do ACR.
2. `dotnet ef database update` proti reálné Azure Postgres instanci (connection string z GitHub
   Secrets) — stejný princip jako dnešní CI krok proti ephemeral Postgres kontejneru.
3. `az containerapp update` (oficiální GitHub Action) nasadí novou revizi backendu.
4. `Azure/static-web-apps-deploy` action postaví a nasadí frontend.

## Konsekvence

**Pozitivní:**

- Container Apps + Static Web Apps drží náklady u malého/demo provozu blízko nule (oba mají
  consumption/free tier), na rozdíl od App Service, který běží "always on" i bez provozu.
- Resource-group-per-projekt konvence v jedné subscription škáluje na další budoucí osobní
  projekty bez nutnosti zakládat novou subscription pokaždé.
- GitHub Actions se pro CD rozšiřuje ze stejného workflow systému, který už CI používá — žádný
  nový nástroj k naučení.
- Lokální vývoj (`docker compose up`) zůstává nedotčený, riziko "funguje to jen v cloudu" se tím
  minimalizuje.

**Negativní / rizika:**

- Static Web Apps má jiný routing/reverse-proxy model než nginx (`nginx.conf` dnes řeší SPA
  fallback a případný API proxy) — při reálné implementaci je nutné ověřit, že `staticwebapp.config.json`
  pravidla pokryjí stejné chování (SPA fallback na `index.html`, správné MIME typy), než se
  `nginx.conf` cesta v produkci opustí.
- Container Apps scale-to-zero znamená cold start po delší nečinnosti — u API s JWT refresh flow
  a wellness/report generováním by to při prvním requestu po probuzení mohlo znamenat znatelnou
  latenci; při reálném provozu zvážit `minReplicas: 1`, pokud to bude vadit.
- Vendor lock-in na Azure-specifické služby (Container Apps, SWA) — přijatelné riziko vzhledem k
  tomu, že šlo o vědomou volbu pro osobní/menší projekty, ne enterprise multi-cloud požadavek.
- Toto rozhodnutí **není zatím implementované** — IaC (Bicep/Terraform) a `cd.yml` workflow chybí
  a vzniknou až v navazující relaci po založení subscription.
