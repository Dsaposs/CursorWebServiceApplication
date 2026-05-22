# GitHub Actions deployment setup

The CI/CD workflow in `.github/workflows/ci-cd.yml` runs on pushes and pull requests for both `main` and `master`.

## What the workflow does

1. Restores, builds, and tests the ASP.NET Core API.
2. Installs dependencies, prepares Nuxt types, tests, and builds the Nuxt UI.
3. Builds both Docker images to catch container build failures.
4. On pushes to `main` or `master`, deploys the repository to a production host over SSH and runs `docker compose up -d --build --remove-orphans` only when production deployment is explicitly enabled.

Pull requests run build and test jobs, but do not deploy. Pushes to `main` or `master` also skip deployment until the `PRODUCTION_DEPLOY_ENABLED` Actions variable is set to `true`.

## Production host requirements

The deploy job expects a Linux server that has:

- SSH access for the deploy user.
- Docker Engine installed.
- The Docker Compose plugin available as `docker compose`.
- Network/firewall rules that expose the ports configured in `docker-compose.yml`.

## Required GitHub secrets

Add these under repository **Settings > Secrets and variables > Actions**. They can be repository secrets or secrets scoped to the `production` environment.

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

## Required GitHub variable

Add this under repository **Settings > Secrets and variables > Actions > Variables**:

| Variable | Value | Purpose |
|----------|-------|---------|
| `PRODUCTION_DEPLOY_ENABLED` | `true` | Enables the production deploy job on pushes to `main` or `master`. Leave unset or set to any other value to run CI/CD validation without deploying. |

This variable prevents unconfigured repositories from failing the whole pipeline because production secrets have not been added yet.

## Remote SSH setup checklist

1. Generate or choose an SSH key pair for the deploy user.
2. Add the public key to `${DEPLOY_USER}`'s `~/.ssh/authorized_keys` on the production host.
3. Add the private key contents, including the `-----BEGIN ...-----` and `-----END ...-----` lines, to the `DEPLOY_SSH_PRIVATE_KEY` GitHub secret.
4. Make sure `${DEPLOY_USER}` can create `${DEPLOY_PATH}` and run `docker compose` from that directory.
5. Confirm the host exposes the ports from `docker-compose.yml`.

## Using a different deployment target

If you deploy to Kubernetes, Azure, AWS, Fly.io, Render, or another host, keep the `api`, `ui`, and `docker` jobs and replace only the `deploy` job with provider-specific steps.
