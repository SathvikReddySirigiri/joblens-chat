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
- **Decision:** Revise. Use Azure OpenAI (`gpt-4o-mini` via Foundry).
- **Who decided:** Team, while setting up the repo
- **Evidence:**
  - [GitHub Models docs](https://docs.github.com/en/github-models) — retired as of July 30, 2026
  - [GitHub changelog: GitHub Models is now retired](https://github.blog/changelog/2026-07-30-github-models-is-now-retired/)
  - [Migrate from GitHub Models to Microsoft Foundry Models](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/quickstart-github-models)

This entry is the “wrong AI suggestion” example for the presentation. The model named a service that had already been shut down. We checked the docs and changed the decision.
