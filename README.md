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

## Solution structure

```
joblens-chat/
├── JobLens.slnx
├── .cursorrules
├── global.json
├── docs/
│   ├── architecture/
│   ├── demo/
│   ├── evidence/
│   ├── testing/
│   └── README.md
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

When `Mcp:AccessKey` is set (user-secrets locally, app settings on Azure), every request to `/mcp` must send header `X-Api-Key` with that value. Leave it empty only for local demos; Development logs a warning if `/mcp` is open.

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
| `AllowedHosts` | Host allow-list (never `*`) |
| `Ai:Provider` | `Demo`, `AzureOpenAI`, or `OpenAICompatible` |
| `Ai:Endpoint` | Azure / compatible endpoint URL |
| `Ai:Model` | Deployment or model name |
| `Ai:ApiKey` | Secret — set via user-secrets / env only |
| `Ai:DisplayName` | Optional label shown in UI |
| `Mcp:Endpoint` | Chat MCP client URL (default `http://localhost:5080/mcp`) |
| `Mcp:AccessKey` | Secret for `/mcp` (`X-Api-Key`) — user-secrets / Azure only; empty in appsettings |
| `Database:Path` | Optional absolute SQLite path override |
| `Seed:ModeratorPassword` | Override demo moderator password |
| `Seed:StudentPassword` | Override demo student password |
| `Logging:LogLevel:*` | Standard ASP.NET Core logging |

## Demo accounts

Synthetic users are seeded for class demos (hashed passwords; defaults used only when seed keys are unset):

| Email | Role | Default password |
| --- | --- | --- |
| `moderator@joblens.demo` | Moderator | `Moderator#2026` |
| `student1@joblens.demo` … `student4@joblens.demo` | Student | `Student#2026` |

Change passwords for any shared or cloud deployment via `Seed:ModeratorPassword` / `Seed:StudentPassword`.

## Docs

See [docs/README.md](docs/README.md) for the full document index (proposal, test matrix, known answers, demo files, evidence).

## Third-party

Chart.js 4.4.6 (MIT) is stored at `src/JobLens.Web/wwwroot/lib/chartjs/chart.umd.min.js` and loaded only on the Scam Insights Dashboard.
