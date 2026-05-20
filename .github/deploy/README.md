# GitHub Actions deployment setup

The CI/CD workflow in `.github/workflows/ci-cd.yml` runs on pushes and pull requests for both `main` and `master`.

## What the workflow does

1. Restores, builds, and tests the ASP.NET Core API.
2. Installs dependencies, prepares Nuxt types, tests, and builds the Nuxt UI.
3. Builds both Docker images to catch container build failures.
4. On pushes to `main` or `master`, deploys the repository to a production host over SSH and runs `docker compose up -d --build --remove-orphans`.

Pull requests run build and test jobs, but do not deploy.

## Production host requirements

The deploy job expects a Linux server that has:

- SSH access for the deploy user.
- Docker Engine installed.
- The Docker Compose plugin available as `docker compose`.
- Network/firewall rules that expose the ports configured in `docker-compose.yml`.

## Required GitHub secrets

Add these under repository **Settings > Secrets and variables > Actions**:

| Secret | Purpose |
|--------|---------|
| `DEPLOY_HOST` | Hostname or IP address of the production server. |
| `DEPLOY_USER` | SSH user on the production server. |
| `DEPLOY_SSH_PRIVATE_KEY` | Private key used to SSH into the server. |
| `DEPLOY_PATH` | Absolute path on the server where the app files should be synced. |
| `DEPLOY_PORT` | Optional SSH port. Defaults to `22` when unset. |
| `TTRPG_JWT_KEY` | Production JWT signing key. Generate one with `openssl rand -base64 48`. |
| `TTRPG_SEED_ADMIN_PASSWORD` | Production seeded admin password. |

The workflow writes `TTRPG_JWT_KEY` and `TTRPG_SEED_ADMIN_PASSWORD` into a remote `.env` file with owner-only permissions before starting Docker Compose.

## Using a different deployment target

If you deploy to Kubernetes, Azure, AWS, Fly.io, Render, or another host, keep the `api`, `ui`, and `docker` jobs and replace only the `deploy` job with provider-specific steps.
