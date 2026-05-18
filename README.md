# Notes App

Two-service notes application with a Nuxt/Vue frontend and an ASP.NET Core Web API backend for user registration, JWT login, and owner-scoped text notes.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) for local Nuxt development
- Docker Desktop for containerized local runs
- SQLite is used through Entity Framework Core and does not require a separate database server

## Run locally

SQLite is created automatically when the application starts.

Start the API:

```powershell
cd "C:\Users\Dan\.cursor\projects\Cursor Web Service Application"
dotnet restore NotesApi\NotesApi.csproj
.\scripts\setup-database.cmd
dotnet run --project NotesApi\NotesApi.csproj
```

In another terminal, start the Nuxt UI:

```powershell
cd "C:\Users\Dan\.cursor\projects\Cursor Web Service Application\frontend"
npm install
$env:NUXT_API_BASE_URL = "http://localhost:5294"
npm run dev
```

Open the UI at `http://localhost:3000`. Open Swagger at `http://localhost:5294/swagger` when the API is running in development.

## Start The App

The UI and API run as separate Docker containers:

- UI: `http://localhost:3000`
- API: `http://localhost:5294`

Use `start-app.cmd` when you want the script to build and start the local Docker app only if no instance is already present:

```powershell
cd "C:\Users\Dan\.cursor\projects\Cursor Web Service Application"
.\scripts\start-app.cmd
```

Open **http://localhost:3000** in your browser to register, sign in, and manage notes. The Nuxt service proxies `/api/*` requests to the backend container.

If an app instance is already running on port `3000` or `5294`, or a `notes-api`/`notes-ui` Docker/Kubernetes instance already exists, the script exits with an error instead of replacing it.

## Stop The App

Use `stop-app.cmd` to stop local app processes, remove the Docker containers, and remove the Kubernetes app deployments/services while preserving SQLite data:

```powershell
.\scripts\stop-app.cmd
```

## Docker Details

### Requirements

- Docker Desktop installed
- Docker Desktop running before you start the script

### What `start-app.cmd` Does

- Builds the Docker images `notes-api:local` and `notes-ui:local`
- Creates the Docker network `notes-network` if it does not already exist
- Creates the Docker volume `notes-data` if it does not already exist
- Starts new detached `notes-api` and `notes-ui` containers
- Maps your browser port `3000` to the Nuxt UI port `3000`
- Maps your browser port `5294` to the API port `8080`
- Stores SQLite data in the Docker volume at `/data/notes.db`
- Configures Nuxt with `NUXT_API_BASE_URL=http://notes-api:8080` so server-side UI requests reach the API over the Docker network

## Kubernetes

The Kubernetes manifests live in [`k8s/`](k8s/). They deploy:

- a single `notes-api` pod and `ClusterIP` service
- a single `notes-ui` pod and `ClusterIP` service
- a persistent volume claim mounted at `/data`
- a secret for the JWT signing key

SQLite is a single-file database, so the deployment intentionally uses **1 replica**.

Build both images and deploy manually if you want to run through Kubernetes:

```powershell
cd "C:\Users\Dan\.cursor\projects\Cursor Web Service Application"
docker build -t notes-api:local .
docker build -t notes-ui:local frontend
kubectl apply -k k8s
kubectl rollout status deployment/notes-api -n notes
kubectl rollout status deployment/notes-ui -n notes
```

If you use Docker Desktop Kubernetes, make sure both images exist in Docker Desktop before deploying. For minikube, build inside minikube instead:

```powershell
minikube image build -t notes-api:local .
minikube image build -t notes-ui:local frontend
kubectl apply -k k8s
```

Open the UI through port forwarding:

```powershell
kubectl port-forward service/notes-ui 3000:80 -n notes
```

Then browse to **http://localhost:3000**. The Nuxt pod sends API requests to `http://notes-api` inside the `notes` namespace.

If you want direct access to Swagger or the API service, run a second port-forward:

```powershell
kubectl port-forward service/notes-api 5294:80 -n notes
```

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
