# LotoHelper

Solution Aspire (.NET 8) + Angular pour collecter et exposer l’historique Loto officiel FDJ.

## Contenu de la solution
- **Loto.AppHost** : orchestration Aspire (API, Worker, Postgres, front Angular).
- **Loto.Api** : Minimal API (endpoint `/api/draws`, `/health`), EF Core 8 + Postgres.
- **Loto.Ingestion.Worker** : télécharge l’archive officielle “nouvelle formule” Loto, parse les tirages et les insère en base (idempotent).
- **Loto.Domain / Loto.Infrastructure** : entités (`Draw`) et DbContext (indexes uniques).
- **Frontend Angular** (`frontend/loto-frontend`) : consomme `/api/draws`, gère le cas “aucune donnée”.

## Prérequis
- .NET 8 SDK
- Node 18+ et npm
- Postgres (provisionné automatiquement par Aspire)

## Configuration clé (Worker)
Fichier `src/Loto.Ingestion.Worker/appsettings.json` :
```json
"Fdj": {
  "NewLotoArchiveUrl": "https://www.sto.api.fdj.fr/anonymous/service-draw-info/v3/documentations/1a2b3c4d-9876-4562-b3fc-2c963f66afp6",
  "IntervalMinutes": 1440,
  "MinRefreshAgeMinutes": 60,
  "UseLocalFile": false,
  "LocalArchivePath": ""
}
```
- `NewLotoArchiveUrl` : archive ZIP officielle FDJ “nouvelle formule”.
- `UseLocalFile` : passer à `true` + renseigner `LocalArchivePath` pour ingérer un ZIP déjà téléchargé (dev/test).

## Démarrage rapide
1. Restaurer et builder :
   ```bash
   dotnet build LotoHelper.sln
   ```
2. Lancer l’orchestrateur Aspire :
   ```bash
   dotnet run --project src/Loto.AppHost/Loto.AppHost.csproj
   ```
   Le dashboard Aspire affiche API, Worker, Postgres et front Angular.
3. Front Angular : démarré par Aspire via NPM app. Sinon, en manuel :
   ```bash
   cd frontend/loto-frontend
   npm install
   npm start
   ```
   (proxy `/api` configuré).

## Ingestion FDJ
- À chaque démarrage du Worker puis toutes les `IntervalMinutes`, l’archive est téléchargée (ou lue localement), parsée, et seuls les tirages absents sont insérés (idempotence via `OfficialDrawId` et index uniques).
- Dates stockées en UTC pour Postgres (`timestamp with time zone`).

## API
- `GET /health` : statut OK.
- `GET /api/draws` : derniers tirages en base (vide si aucune ingestion ou si l’archive n’est pas accessible).

## Base de données
- Postgres provisionné par Aspire.
- Migration auto au démarrage API/Worker (avec retry). En manuel :  
  ```bash
  dotnet ef database update -p src/Loto.Infrastructure/Loto.Infrastructure.csproj -s src/Loto.Api/Loto.Api.csproj
  ```

## Observabilité
- OpenTelemetry (traces/metrics) activé sur API et Worker (OTLP).
- ActivitySource `Loto.Ingestion`, métriques ingestion (`draws_ingested_total`, `ingestion_failures_total`, `ingestion_duration_seconds`).

## Dépannage
- Fichiers verrouillés au build : arrêter l’AppHost/Worker/Api en cours puis relancer `dotnet build`.
- Erreurs d’ingestion : vérifier `NewLotoArchiveUrl` ou utiliser `UseLocalFile=true` avec un ZIP valide ; consulter les logs Aspire pour le Worker.
