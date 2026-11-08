# JobLens Chat

JobLens Chat is a .NET 10 ASP.NET Core MVC app for **ISM 6225 · App Dev for Analytics**. It helps university career-center staff and students explore **synthetic** job and internship scam reports through CRUD screens, risk checks, and a data-grounded chatbot that answers only from read-only MCP tools.

Team: Naga Pavan Sathvik Reddy Sirigiri · Isabella Cuello Penaloza · Lakshmi Reethika Guduri

## Features

- Seeded SQLite database (companies, scam reports, recruiters, job postings, interview reviews, check logs, demo users)
- CRUD for companies, scam reports, recruiters, job postings, and interview reviews
- Check Recruiter (deterministic risk score + live RDAP / EDGAR signals)
- Offer Analyzer (paste or PDF/DOCX upload, PII redaction, AI + rule fallback)
- Smart Search (plain-English → structured job filters)
- Live Scam Insights Dashboard (Chart.js)
- Chat grounded via MCP tools on `/mcp` (no free-form SQL)
- **Jobs** page with two tabs: live openings from [USAJOBS](https://www.usajobs.gov) by default (or [The Muse](https://www.themuse.com) via `Jobs:Provider`) fetched on demand with JobLens trust signals, and JobLens-curated postings with Smart Search

## Solution structure

```
joblens-chat/
├── JobLens.slnx
├── .cursorrules
├── global.json
├── docs/
│   ├── DOCUMENTATION.md
│   ├── demo/
│   ├── evidence/
│   └── testing/
├── src/
│   ├── JobLens.Domain/           entities + constants
│   ├── JobLens.Application/      interfaces, DTOs, services, AddApplication()
│   ├── JobLens.Infrastructure/   SQLite, seed, HTTP clients, AI, documents, AddInfrastructure()
│   └── JobLens.Web/              MVC, Views, wwwroot, Mcp/, ChatOrchestrator, Program.cs
└── tests/
    └── JobLens.Tests/
```

Dependency rule: **Web → Application ← Infrastructure; Application → Domain.**

## How to run

```powershell
dotnet run --project src/JobLens.Web --launch-profile http
```

- Home: http://localhost:5080/
- Chat: http://localhost:5080/Chat
- MCP: http://localhost:5080/mcp
- HTTPS profile: https://localhost:7080 (HTTP still on 5080)

SDK band is pinned in `global.json` (10.0.401).

AI keys and seed password overrides use **user-secrets** (or Azure app settings), never `appsettings.json`.

```powershell
dotnet user-secrets set "Ai:ApiKey" "<your-key>" --project src/JobLens.Web
dotnet user-secrets set "Mcp:AccessKey" "<your-local-mcp-key>" --project src/JobLens.Web
```

### MCP access key (`X-Api-Key`)

When `Mcp:AccessKey` is set (user-secrets locally, app settings on Azure), every request to `/mcp` must send header `X-Api-Key` with that value. Leave it empty only for local Development demos (logs a warning). Outside Development, a missing key fails closed: `/mcp` returns 503 and startup logs an error.

**Cursor:** copy `.cursor/mcp.example.json` to `.cursor/mcp.json` (gitignored) and set the same key under `headers.X-Api-Key`:

```json
{
  "mcpServers": {
    "joblens-local": {
      "url": "http://localhost:5080/mcp",
      "headers": {
        "X-Api-Key": "<your-local-mcp-key>"
      }
    }
  }
}
```

**VS Code (GitHub Copilot MCP / `mcp.json`):** same shape — `url` plus a `headers` object with `X-Api-Key`.

**MCP Inspector:** start against `http://localhost:5080/mcp` and add a custom header `X-Api-Key` with your secret (Inspector UI: Headers / Custom Headers, depending on version).

## How to test

```powershell
dotnet test
```

## Configuration keys

| Key | Purpose |
| --- | --- |
| `AllowedHosts` | Host allow-list (never `*`; set the exact Azure hostname in production) |
| `Ai:Provider` | `Demo`, `AzureOpenAI`, or `OpenAICompatible` |
| `Ai:Endpoint` | Azure / compatible endpoint URL |
| `Ai:Model` | Deployment or model name |
| `Ai:ApiKey` | Secret — set via user-secrets / Azure App Settings only |
| `Ai:DisplayName` | Optional label shown in UI |
| `Mcp:Endpoint` | Chat MCP client URL (default `http://localhost:5080/mcp`) |
| `Mcp:AccessKey` | Secret for `/mcp` (`X-Api-Key`) — required outside Development |
| `Jobs:Provider` | `UsaJobs` (default) or `TheMuse` |
| `Jobs:UsaJobs:ApiKey` | USAJOBS API key — user-secrets / Azure only |
| `Jobs:UsaJobs:UserAgent` | Email registered with USAJOBS API — user-secrets / Azure only |
| `Jobs:TheMuse:ApiKey` | Optional The Muse API key when `Jobs:Provider=TheMuse` |
| `Database:Path` | Optional absolute SQLite path override |
| `Seed:ModeratorPassword` | Demo moderator password — required outside Development (`Seed__ModeratorPassword`) |
| `Seed:StudentPassword` | Demo student password — required outside Development (`Seed__StudentPassword`) |
| `Logging:LogLevel:*` | Standard ASP.NET Core logging |

### Azure App Settings (production)

Set these as application settings (double-underscore env form works too):

| Setting | Notes |
| --- | --- |
| `AllowedHosts` | Exact App Service hostname |
| `ASPNETCORE_ENVIRONMENT` | `Production` (or non-Development) |
| `Ai:Provider` / `Ai:Endpoint` / `Ai:Model` / `Ai:ApiKey` | Only if AI mode is enabled |
| `Mcp:Endpoint` | In-app MCP URL the chat client calls |
| `Mcp:AccessKey` | Required; `/mcp` returns 503 if missing |
| `Jobs__Provider` | `UsaJobs` (default) or `TheMuse` |
| `Jobs__UsaJobs__ApiKey` | USAJOBS API key |
| `Jobs__UsaJobs__UserAgent` | Email registered with USAJOBS |
| `Jobs__TheMuse__ApiKey` | Optional; when using The Muse |
| `Seed:ModeratorPassword` | Required; startup throws if missing |
| `Seed:StudentPassword` | Required; startup throws if missing |
| `Database:Path` | Optional; otherwise `HOME/data/JobLens/joblens.db` |

Forwarded headers (`X-Forwarded-For` / `X-Forwarded-Proto`) are enabled for App Service. Auth cookies use `SecurePolicy=Always` outside Development. Expensive POSTs (Chat, Offer Analyzer, Check Recruiter) and Jobs GET (`/Jobs`) are limited to 10 requests/minute per IP.

### Jobs (USAJOBS / The Muse + JobLens postings)

- Page: `/Jobs` — two tabs (`?tab=live` default, `?tab=joblens`). Legacy `/LiveJobs` and `/JobPostings` Index redirect into these tabs; JobPostings CRUD routes stay unchanged.
- **Live openings (default USAJOBS):** filters for keyword, location, hiring path (students/interns by default, recent graduates), remote-only, and OPM series codes against `https://data.usajobs.gov/api/search` (1-based pages, 30-minute memory cache, never written to SQLite). Trust card shows **Official .gov source** tips; apply links must be on `usajobs.gov`.
- Switch provider with `Jobs:Provider` = `UsaJobs` | `TheMuse` (Muse keeps category/level/company filters).
- **JobLens postings:** curated synthetic listings with Smart Search, filters, and moderator CRUD.
- MCP tool: `search_live_jobs` (live external; scam facts still come only from database tools).

#### USAJOBS local setup

```powershell
dotnet user-secrets set "Jobs:UsaJobs:ApiKey" "<your-usajobs-api-key>" --project src/JobLens.Web
dotnet user-secrets set "Jobs:UsaJobs:UserAgent" "<your-registered-email>" --project src/JobLens.Web
dotnet user-secrets set "Jobs:Provider" "UsaJobs" --project src/JobLens.Web
```

Azure App Settings: `Jobs__Provider`, `Jobs__UsaJobs__ApiKey`, `Jobs__UsaJobs__UserAgent`. Never put keys in `appsettings.json` or source control.

## Demo accounts

Synthetic users are seeded for class demos (hashed passwords). Documented defaults apply **only in Development** when seed keys are unset:

| Email | Role | Default password (Development only) |
| --- | --- | --- |
| `moderator@joblens.demo` | Moderator | `Moderator#2026` |
| `student1@joblens.demo` … `student4@joblens.demo` | Student | `Student#2026` |

On Azure / any non-Development host, set `Seed__ModeratorPassword` and `Seed__StudentPassword` in App Settings or startup fails.

## Documentation

See [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md) for the full document index (proposal, test matrix, known answers, demo files, evidence).

## Third-party

Chart.js 4.4.6 (MIT) is stored at `src/JobLens.Web/wwwroot/lib/chartjs/chart.umd.min.js` and loaded only on the Scam Insights Dashboard.

Live job listings are provided by [USAJOBS](https://www.usajobs.gov) by default (or The Muse when `Jobs:Provider=TheMuse`) and are not stored by JobLens.
