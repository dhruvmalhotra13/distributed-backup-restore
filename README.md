# Distributed Backup & Restore Platform

A local-first, distributed backup and restore platform — a "mini Azure Backup"
built entirely from **free, open-source components**. It performs real file
backup and restore on your machine while demonstrating production-style
distributed-systems concepts: asynchronous job orchestration, chunk-based
transfer, real-time progress, checkpointing, worker-crash recovery, retries,
and hash-based integrity validation.

> Anyone can clone this repository and run the whole system locally with a
> single command — only Docker is required.

---

## Architecture

```
 React/Next.js dashboard        ASP.NET Core API            .NET Worker(s)
 ┌─────────────────┐  HTTP   ┌──────────────────┐  AMQP  ┌──────────────────┐
 │ Dashboard       │ ──────▶ │ Job controllers  │ ─────▶ │ Backup DataMover │
 │ live progress   │ ◀─────  │ SignalR hub      │        │ Restore DataMover│
 └─────────────────┘  WS     └───────┬──────────┘        └────────┬─────────┘
                                     │                            │
                          ┌──────────┴─────────┐        ┌─────────┴─────────┐
                          │ RabbitMQ (commands)│        │ Backup Vault      │
                          │ Redis (progress +  │        │ (local folder)    │
                          │ control signals)   │        └─────────┬─────────┘
                          └──────────┬─────────┘                  │
                                     │                            │
                              ┌──────┴──────────────┐             │
                              │ PostgreSQL           │ ◀───────────┘
                              │ jobs, files, chunks, │
                              │ checkpoints, events  │
                              └──────────────────────┘
```

- **API** creates jobs, exposes control endpoints, and relays progress to the UI via SignalR.
- **Worker** consumes commands from RabbitMQ and streams bytes to/from the Backup Vault.
- **PostgreSQL** is the source of truth for jobs, files, chunks, checkpoints, and events.
- **Redis** carries live progress (pub/sub) and cooperative control signals (pause/cancel).
- **Backup Vault** is a local folder holding chunks + `manifest.json`, `metadata.json`, and hashes.

## Tech stack (all free / open-source)

| Layer            | Technology                          |
| ---------------- | ----------------------------------- |
| Frontend         | Next.js 16 (React 19) + Tailwind    |
| API              | ASP.NET Core 8 (Web API + SignalR)  |
| Worker           | .NET 8 Worker Service + MassTransit |
| Messaging        | RabbitMQ                            |
| Database         | PostgreSQL + EF Core                |
| Progress / PubSub| Redis (StackExchange.Redis)         |
| Storage          | Local file system (Backup Vault)    |
| Packaging        | Docker Compose                      |

---

## Quick start (Docker — the only prerequisite)

1. **Install Docker Desktop** and make sure it is running.
2. Copy the environment template and adjust paths if you like:
   ```bash
   cp .env.example .env
   ```
3. Start everything:
   ```bash
   docker compose up --build
   ```
   This launches PostgreSQL, Redis, RabbitMQ, the API, a worker, and the web
   dashboard. Database migrations are applied automatically on startup.
4. Open the **dashboard at http://localhost:3000** to create and monitor jobs
   with live progress.
   The API docs are at **http://localhost:8080/swagger** and the RabbitMQ
   management UI at **http://localhost:15672** (guest/guest).

By default the repository's `sample-data/` folder is mounted into the containers
at `/data/source`, and the vault is written to `./data/BackupVault` on your host.

### Run a backup

`POST /backup-jobs`
```json
{ "sourcePath": "/data/source", "backupName": "FirstBackup" }
```
The response includes a `backupId` (e.g. `backup-a1b2c3d4`) and a job `id`.
Watch progress via `GET /jobs/{id}/progress` or the SignalR hub at `/hubs/progress`.

### Run a restore

`POST /restore-jobs`
```json
{ "backupId": "backup-a1b2c3d4", "restorePath": "/data/source/_restored" }
```
The restore worker reconstructs files and validates each one's SHA-256 hash
against the original.

---

## API surface

| Method & path                     | Purpose                                  |
| --------------------------------- | ---------------------------------------- |
| `POST /backup-jobs`               | Create a backup job                      |
| `GET  /backup-jobs`               | List backup jobs (`?status=` filter)     |
| `GET  /backup-jobs/{id}`          | Backup job details                       |
| `POST /backup-jobs/{id}/pause`    | Pause a running backup                   |
| `POST /backup-jobs/{id}/resume`   | Resume from last checkpoint              |
| `POST /backup-jobs/{id}/cancel`   | Cancel a backup                          |
| `POST /backup-jobs/{id}/retry`    | Retry a failed/cancelled backup          |
| `POST /restore-jobs`              | Create a restore job                     |
| `GET  /restore-jobs/{id}`         | Restore job details / progress           |
| `GET  /jobs/{id}/events`          | Job event timeline                       |
| `GET  /jobs/{id}/progress`        | Latest cached progress snapshot          |

---

## Frontend (run in development)

The dashboard lives in [`frontend/`](frontend) and is a Next.js app. It is built
and served automatically by `docker compose`, but you can also run it standalone
against a locally-running API:

```bash
cd frontend
cp .env.local.example .env.local   # NEXT_PUBLIC_API_BASE=http://localhost:8080
npm install
npm run dev                        # http://localhost:3000
```

It talks to the API over REST and subscribes to the SignalR hub at
`/hubs/progress` for real-time job progress.

---

## Interview demo script

1. `docker compose up --build` — all services become healthy.
2. Create a backup of `/data/source` and watch progress climb by **bytes**.
3. Kill the worker mid-backup to prove crash recovery:
   ```bash
   docker compose restart worker
   ```
   The job **resumes from its last checkpoint**, not from 0%.
4. Move or delete the source, then restore into a new folder and show the files
   reconstructed and **hash-validated**.
5. Inspect `GET /jobs/{id}/events` for a production-style timeline, and the
   per-backup `logs/worker.log` inside the vault.
6. Scale workers to show throughput/parallelism:
   ```bash
   docker compose up --scale worker=3
   ```

---

## Local development (without Docker for the app)

You still need PostgreSQL, Redis, and RabbitMQ — the easiest way is to run just
those via Docker and run the API/Worker from the SDK:

```bash
docker compose up postgres redis rabbitmq
dotnet run --project src/BackupRestore.Api
dotnet run --project src/BackupRestore.Worker
```

Update the `ConnectionStrings`/`RabbitMq` values in each project's
`appsettings.json` (they default to `localhost`).

### Build & test

```bash
dotnet build
dotnet test
```

### Database migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> -p src/BackupRestore.Infrastructure -s src/BackupRestore.Infrastructure
```

---

## Project structure

```
distributed-backup-restore/
├── docker-compose.yml            # one-command local stack
├── Dockerfile.api / Dockerfile.worker
├── .env.example                  # configurable host paths & credentials
├── sample-data/                  # ships so backup works immediately
├── src/
│   ├── BackupRestore.Core/           # entities, enums, message contracts, ports
│   ├── BackupRestore.Infrastructure/ # EF Core, RabbitMQ, Redis, vault storage
│   ├── BackupRestore.Api/            # controllers, SignalR hub, progress relay
│   └── BackupRestore.Worker/         # backup & restore DataMovers (consumers)
└── tests/
    └── BackupRestore.Tests/          # unit tests
```

## How key requirements are met

- **Byte-based progress**: `ProgressCalculator.Percent(copied, total)`, kept **monotonic**.
- **Chunking / streaming**: files are read and written chunk-by-chunk (default 4 MB); files are never loaded fully into memory.
- **Checkpointing & resume**: a `Checkpoint` row records the last completed chunk per file; resume/retry re-queues the job and skips completed chunks.
- **Crash recovery**: MassTransit redelivers unacked messages; the worker resumes from the checkpoint.
- **Integrity**: per-chunk and whole-file SHA-256 hashes; restore recomputes and compares.
- **Observability**: `JobEvent` timeline, structured logs, and per-backup `worker.log`.

## Roadmap

- Next.js dashboard (live progress bars, logs, controls).
- Stretch features from the design: deduplication, compression, at-rest encryption, scheduled/versioned backups, and pluggable storage adapters (S3/Azure Blob).

## License

MIT — see [LICENSE](LICENSE).
