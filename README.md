# joblens-chat

JobLens Chat is a .NET 10 ASP.NET Core MVC app for ISM 6225 · App Dev for Analytics · Fall 2026. Career-center staff and students will ask questions about synthetic job-scam reports. The chatbot will answer only from read-only MCP tools.

This phase is the project skeleton. Home and Chat are placeholder pages. There is no database and no MCP server yet.

Team: Naga Pavan Sathvik Reddy Sirigiri · Isabella Cuello Penaloza · Lakshmi Reethika Guduri

## Run

```powershell
dotnet run --project JobLensChat --launch-profile http
```

- Home: http://localhost:5080/
- Chat: http://localhost:5080/Chat

The HTTPS profile is https://localhost:7080, with HTTP still on http://localhost:5080. The SDK is pinned in `global.json` to 10.0.401 (latest patch of that band).

## Packages

Pinned in `JobLensChat/JobLensChat.csproj`. This phase references them and does not call them yet.

- ModelContextProtocol.AspNetCore 2.2.0
- Microsoft.Data.Sqlite 10.0.12
- Microsoft.Extensions.AI 10.10.0
- Microsoft.Extensions.AI.OpenAI 10.10.0
- OpenAI 2.14.0

## Layout

```
joblens-chat/
├── .cursorrules
├── global.json
├── docs/
├── JobLensChat/          ASP.NET Core MVC app (net10.0)
└── README.md
```

Inside `JobLensChat/`:

- `Controllers/` — Home and Chat
- `Models/` — view models
- `Views/` — Razor pages
- `Data/`, `Mcp/`, `Services/` — empty folders for later phases
- `App_Data/` — local SQLite file later; `*.db` is gitignored
- `wwwroot/css/site.css` — JobLens light theme

The pages use that stylesheet. Bootstrap and jQuery are not part of the app.

## Docs

The proposal and project records are in `docs/`.
