---
name: xke-dotnet8-maintainer
description: Continue or review work inside `XKE.DotNet8` while preserving the current vertical-slice migration strategy, local runtime data rules, test gates, and sensitive-data constraints. Use when modifying `XKE.DotNet8/src`, `XKE.DotNet8/tests`, `XKE.DotNet8/tools`, `XKE.DotNet8/fixtures`, or when deciding the next slice boundary for the .NET rewrite.
---

# XKE DotNet8 Maintainer

Keep the .NET rewrite on the current vertical-slice path. Do not reopen the old “flatten by technical layer” plan.

## Current Baseline

- Treat these slices as completed baselines unless the user explicitly reopens them: host shell, Auth, Notice/control plane, Desktop settings, Schedule mobile-only chain, Network, Telemetry local-only, Feedback local-only.
- Treat these as deferred: Course, traditional JW captcha login, cookie schedule refresh, Network cloud integrations, Telemetry cloud upload, Feedback cloud upload, WebView2 host bridge closure, tray, autostart system registration, single-instance, system notifications.
- Keep `Xke.Core` as one project with namespace-level modularity. Do not split csproj files unless a boundary becomes stable through delivered slices.

## Non-Negotiables

- Keep runtime writes inside `XKE.DotNet8/.runtime/data` or the overridden `XKE_DOTNET_DATA_DIR`.
- Do not read from or write to Python legacy runtime `data/`, `.env`, real token caches, or mirrored production snapshots.
- Keep fixtures desensitized. Never commit real credentials, captcha material, tokens, cloud keys, telemetry payloads from production, or legacy data exports.
- Preserve current API compatibility only for slices already delivered. Do not pre-model future contracts.
- Add comments only at boundaries and side effects: DPAPI storage, local-only transport, system commands, blocked-policy enforcement, persistence decisions.

## Required Workflow

1. Re-ground on the current slice boundary before editing.
2. Prefer changing storage and core orchestration before route glue or frontend wording.
3. Add or update tests in the same slice: contracts, storage/core, WebHost integration.
4. Run:
   - `dotnet build .\XKE.DotNet8\XKE.DotNet8.sln`
   - `dotnet test .\XKE.DotNet8\XKE.DotNet8.sln`
5. If relevant, add or update one smoke entry under `XKE.DotNet8/tools`.

## File Heuristics

- For runtime persistence, inspect `src/Xke.Storage`.
- For API shapes, inspect `src/Xke.Contracts` before adding anonymous payloads.
- For route behavior, inspect `src/Xke.WebHost/XkeWebHostApplication.cs`.
- For blocked-policy behavior, follow the existing `EnsureSessionAllowedAsync` / notice access-decision pattern.
- For tests, prefer extending the existing xUnit projects instead of creating ad hoc scripts.

## Slice Decisions

- If a feature depends on traditional JW captcha or cookie-only legacy state, defer it unless the user explicitly changes scope.
- If cloud upload is not part of the approved slice, implement a local-only loop first and keep a replaceable transport interface.
- If a feature can affect the host machine, keep the side effect behind an interface and make failure non-fatal unless the plan says otherwise.
