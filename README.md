# Notes REST API

ASP.NET Core Web API for user registration, JWT login, and owner-scoped text notes.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQLite is used through Entity Framework Core and does not require a separate database server

## Run locally

This repo includes **dotnet-ef as a local tool** (see [`.config/dotnet-tools.json`](.config/dotnet-tools.json)), so you do not need a global `dotnet ef` install.

```powershell
cd C:\Users\Dan\.cursor\projects\empty-window
dotnet restore NotesApi\NotesApi.csproj
.\scripts\setup-database.cmd
dotnet run --project NotesApi\NotesApi.csproj
```

Use `setup-database.cmd` (not `.ps1`) so you avoid PowerShell execution-policy blocks on unsigned scripts.

Or run the migration manually after restoring tools:

```powershell
dotnet tool restore
dotnet dotnet-ef database update --project NotesApi\NotesApi.csproj
```

Optional global install (if you prefer `dotnet ef` everywhere):

```powershell
dotnet tool install --global dotnet-ef
```

Open Swagger at `http://localhost:5294/swagger` (or the URL shown in the console).

## Web frontend (no Node/npm required)

The UI is served from the API at **`http://localhost:5294`** (static files in `NotesApi/wwwroot/`).

```powershell
cd C:\Users\Dan\.cursor\projects\empty-window
.\run-app.cmd
```

Open **http://localhost:5294** in your browser (register, sign in, manage notes).

## Docker

Build and run the API plus built-in web UI in Docker:

```powershell
cd "C:\Users\Dan\.cursor\projects\Cursor Web Service Application"
.\run-docker.cmd
```

Open **http://localhost:5294**.

The script builds the Docker image, creates the `notes-data` volume if needed, removes any old `notes-api` container, and starts the app with SQLite stored at `/data/notes.db`.

## Kubernetes

The Kubernetes manifests live in [`k8s/`](k8s/). They deploy:

- a single `notes-api` pod
- a `ClusterIP` service
- a persistent volume claim mounted at `/data`
- a secret for the JWT signing key

SQLite is a single-file database, so the deployment intentionally uses **1 replica**.

Build the image and deploy:

```powershell
cd C:\Users\Dan\.cursor\projects\empty-window
.\scripts\docker-build.cmd
.\scripts\k8s-deploy.cmd
```

If you use Docker Desktop Kubernetes, make sure the image exists in Docker Desktop before deploying. For minikube, build inside minikube instead:

```powershell
minikube image build -t notes-api:local .
kubectl apply -k k8s
```

Open the app through port forwarding:

```powershell
kubectl port-forward service/notes-api 5294:80 -n notes
```

Then browse to **http://localhost:5294**.

Before using this outside local development, replace the placeholder value in [`k8s/secret.yaml`](k8s/secret.yaml).

## API endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/register` | No | Create account (`email`, `password`) |
| POST | `/api/auth/login` | No | Returns JWT (`token`, `expiresAt`) |
| GET | `/api/notes` | Bearer | List your notes |
| GET | `/api/notes/{id}` | Bearer | Get one note |
| POST | `/api/notes` | Bearer | Create note (`title?`, `content`) |
| PUT | `/api/notes/{id}` | Bearer | Update note |
| DELETE | `/api/notes/{id}` | Bearer | Delete note |

## Password rules

- Valid email address
- At least 7 characters
- At least one uppercase letter
- At least one number

## Configuration

- Connection string: `NotesApi/appsettings.json` → `ConnectionStrings:DefaultConnection`
- SQLite database file: `NotesApi/notes.db` (created automatically by migrations)
- JWT signing key: override `Jwt:Key` via User Secrets or environment variables in production
