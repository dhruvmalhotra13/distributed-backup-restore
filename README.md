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

## Getting started — run it locally (Docker only)

Everything (database, message queue, cache, API, worker, and web UI) runs in
containers. You do **not** need .NET, Node, PostgreSQL, etc. installed on your
machine — only Docker and Git.

### Prerequisites

| Requirement | Notes |
| ----------- | ----- |
| **Docker Desktop** | Installed and running. On **Windows** this needs **WSL2** (Docker Desktop enables it; a one-time reboot may be required the first time). |
| **Git** | To clone the repository. |
| Free ports | `3000` (UI), `8080` (API), `15672` (RabbitMQ UI). |

### Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/dhruvmalhotra13/distributed-backup-restore.git
   cd distributed-backup-restore
   ```
2. **Create your `.env`** from the template and set `HOST_HOME` to your user folder:
   ```bash
   cp .env.example .env
   ```
   Then edit `.env`:
   - Windows: `HOST_HOME=C:/Users/yourname`
   - macOS: `HOST_HOME=/Users/yourname`
   - Linux: `HOST_HOME=/home/yourname`

   Your whole user folder is mounted once into the containers at `/host`, so you
   can back up or restore **any folder under it just by typing its real path**
   (e.g. `C:\Users\yourname\Desktop\MyProject`) — no per-folder config edits.
3. **Start everything**
   ```bash
   docker compose up --build
   ```
   This launches PostgreSQL, Redis, RabbitMQ, the API, a worker, and the
   dashboard. Database migrations are applied automatically. The first run
   downloads images and builds the apps, so it takes a few minutes.
4. **Open the app**
   - **Dashboard:** http://localhost:3000 — create backups/restores and watch live progress.
   - **API docs (Swagger):** http://localhost:8080/swagger
   - **RabbitMQ UI:** http://localhost:15672 (guest / guest)
5. **Stop when done**
   ```bash
   docker compose down          # add -v to also wipe the database volume
   ```

All backups are stored in a single fixed vault at `./data/BackupVault` inside
the repo folder.

### Run a backup

`POST /backup-jobs`
```json
{ "sourcePath": "C:/Users/you/Desktop/MyProject", "backupName": "FirstBackup" }
```
The response includes a `backupId` (e.g. `backup-a1b2c3d4`) and a job `id`.
Watch progress via `GET /jobs/{id}/progress` or the SignalR hub at `/hubs/progress`.

### Run a restore

`POST /restore-jobs`
```json
{ "backupId": "backup-a1b2c3d4", "restorePath": "C:/Users/you/Desktop/Restored" }
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
2. Create a backup of a real folder (e.g. `C:\Users\you\Desktop\MyProject`) and watch progress climb by **bytes**.
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

## Troubleshooting

| Symptom | Fix |
| ------- | --- |
| `docker: command not found` or "Cannot connect to the Docker daemon" | Docker Desktop isn't running. Start it and wait until it says **Engine running**. |
| On Windows: *"WSL2 is not installed / virtualization not enabled"* | Run `wsl --install`, reboot, and make sure virtualization is enabled in your BIOS. Docker Desktop needs the WSL2 backend. |
| Port already in use (`3000`, `8080`, `15672`) | Stop whatever is using the port, or change the mapping in `.env` (`FRONTEND_PORT`, `API_PORT`). |
| Dashboard loads but shows "Offline / can't reach API" | The API container may still be starting (it waits for the database). Give it a few seconds, or check `docker compose logs api`. |
| "Source path not found" when creating a backup | The folder must be **inside your `HOST_HOME`**. Double-check the path and that `HOST_HOME` in `.env` points at your user folder. Restart with `docker compose up -d` after editing `.env`. |
| A backup/restore job is stuck in `Queued` | The worker may not have connected to RabbitMQ. Check `docker compose logs worker`; `docker compose restart worker` if needed. |
| OneDrive files won't back up | Cloud-only files aren't on disk. In OneDrive, choose **"Always keep on this device"** for the folder first. |
| Want a totally clean slate | `docker compose down -v` removes containers **and** the database volume. |

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
