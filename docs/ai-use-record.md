# AI Use Record (JobLens)

Add one entry for each meaningful AI suggestion the team acts on. Copy the blank entry, fill it in, and keep newer entries under **Entries**. Summarize the suggestion. Do not paste secrets, keys, or full chat transcripts.

## Entry template

- **Date:** YYYY-MM-DD
- **Phase:**
- **Goal:**
- **Tool:**
- **AI suggested:**
- **What we checked:**
- **Decision:** Accept / Revise / Reject — and what we will do
- **Who decided:**
- **Evidence:** link, or a file in `docs/evidence/`

## Entries

### 1 — 2026-11-06 — Choose the AI provider

- **Date:** 2026-11-06
- **Phase:** 0
- **Goal:** Choose the AI provider
- **Tool:** Cursor
- **AI suggested:** GitHub Models
- **What we checked:** The GitHub docs and changelog. GitHub Models was retired on July 30, 2026. The playground, model catalog, inference API, and bring-your-own-key support are no longer available. Microsoft’s migration guide sends new projects to Microsoft Foundry.
- **Decision:** Revise. Azure OpenAI via Foundry; gpt-4o-mini had no quota, final model gpt-4.1-mini, deployment gpt-4.1-mini-1.
- **Who decided:** Team, while setting up the repo
- **Evidence:**
  - [GitHub Models docs](https://docs.github.com/en/github-models) — retired as of July 30, 2026
  - [GitHub changelog: GitHub Models is now retired](https://github.blog/changelog/2026-07-30-github-models-is-now-retired/)
  - [Migrate from GitHub Models to Microsoft Foundry Models](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/quickstart-github-models)

This entry is the “wrong AI suggestion” example for the presentation. The model named a service that had already been shut down. We checked the docs and changed the decision.

### 2 — 2026-11-06 — Model choice after quota error

- **Date:** 2026-11-06
- **Phase:** 5
- **Goal:** Pick a deployable chat model after the first Azure quota failure
- **Tool:** Cursor
- **AI suggested:** Groq with an open-source model
- **What we checked:** Groq returned 404 model-not-found; the course allowed Azure credits, so we stayed on Foundry. `docs/DOCUMENTATION.md` records Azure OpenAI **gpt-4.1-mini** (deployment `gpt-4.1-mini-1`). Managed Compute / GPU SKUs were far above the free-trial budget.
- **Decision:** Revise → Azure OpenAI gpt-4.1-mini (Global Standard); rejected Managed Compute GPU deployment as too costly.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** `src/JobLens.Infrastructure/AI/AiClientFactory.cs` (commit `0a82ba7`)

### 3 — 2026-11-06 — Grounding the chatbot

- **Date:** 2026-11-06
- **Phase:** 5
- **Goal:** Keep chat answers tied to JobLens data
- **Tool:** Cursor
- **AI suggested:** Let the model answer the user directly from general knowledge
- **What we checked:** Without tools the bot called an old domain “legitimate.” Course rules require MCP tools + system prompt. `ChatOrchestrator` rule 8 forbids calling a company/domain/recruiter safe from age or score alone; T01–T12 in `docs/testing/test-matrix.md` check known answers from `docs/testing/known-answers.md`.
- **Decision:** Revise → MCP tools + system prompt; never call a company safe from age/score alone; approved-only tool data.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** `src/JobLens.Web/Chat/ChatOrchestrator.cs`; chatbot cases T01–T12 (`docs/testing/test-matrix.md`, `docs/testing/known-answers.md`)

### 4 — 2026-11-06 — RDAP failures

- **Date:** 2026-11-06
- **Phase:** 4
- **Goal:** Reliable domain registration age for Check Recruiter
- **Tool:** Cursor
- **AI suggested:** A single call to rdap.org
- **What we checked:** rdap.org timed out and returned varied JSON shapes. Docs specify registry-first Verisign (.com/.net) and PIR (.org), rdap.org fallback, 6 s per attempt, 6 h cache (`docs/DOCUMENTATION.md` § external APIs).
- **Decision:** Revise → registry-first clients, per-attempt timeout, rdap.org fallback, 6 h cache, tolerant parser + fixture tests.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** `src/JobLens.Infrastructure/External/RdapClient.cs`; `tests/JobLens.Tests/RdapParserTests.cs`, `RdapRegistryUrlTests.cs` (commit `694e261`)

### 5 — 2026-11-07 — Security review

- **Date:** 2026-11-07
- **Phase:** 10
- **Goal:** Close production safety gaps before Azure
- **Tool:** Cursor (AI code review)
- **AI suggested:** Flagged fail-open `/mcp`, missing PII redaction in chat, seed passwords usable in production, and missing rate limits
- **What we checked:** Each finding against `Program.cs`, MCP middleware, `PiiRedactor` / `ChatOrchestrator.PrepareQuestion`, seed initializer, and login rate limiting. Docs list fail-closed 503, `X-Api-Key`, and the expensive policy.
- **Decision:** Accept → fail-closed 503, `X-Api-Key`, `Redact()` on chat input, production seed guard, “expensive” rate limit.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** hardening commit `bf7f9d4`

### 6 — 2026-11-08 — Live jobs API

- **Date:** 2026-11-08
- **Phase:** 12
- **Goal:** Show live job openings with a trustworthy source
- **Tool:** Cursor
- **AI suggested:** Adzuna (paid), then The Muse
- **What we checked:** Adzuna needed a paid key. The Muse connection was reset on our network. Docs state live jobs come only from USAJOBS behind `ILiveJobsProvider`.
- **Decision:** Revise → `ILiveJobsProvider` + USAJOBS official Search API.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** `src/JobLens.Infrastructure/External/UsaJobsClient.cs` (commit `8279379`); Muse removed in `8282529`

### 7 — 2026-11-07 — Large Cursor prompt blocked

- **Date:** 2026-11-07
- **Phase:** 10
- **Goal:** Finish sign-in, roles, and `/mcp` access in one pass
- **Tool:** Cursor
- **AI suggested:** One long combined Phase 10 prompt
- **What we checked:** The provider rejected the oversized prompt. Smaller slices with neutral wording still matched the Phase 10 checklist (auth, roles, login throttle, MCP key).
- **Decision:** Revise → split into smaller prompts (10A–10D) with neutral wording.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** Phase 10 commits `dcfca84`, `52a6594`

### 8 — 2026-11-09 — Feature scope and cleanup

- **Date:** 2026-11-09
- **Phase:** 12
- **Goal:** Remove dead paths and avoid duplicate schema
- **Tool:** Cursor (audit)
- **AI suggested:** New columns/features and listed duplicates/dead code (including a new SuspiciousInterview column)
- **What we checked:** Each item in code. `InterviewReview.WasLegit` already covers suspicious interviews (`docs/testing/known-answers.md`: 7 with WasLegit = false). The Muse client and `LiveJobsController` were redundant with `JobsController` + USAJOBS; resume “tips” path was unused.
- **Decision:** Revise → reuse `WasLegit`; remove The Muse, `LiveJobsController`, resume tips.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** final-cleanup commit `8282529`

### 9 — 2026-11-09 — Azure App Settings script

- **Date:** 2026-11-09
- **Phase:** 13
- **Goal:** Apply App Service settings without putting secrets in Git
- **Tool:** Cursor
- **AI suggested:** A PowerShell script that set labels and generated passwords
- **What we checked:** Windows PowerShell rejected `$Label:` parsing, non-ASCII dashes, advanced-function parameter binding, and `%` in generated passwords. We ran the script dry and fixed each failure mode.
- **Decision:** Revise → corrected script, ASCII only, generated passwords without cmd-sensitive characters.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** `scripts/azure-appsettings.ps1` (commits `2e7cfb9`, `fdd3b68`)

### 10 — 2026-11-09 — Azure deployment

- **Date:** 2026-11-09
- **Phase:** 13
- **Goal:** Host JobLens on Azure App Service (Linux, .NET 10, F1)
- **Tool:** Cursor / Azure Portal
- **AI suggested:** Deployment Center with GitHub/OIDC continuous deploy
- **What we checked:** GitHub/OIDC setup failed in the portal. Free F1 quota existed only in West US 3 (`docs/DOCUMENTATION.md` / README).
- **Decision:** Revise → create the app in West US 3; deploy with `az webapp deploy` zip.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** README deploy section; `docs/DOCUMENTATION.md` (commit `f46111f` and later README updates)

### 11 — 2026-11-09 — Case study images

- **Date:** 2026-11-09
- **Phase:** 14
- **Goal:** Illustrate four sample scam email threads for teaching
- **Tool:** AI image generator (DALL·E)
- **AI suggested:** Full email screenshots from our written prompts (case1a–case4c)
- **What we checked:** Every PNG for wrong text; added a “SAMPLE – JobLens demo” watermark and fictional names only. Catalog references `case{n}{a|b|c}.png` under `wwwroot/images/cases`.
- **Decision:** Accept with review.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** `src/JobLens.Web/wwwroot/images/cases/` (commits `4efa060`, `0921c12`)

### 12 — 2026-11-09 — Documentation

- **Date:** 2026-11-09
- **Phase:** 14
- **Goal:** Ship accurate project documentation for grading and handoff
- **Tool:** Cursor
- **AI suggested:** Draft `DOCUMENTATION.md` and README from the codebase
- **What we checked:** MCP tool argument names, system-prompt rule points, and routes against `src/JobLens.Web/Mcp`, `ChatOrchestrator`, and controllers; fixed mismatches before merge.
- **Decision:** Revise → keep the draft after verifying tools, rules, and routes against source.
- **Who decided:** Sathvik (reviewed with team)
- **Evidence:** `docs/DOCUMENTATION.md` (commit `1bb3d5f`)

## Summary

| Decision | Count |
|----------|------:|
| Accepted | 2 |
| Revised | 10 |
| Rejected | 0 |

**Lesson:** AI output was always verified against docs, code or tests before use.
