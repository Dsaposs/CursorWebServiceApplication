import {
  Callout,
  Card,
  CardBody,
  CardHeader,
  Code,
  Divider,
  Grid,
  H1,
  H2,
  H3,
  Pill,
  Row,
  Stack,
  Stat,
  Table,
  Text,
  useCanvasState,
} from "cursor/canvas";

type Section = "all" | "infra" | "security" | "observability" | "scale" | "cicd" | "testing";

export default function EnterpriseProductionReadiness() {
  const [activeSection, setActiveSection] = useCanvasState<Section>("all");

  const sections: { id: Section; label: string }[] = [
    { id: "all", label: "All" },
    { id: "infra", label: "Infrastructure" },
    { id: "security", label: "Security" },
    { id: "observability", label: "Observability" },
    { id: "scale", label: "Scalability" },
    { id: "cicd", label: "CI/CD" },
    { id: "testing", label: "Testing" },
  ];

  const show = (id: Section) => activeSection === "all" || activeSection === id;

  return (
    <Stack gap={24} style={{ padding: 24, maxWidth: 960, margin: "0 auto" }}>
      <Stack gap={4}>
        <H1>Enterprise Production Readiness</H1>
        <Text tone="secondary">TTRPG Session App — ASP.NET Core 8 + Nuxt 4 + SQLite on Kubernetes</Text>
      </Stack>

      <Grid columns={4} gap={12}>
        <Stat value="2" label="Critical Blockers" tone="danger" />
        <Stat value="7" label="High Priority" tone="warning" />
        <Stat value="9" label="Medium Priority" />
        <Stat value="4" label="Quick Wins" tone="success" />
      </Grid>

      <Callout tone="danger" title="Before anything else">
        SQLite is a single-writer file database — it cannot scale horizontally and will corrupt under concurrent writes
        in multi-replica deployments. Migrating to PostgreSQL and adopting EF Core Migrations are prerequisites for
        everything else in this plan.
      </Callout>

      <Row gap={8} wrap>
        {sections.map((s) => (
          <Pill key={s.id} active={activeSection === s.id} onClick={() => setActiveSection(s.id)}>
            {s.label}
          </Pill>
        ))}
      </Row>

      <Divider />

      {/* INFRASTRUCTURE */}
      {show("infra") && (
        <Stack gap={16}>
          <H2>Infrastructure & Database</H2>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="deleted" active size="sm">Critical</Pill>}>
              Replace SQLite with PostgreSQL
            </CardHeader>
            <CardBody>
              <Stack gap={10}>
                <Text>SQLite is unsuitable for production: no connection pooling, single-writer lock, no replication. Every replica would need its own file — impossible in Kubernetes without a single Pod constraint.</Text>
                <H3>What to do</H3>
                <Table
                  headers={["Step", "Detail"]}
                  rows={[
                    ["1. Add EF Core PostgreSQL provider", "Replace Microsoft.EntityFrameworkCore.Sqlite with Npgsql.EntityFrameworkCore.PostgreSQL"],
                    ["2. Connection string config", "Use Npgsql connection string via env var DATABASE_URL or Postgres__ConnectionString"],
                    ["3. Drop EnsureCreated()", "Remove EnsureCreated() and the ApplySchemaUpdatesAsync() ALTER TABLE hacks from Program.cs"],
                    ["4. Generate EF migrations", "Run dotnet ef migrations add InitialCreate — commit migrations to source control"],
                    ["5. Apply on startup", "Call db.Database.MigrateAsync() in Program.cs startup, or run as a separate init container in k8s"],
                    ["6. Update k8s manifests", "Deploy a Postgres StatefulSet (or use a managed service: Azure Database for PostgreSQL, AWS RDS, Cloud SQL)"],
                  ]}
                  striped
                />
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              Add docker-compose for local development
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>The current scripts rely on Docker CLI commands hard-coded in <Code>.cmd</Code> files. A <Code>docker-compose.yml</Code> at the repo root provides a repeatable, one-command local stack with Postgres, the API, and the UI — matching production topology.</Text>
                <Text tone="secondary" size="small">docker compose up --build — replaces start-app.cmd</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              Kubernetes: increase API replicas and add HPA
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>The k8s deployment runs 1 API replica. After moving to Postgres, set <Code>replicas: 3</Code> and add a <Code>HorizontalPodAutoscaler</Code> targeting CPU/memory. Also add pod disruption budgets and pod anti-affinity rules so replicas spread across nodes.</Text>
                <Text>The UI deployment can already scale — verify the <Code>NUXT_API_BASE_URL</Code> points to the API <Code>Service</Code> (cluster-internal DNS), not the host port.</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              Secrets management
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>Currently secrets live in a Kubernetes <Code>secret.yaml</Code> committed to the repo as placeholders. For production use an external secrets store:</Text>
                <Table
                  headers={["Option", "Best for"]}
                  rows={[
                    ["Azure Key Vault + CSI driver", "Azure-hosted clusters (AKS)"],
                    ["AWS Secrets Manager + ASCP", "EKS / AWS-hosted"],
                    ["HashiCorp Vault + Vault Agent Injector", "Multi-cloud or on-prem"],
                    ["Sealed Secrets (Bitnami)", "GitOps-friendly, simpler setup"],
                  ]}
                />
                <Text tone="secondary" size="small">Remove secret.yaml from the repo entirely. Never commit real secrets — rotate the JWT key if it was ever committed.</Text>
              </Stack>
            </CardBody>
          </Card>
        </Stack>
      )}

      {/* SECURITY */}
      {show("security") && (
        <Stack gap={16}>
          {show("infra") && <Divider />}
          <H2>Security</H2>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="deleted" active size="sm">Critical</Pill>}>
              Move tokens out of localStorage
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  The DM JWT (<Code>ttrpg_token</Code>) and all player tokens are stored in <Code>localStorage</Code>.
                  This is vulnerable to XSS — any injected script can steal all sessions silently.
                </Text>
                <H3>Recommended approach</H3>
                <Table
                  headers={["Token type", "Recommended storage", "Why"]}
                  rows={[
                    ["DM access token (JWT)", "httpOnly cookie (SameSite=Strict)", "Inaccessible to JS — immune to XSS"],
                    ["DM refresh token", "httpOnly cookie with /refresh path restriction", "Limits exposure window"],
                    ["Player join tokens", "sessionStorage or httpOnly cookie", "Tab-scoped or cookie-protected"],
                  ]}
                />
                <Text tone="secondary" size="small">The Nuxt server-side proxy already handles forwarding — update it to forward cookies rather than Authorization headers, keeping tokens out of client JS entirely.</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              JWT refresh tokens & revocation
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>JWTs currently expire in 60 minutes with no refresh mechanism. Enterprise requirements:</Text>
                <Table
                  headers={["Feature", "Implementation"]}
                  rows={[
                    ["Refresh tokens", "Issue an opaque refresh token alongside the JWT; store hash in DB"],
                    ["Token revocation", "Maintain a revocation list in Redis (fast) or a DB table"],
                    ["Short-lived access tokens", "Reduce access token TTL to 5–15 min; use refresh flow silently"],
                    ["Logout invalidation", "Revoke refresh token on logout — current logout is client-side only"],
                  ]}
                  striped
                />
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              Rate limiting & brute force protection
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  The API has no rate limiting. Add <Code>Microsoft.AspNetCore.RateLimiting</Code> (built into .NET 7+):
                </Text>
                <Table
                  headers={["Endpoint group", "Policy"]}
                  rows={[
                    ["POST /api/auth/login", "Fixed window: 10 req/min per IP"],
                    ["POST /api/auth/register", "Fixed window: 5 req/min per IP"],
                    ["GET /api/sessions/*/state (polling)", "Sliding window: 60 req/min per token"],
                    ["All other endpoints", "Token bucket: 300 req/min per user"],
                  ]}
                />
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              CORS hardening & Content Security Policy
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>Add HTTP security headers via middleware or the Nuxt server layer:</Text>
                <Table
                  headers={["Header", "Recommended value"]}
                  rows={[
                    ["Content-Security-Policy", "default-src 'self'; script-src 'self'; object-src 'none'"],
                    ["Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload"],
                    ["X-Content-Type-Options", "nosniff"],
                    ["X-Frame-Options", "DENY"],
                    ["Referrer-Policy", "strict-origin-when-cross-origin"],
                  ]}
                  striped
                />
                <Text>CORS: restrict allowed origins to an explicit whitelist — no wildcard in production. Validate <Code>AllowedOrigins</Code> config at startup (already partially done — extend it).</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              Input validation & SQL injection hardening
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>EF Core parameterizes queries, but the raw SQL in <Code>ApplySchemaUpdatesAsync()</Code> uses string interpolation — this disappears once you migrate to EF migrations. Additional steps:</Text>
                <Table
                  headers={["Area", "Action"]}
                  rows={[
                    ["Request DTOs", "Add FluentValidation or DataAnnotations with explicit MaxLength on all string fields"],
                    ["Ruleset JSON blobs", "Validate with the existing RulesetDefinitionValidator; add a size limit"],
                    ["File uploads", "N/A currently — add when needed"],
                    ["OWASP dependency scan", "Run dotnet list package --vulnerable in CI; add npm audit for UI"],
                  ]}
                  striped
                />
              </Stack>
            </CardBody>
          </Card>
        </Stack>
      )}

      {/* OBSERVABILITY */}
      {show("observability") && (
        <Stack gap={16}>
          {(show("infra") || show("security")) && <Divider />}
          <H2>Observability</H2>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              Structured logging with correlation IDs
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  Add <Code>Serilog</Code> (or <Code>OpenTelemetry.Logs</Code>) with a structured JSON sink. Every
                  request should carry a <Code>CorrelationId</Code> header that is logged on every line — this makes
                  distributed tracing across API + UI possible.
                </Text>
                <Table
                  headers={["Package", "Purpose"]}
                  rows={[
                    ["Serilog.AspNetCore", "Request pipeline integration, enrichers"],
                    ["Serilog.Sinks.Console (JSON)", "Structured JSON to stdout for log aggregators"],
                    ["Serilog.Sinks.OpenTelemetry", "Forward to OTLP collector"],
                    ["OpenTelemetry.Instrumentation.AspNetCore", "Automatic span creation per request"],
                  ]}
                />
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              Metrics & distributed tracing
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  .NET 8 has built-in OpenTelemetry support. Instrument the API and export to your observability stack:
                </Text>
                <Table
                  headers={["Stack option", "Components"]}
                  rows={[
                    ["Grafana OSS (self-hosted)", "Prometheus (metrics) + Loki (logs) + Tempo (traces) + Grafana (dashboards)"],
                    ["Azure Monitor", "Application Insights SDK — one NuGet package, auto-instruments everything"],
                    ["Datadog", "OpenTelemetry exporter → Datadog agent — best for large orgs already on Datadog"],
                  ]}
                />
                <Text>Add custom metrics: active sessions, action queue depth, EF query duration, polling request rate per session. These map directly to your domain and will surface problems before users notice.</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              Health checks & alerting
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  A <Code>/health</Code> endpoint exists. Extend it with component checks using{" "}
                  <Code>Microsoft.Extensions.Diagnostics.HealthChecks</Code>:
                </Text>
                <Table
                  headers={["Check", "Package"]}
                  rows={[
                    ["Database connectivity", "AspNetCore.HealthChecks.NpgsqlHealthCheck"],
                    ["Redis (when added)", "AspNetCore.HealthChecks.Redis"],
                    ["/health/ready vs /health/live", "Separate readiness (DB up) from liveness (process alive) for k8s probes"],
                  ]}
                  striped
                />
                <Text>Wire readiness/liveness endpoints in the k8s deployment manifests — replace the current generic probes.</Text>
              </Stack>
            </CardBody>
          </Card>
        </Stack>
      )}

      {/* SCALABILITY */}
      {show("scale") && (
        <Stack gap={16}>
          {(show("infra") || show("security") || show("observability")) && <Divider />}
          <H2>Scalability</H2>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              Replace polling with WebSockets / SignalR
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  The current live-session experience uses HTTP polling (<Code>useSessionPolling</Code>) — every
                  connected player issues repeated GET requests to <Code>/api/sessions/*/state</Code>. At scale (many
                  concurrent sessions), this creates significant DB read load.
                </Text>
                <Table
                  headers={["Option", "Effort", "Best for"]}
                  rows={[
                    ["ASP.NET Core SignalR", "Medium", "Real-time push, built-in .NET, works behind reverse proxies"],
                    ["Server-Sent Events (SSE)", "Low", "One-way push from server; simpler than WebSockets"],
                    ["WebSockets (raw)", "High", "Full-duplex; only needed if players send high-frequency events"],
                  ]}
                />
                <Text tone="secondary" size="small">SignalR is the pragmatic choice — it falls back to long-polling automatically and has a Nuxt/Vue client library. Scale-out requires adding a Redis backplane so messages broadcast across all API replicas.</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              Add Redis for distributed caching & SignalR backplane
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>Once the API runs as multiple replicas, in-memory state is per-process. Redis provides:</Text>
                <Table
                  headers={["Use case", "Package"]}
                  rows={[
                    ["Session/game state cache", "Microsoft.Extensions.Caching.StackExchangeRedis"],
                    ["SignalR scale-out backplane", "Microsoft.AspNetCore.SignalR.StackExchangeRedis"],
                    ["JWT revocation list", "StackExchange.Redis — store token JTI with TTL = token expiry"],
                    ["Rate limiter store", "AspNetCore.RateLimiting + Redis for distributed windows"],
                  ]}
                  striped
                />
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              API versioning
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  Add <Code>Asp.Versioning.Http</Code> and prefix all routes with <Code>/api/v1/</Code>. This allows
                  breaking changes to the API without forcing all clients to update simultaneously — essential once
                  external consumers or mobile clients exist.
                </Text>
                <Text tone="secondary" size="small">Deprecation headers (Sunset, Deprecation) communicate end-of-life dates to API consumers automatically.</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              CDN & static asset optimization
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  The Nuxt app currently serves everything from the Node runtime. For production, put a CDN in front of
                  the UI:
                </Text>
                <Table
                  headers={["Asset type", "Strategy"]}
                  rows={[
                    ["JS/CSS bundles", "CDN with long max-age + content hash filenames (Nuxt does this automatically)"],
                    ["Nuxt SSR pages", "CDN edge caching with short TTL for public pages; bypass cache for authenticated routes"],
                    ["API responses", "Cache GET /api/rulesets in CDN — rulesets are mostly static"],
                  ]}
                  striped
                />
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              Database performance
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>After migrating to Postgres, add indexes and connection pooling:</Text>
                <Table
                  headers={["Item", "Detail"]}
                  rows={[
                    ["Connection pooling", "Use Npgsql's built-in pooler; set Min/MaxPoolSize in connection string"],
                    ["Read replicas", "Route GET queries to a read replica using EF Core query splitting"],
                    ["Indexes", "Add indexes on GameSession.JoinCode, GameParticipant.JoinToken, ActionRequest.SessionId+Status"],
                    ["Pagination", "All list endpoints should implement cursor-based or offset pagination; none currently do"],
                    ["N+1 queries", "Audit LINQ queries with EF Core logging; add .Include() or .AsSplitQuery() where needed"],
                  ]}
                  striped
                />
              </Stack>
            </CardBody>
          </Card>
        </Stack>
      )}

      {/* CI/CD */}
      {show("cicd") && (
        <Stack gap={16}>
          {(show("infra") || show("security") || show("observability") || show("scale")) && <Divider />}
          <H2>CI/CD Pipeline</H2>

          <Callout tone="info" title="Zero CI/CD currently">
            There are no GitHub Actions, Azure Pipelines, or any other pipeline configs in the repo. This is the highest
            operational risk for a team: manual deployments are error-prone and block scaling the team.
          </Callout>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="warning" active size="sm">High</Pill>}>
              GitHub Actions pipeline (recommended starting point)
            </CardHeader>
            <CardBody>
              <Stack gap={16}>
                <Text>Create <Code>.github/workflows/ci.yml</Code> with these jobs running on every PR and push to main:</Text>
                <Table
                  headers={["Job", "Steps"]}
                  rows={[
                    ["build-api", "dotnet restore → dotnet build --no-restore → dotnet test --no-build"],
                    ["build-ui", "npm ci → npm run build → npm test"],
                    ["docker-build", "Build both Docker images; push to registry on merge to main"],
                    ["security-scan", "dotnet list package --vulnerable; npm audit; Trivy image scan"],
                    ["deploy-staging", "kubectl apply -k k8s/ after tests pass (manual approval gate for prod)"],
                  ]}
                  striped
                />
                <H3>Recommended deployment strategy</H3>
                <Table
                  headers={["Environment", "Trigger", "Approval"]}
                  rows={[
                    ["Dev/Preview", "Every PR — ephemeral namespace in k8s", "Automatic"],
                    ["Staging", "Merge to main", "Automatic"],
                    ["Production", "Tag v* or manual workflow dispatch", "Manual approval required"],
                  ]}
                />
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              Container registry & image tagging
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>Use GitHub Container Registry (GHCR) or a managed registry (ACR, ECR, Artifact Registry). Tag images with:</Text>
                <Table
                  headers={["Tag", "Purpose"]}
                  rows={[
                    ["sha-{git-sha}", "Immutable — exact commit deployed; use in k8s manifests"],
                    ["latest", "Floating — convenience only, never deploy latest to production"],
                    ["v1.2.3", "Semantic version on release tags"],
                  ]}
                />
                <Text tone="secondary" size="small">Use Kustomize image transformers to pin the deployed SHA in the k8s manifests — this gives you a full audit trail of what is running where.</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              GitOps with ArgoCD or Flux
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  The <Code>k8s/</Code> directory is already structured with Kustomize. Add ArgoCD or Flux to
                  continuously reconcile cluster state from Git — deployments become Git commits, rollbacks become git
                  reverts, and you have a complete audit trail without manual kubectl commands.
                </Text>
              </Stack>
            </CardBody>
          </Card>
        </Stack>
      )}

      {/* TESTING */}
      {show("testing") && (
        <Stack gap={16}>
          {(show("infra") || show("security") || show("observability") || show("scale") || show("cicd")) && <Divider />}
          <H2>Testing</H2>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              API integration tests
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  The existing test project (<Code>TtrpgDomainTests.cs</Code>) covers domain rules. Add integration
                  tests using <Code>Microsoft.AspNetCore.Mvc.Testing</Code> + <Code>Respawn</Code> for DB reset:
                </Text>
                <Table
                  headers={["Test type", "Coverage goal"]}
                  rows={[
                    ["Auth flow", "Register → login → JWT claims → protected endpoint returns 200"],
                    ["Game lifecycle", "Create game → invite → join → start session → submit action → resolve"],
                    ["Combat flow", "Set initiative → advance turn → NPC visibility rules"],
                    ["Authorization rules", "Each role/token type can only access permitted endpoints"],
                  ]}
                  striped
                />
                <Text tone="secondary" size="small">Use Testcontainers.PostgreSQL to spin up a real Postgres instance per test run — avoids SQLite-specific behavior divergence.</Text>
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Medium</Pill>}>
              UI end-to-end tests
            </CardHeader>
            <CardBody>
              <Stack gap={8}>
                <Text>
                  Add <Code>Playwright</Code> (or Cypress) for critical user flows. The most valuable E2E scenarios:
                </Text>
                <Table
                  headers={["Flow", "Priority"]}
                  rows={[
                    ["DM: login → create game → create session → start combat", "P0"],
                    ["Player: join via invite link → view character → submit action", "P0"],
                    ["DM: resolve action → advance turn → end session", "P1"],
                    ["Admin: view user report", "P2"],
                  ]}
                />
              </Stack>
            </CardBody>
          </Card>

          <Card collapsible defaultOpen>
            <CardHeader trailing={<Pill tone="neutral" active size="sm">Quick Win</Pill>}>
              Code coverage thresholds in CI
            </CardHeader>
            <CardBody>
              <Text>
                Add <Code>coverlet</Code> to the test project and enforce a minimum line coverage threshold (start at
                60%, raise over time) in the CI pipeline. This prevents coverage regressions silently sneaking in with
                new features.
              </Text>
            </CardBody>
          </Card>
        </Stack>
      )}

      <Divider />

      {/* ROADMAP */}
      {show("all") && (
        <Stack gap={16}>
          <H2>Recommended Execution Order</H2>
          <Table
            headers={["Phase", "Focus", "Key deliverables"]}
            rows={[
              ["1 — Foundation (week 1–2)", "Database & secrets", "Postgres migration, EF migrations, external secrets store, docker-compose"],
              ["2 — Security (week 3–4)", "Harden auth & API", "httpOnly cookies, refresh tokens, rate limiting, security headers"],
              ["3 — Observability (week 5–6)", "See what's happening", "Structured logs, OpenTelemetry traces, Grafana dashboards, health checks"],
              ["4 — CI/CD (week 7–8)", "Automate delivery", "GitHub Actions, registry, staging auto-deploy, production gate"],
              ["5 — Scale (week 9–12)", "Handle load", "SignalR + Redis backplane, HPA, CDN, API versioning, DB indexes"],
              ["6 — Quality (ongoing)", "Reduce risk", "Integration tests, E2E with Playwright, coverage thresholds, load tests"],
            ]}
            striped
            rowTone={["danger", "warning", "info", "neutral", "neutral", "success"]}
          />
        </Stack>
      )}
    </Stack>
  );
}
