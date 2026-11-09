# Team Record (JobLens)

One row per person. Update the row when a role or ownership changes.

| Name | Role | Owns | Notes |
|------|------|------|-------|
| Naga Pavan Sathvik Reddy Sirigiri | MCP & Backend Lead | Architecture, SQLite data layer, MCP server/tools, chatbot, external APIs, Azure | |
| Isabella Cuello Penaloza | Data & Analytics Lead | Seed data & known answers, CRUD rules, dashboard metrics, CRUD/role testing | TODO(Isabella): confirm |
| Lakshmi Reethika Guduri | MVC & Experience + Testing & Documentation Lead | Views/UX, test matrix & evidence, documentation, presentation | TODO(Lakshmi): confirm |

## How to fill a row

- **Name:** the person on the team.
- **Role:** their job on JobLens (for example, frontend, API, testing, or docs).
- **Owns:** the part of the product they are responsible for.
- **Notes:** what they finished in the current phase, or a handoff note.

## Contributions by phase

Dates from `git log --date=short` (nearest related commit on `main`).

| Phase | What shipped | Owners | Nearest commit date |
|------:|--------------|--------|---------------------|
| 0 | Repo setup and documentation templates | Sathvik | 2026-11-06 (`b1337a9`) |
| 1 | Project proposal (Checkpoint 1) | Team | 2026-11-06 (`6bb7e3b`) |
| 2 | .NET 10 MVC skeleton, pinned packages, JobLens light theme | Lakshmi, Sathvik | 2026-11-06 (`48d627e`) |
| 3 | MCP server / tool vertical slice (before Inspector evidence) | Sathvik | 2026-11-06 (`e3749d6` merge) |
| 4 | MCP tools tested in Inspector and Cursor; RDAP registry-first client | Sathvik | 2026-11-06 (`e542ea0`, `694e261`) |
| 5 | Azure OpenAI gpt-4.1-mini via MCP tools, Demo fallback, A5 grounding | Sathvik | 2026-11-06 (`0a82ba7`) |
| 6 | Full data model (7+ tables), repositories, services, demo users, tests | Isabella, Sathvik | 2026-11-06 (`ebf9af4`) |
| 7 | CRUD pages (reports moderation, companies, recruiters, jobs, reviews) | Lakshmi, Isabella | 2026-11-07 (`74c3cb7`) |
| 8 | Check Recruiter, Offer Analyzer, Smart Search, extra MCP tools | Sathvik, Lakshmi | 2026-11-07 (`c21b424`) |
| 9 | Live scam insights dashboard (Chart.js) and derived money insight | Isabella, Lakshmi | 2026-11-07 (`dbd178c`) |
| 10 | Sign-in, roles/permissions, login throttling, `/mcp` access key; hardening | Sathvik | 2026-11-07 (`52a6594`, `bf7f9d4`) |
| 11 | Home, About Us, Architecture page, UX polish, friendly errors | Lakshmi, Sathvik | 2026-11-07 (`746b051`) |
| 12 | UI consistency, Phase 12 test prep, USAJOBS live jobs, Muse cleanup | Lakshmi, Sathvik | 2026-11-08–09 (`8279379`, `8ce0f68`, `8282529`) |
| 13 | Health check, data-protection keys, Azure docs and App Settings script | Sathvik | 2026-11-09 (`f46111f`, `2e7cfb9`, `fdd3b68`) |
| 14 | Full DOCUMENTATION.md, case studies page, AI/team records | Lakshmi, Sathvik | 2026-11-09 (`1bb3d5f`, `4efa060`) |

## Meeting log

| Date | Attendees | Decisions | Next steps |
|------|-----------|-----------|------------|
| TODO: fill | TODO: fill | TODO: fill | TODO: fill |
| TODO: fill | TODO: fill | TODO: fill | TODO: fill |
| TODO: fill | TODO: fill | TODO: fill | TODO: fill |
