# JobLens Chat — Project Proposal (Checkpoint 1)
ISM 6225 · App Dev for Analytics · Fall 2026
Team: Naga Pavan Sathvik Reddy Sirigiri · Isabella Cuello Penaloza · Lakshmi Reethika Guduri

## 1. Business problem
Job and internship scams target college students — especially international students — through
fake recruiters, look-alike company emails (e.g., amaz0n-careers.net), and offers that ask for
money or personal information. University career centers receive scattered reports by email
and chat, but cannot quickly answer questions like "Which companies are being impersonated
most?" or "Has this recruiter domain been reported before?"

## 2. Audience and decisions
| Audience | Decision they need to make |
|---|---|
| Career-center staff (primary) | Which scams to warn students about this week; which companies/domains to flag; whether scam activity is rising |
| Students (secondary) | Whether a specific recruiter email/domain is safe to reply to |

## 3. Solution
JobLens Chat is a C# ASP.NET Core MVC web app with a chatbot. Users ask business questions in
plain English. The chatbot connects as an MCP client to the app's own HTTP MCP server, discovers
read-only tools at runtime, and answers ONLY from tool results that query a SQLite database of
synthetic scam reports.

Why chat + tools adds value: staff get answers in seconds without SQL or dashboards, and every
data claim is grounded in a controlled, parameterized query — the model never invents data or
runs arbitrary SQL.

## 4. Business questions the chatbot will answer
1. Has the domain amaz0n-careers.net been reported? (exact lookup)
2. Is hr@rnicrosoft-hiring.com a legitimate Microsoft recruiter? (recruiter check)
3. Show approved equipment-purchase scams reported since September. (filtered search)
4. List Telegram-based scam reports impersonating Deloitte. (filtered search)
5. How many scam reports did we receive this quarter, by type and status? (aggregate)
6. What is the total money requested by scammers in October? (aggregate)
7. Which 5 companies are impersonated most often? (ranking + join)
8. Which companies were impersonated most since August? (ranking + join, date filter)
9. Is recruiting@google.com legitimate? (recruiter check — expected LOW risk)
10. Has the domain safe-company-example.com been reported? (no-result case)

## 5. Data model (SQLite, synthetic data only)
### Companies (10 rows)
| Column | Type | Notes |
|---|---|---|
| CompanyId | INTEGER | PK, autoincrement |
| Name | TEXT | NOT NULL, UNIQUE (e.g., Amazon) |
| OfficialDomain | TEXT | NOT NULL, UNIQUE (e.g., amazon.com) |
| Industry | TEXT | NOT NULL (Technology, Consulting, Finance, Retail, Healthcare) |
| HqState | TEXT | NOT NULL (two-letter code) |

### ScamReports (60 rows)
| Column | Type | Notes |
|---|---|---|
| ReportId | INTEGER | PK, autoincrement |
| CompanyId | INTEGER | FK → Companies.CompanyId (company impersonated) |
| RecruiterEmail | TEXT | NOT NULL |
| Domain | TEXT | NOT NULL (derived from email) |
| ScamType | TEXT | NOT NULL — Fake check, Equipment purchase, Upfront fee, Chat-only interview, Identity theft, Fake staffing agency |
| Channel | TEXT | NOT NULL — Email, Telegram, WhatsApp, LinkedIn, Text message |
| AmountRequestedUsd | REAL | NOT NULL, CHECK ≥ 0 (0 if no money requested) |
| DomainAgeDays | INTEGER | NOT NULL, CHECK ≥ 0 (synthetic) |
| Status | TEXT | NOT NULL — Pending, Approved, Rejected |
| ReportedDate | TEXT | NOT NULL, ISO date (2026-01-01 to 2026-10-31) |

Relationship: one Company → many ScamReports (one-to-many via CompanyId).
Total seed records: 70 (requirement: ≥ 25). Numeric fields: AmountRequestedUsd, DomainAgeDays.
Categorical fields: ScamType, Channel, Status, Industry.

## 6. MCP tools (read-only, bounded, no arbitrary SQL)
| Tool | Type | Inputs (validation) | Output (bounded) |
|---|---|---|---|
| get_domain_report | Exact lookup | domain (required, valid domain format, ≤ 100 chars) | All reports for that exact domain (max 20) + company impersonated, or "no reports found" |
| search_scam_reports | Filtered search | scamType, companyName, channel, status, fromDate, toDate (all optional; dates ISO; fromDate ≤ toDate) | Matching reports, newest first, max 20 rows |
| summarize_scams | Aggregate | fromDate, toDate (optional, ISO) | Total reports, counts by ScamType and Status, total and average AmountRequestedUsd |
| top_impersonated_companies | Ranking + join | count (clamped 1–10), fromDate (optional) | Companies ranked by approved report count (Companies ⋈ ScamReports) |
| check_recruiter_email | Domain logic + lookup | email (required, valid format, ≤ 254 chars) | Risk score 0–100, verdict LOW/MEDIUM/HIGH, reasons (official domain match, look-alike, free email provider, prior reports) |

No execute_sql/run_query tool. No write tools.

## 7. Chatbot behavior
- Modes: AI mode (Azure OpenAI gpt-4o-mini via Microsoft Foundry) and Guided Demo mode (no key;
  intent rules call the same MCP tools). The UI shows which mode is active.
- System prompt: audience = career-center staff and students; every data claim must come from an
  MCP tool; never invent reports, companies, or numbers; if a tool returns no results, say so
  clearly and suggest verifying on the company's official careers page.
- Evidence: each answer shows "Tool used: <tool_name>"; every tool call is logged.
- Chat page: 2–500 character validation, anti-forgery token, last 12 turns kept in session.

## 8. Pages
- Home / About & Architecture: business problem, request flow, MCP boundary, schema, security, team.
- Chat: conversation, mode label, tool evidence, sample questions, clear-history button.

## 9. Security & responsible AI
- Parameterized SQLite commands only; output limits on every tool; input validation.
- Anti-forgery on POST; HTTPS; exact AllowedHosts on Azure.
- Secrets (API key) only in User Secrets locally and Azure environment variables — never in Git.
- Tests: prompt injection, SQL-like input, missing data, long input, model/tool failure.
- Limitations: synthetic data; SQLite single-instance only.
- Fairness/misuse: a legitimate company or recruiter could be wrongly associated with scams;
  data is synthetic, only Approved reports drive rankings, and the bot advises verification
  rather than making accusations.

## 10. Architecture
Browser → ChatController (MVC) → ChatOrchestrator → MCP client → HTTP /mcp → ScamTools →
ScamRepository (parameterized SQL) → SQLite (App_Data locally; HOME/data/JobLens on Azure).
Deployed as one Azure App Service (Linux, .NET 10, F1, single instance).

## 11. Team roles
| Member | Role | Responsibilities |
|---|---|---|
| Isabella Cuello Penaloza | Data & analytics lead | Schema, seed data, repository, verifying tool results against data |
| Naga Pavan Sathvik Reddy Sirigiri | MCP & backend lead | MCP tools, validation, orchestrator, system prompt, logging |
| Lakshmi Reethika Guduri | MVC & experience + cloud, test & documentation lead | Controllers, views, accessibility, test matrix, Azure deployment evidence, report |

## 12. Risks and mitigations
| Risk | Mitigation |
|---|---|
| AI provider unavailable or quota issues | Guided Demo mode works without a key |
| Azure free credit ends (30 days) before grading | Free F1 tier; upgrade to pay-as-you-go before expiry; backup demo video |
| SQLite data lost on redeploy | Database stored under HOME/data on Azure (persistent share); restart test |
| Model gives ungrounded answers | Narrow read-only tools, strict system prompt, tool evidence in UI, evaluation matrix |
| Timeline | Vertical slice (2 tools end-to-end) first, then remaining tools |

## 13. Milestones
| Checkpoint | Target |
|---|---|
| 1. Proposal | This document |
| 2. Local vertical slice | Chat → MCP tool → SQLite working for 2 tools |
| 3. Feature complete | 5 tools, AI + Demo modes, tests, draft evidence |
| 4. Cloud / rehearsal | Azure URL, restart test, timed rehearsal, backup demo (48 h before presentation) |
