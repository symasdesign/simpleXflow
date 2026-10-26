# Testing simpleXflow

The baseline exercises domain rules, sample consistency, real relational persistence, HTTP/Identity,
imports, the JavaScript bridge and the complete Blazor workspace with the shipped modeler bundle.
All default tests run locally without Azure, email delivery, Docker or production database access.

Both GitHub Actions workflows pin `ubuntu-24.04`, which is supported by the locked Playwright 1.58.2
version. Keep the runner image and browser dependency versions compatible when upgrading either;
`ubuntu-latest` can advance to an unsupported distribution independently of this repository.

Workspace collaboration update verified on Windows on 2026-10-26: **427 passing tests, none skipped**
(391 .NET, 22 editor bridge/real-modeler, 14 end-to-end). The Release build has no warnings or errors.
The browser flows cover the organization/workspace/project/folder hierarchy, model selection without
fullscreen, autosave, conflict preservation, local `.sxf` export/import, invitation links, version releases,
work packages, model deletion, legacy sample links and the header navigation with searchable workspace
cards/list, filters, sorting and direct member management. Real-modeler regressions additionally verify
Architecture-to-Logic resource synchronization, viewer restrictions and immediate dirty-state tracking.
See [Workspace behavior and migration](workspaces.md) and [Email configuration](workspace-email.md).

Verified on Windows on 2026-09-06: **385 passing tests, none skipped** (357 .NET, 18 editor bridge,
10 end-to-end), Release build without warnings and successful web publish. Combined .NET line coverage:
Application 100%, Domain 99.38%, Infrastructure 99.07%, Web 26.47%. The Web number excludes the separate
browser process and retains the untested account/interactive paths in its denominator. The GitHub Linux
workflow is configured but has not been executed remotely as part of this local change.

## Run everything

Prerequisites: .NET SDK 10, PowerShell 7 and Node.js 22 with npm. Run from the repository root:

```powershell
./scripts/test.ps1 -InstallBrowsers
```

The first run restores NuGet/npm dependencies, installs Chromium (and required OS dependencies),
builds Release, runs all .NET tests with coverage, then runs both browser suites. Later runs can omit
`-InstallBrowsers`. Any failed stage returns a failure; publishing cannot proceed after a failed test.

```powershell
./scripts/test.ps1 -SkipBrowser             # .NET tests and coverage only
./scripts/test.ps1 -NoRestore               # Reuse installed NuGet/npm dependencies
./scripts/test.ps1 -Configuration Debug     # Same suite against Debug builds
```

`-NoRestore` still builds and tests. It requires a previous successful restore/npm installation.
The script restores the working directory and test configuration environment variable even on failure.

## Suites

| Suite | What it verifies |
| --- | --- |
| `SimpleXflow.Domain.Tests` | Validation before mutation; normalization; tenant slugs; attachment/version invariants; unchanged updates; ordered multi-step and legacy undo |
| `SimpleXflow.Application.Tests` | Sample lookup, XML namespaces, unique IDs, flow/reference integrity, reachability, diagram geometry and resource/logic relationships |
| `SimpleXflow.Infrastructure.Tests` | Real SQLite constraints and transactions; create/read/update/undo across fresh contexts; uniqueness, cancellation and rollback; tenant query filters and all SaveChanges overloads; Identity stores/claims; provider aliases and offline migration scripts/snapshots |
| `SimpleXflow.Web.Tests` | Real middleware, Razor form POSTs, antiforgery, registration/login/logout, cookies/tenant claims, authorization, database initialization/health failure; bounded BPMN/XML/ZIP imports |
| `tests/browser/bridge` | Browser DOM with a substituted legacy bundle: loading/retry, XML/logic round trips, escaping, project switches, debounced notifications, keyboard filtering and teardown |
| `tests/browser/e2e` | Actual shipped modeler and Blazor server: registration/login/logout, persisted projects/autosave, diagram editing, tenant isolation, all demo samples, fullscreen and file import |

Run a .NET suite or a focused regression directly:

```powershell
dotnet test simpleXflow.slnx
dotnet test tests/SimpleXflow.Infrastructure.Tests --filter FullyQualifiedName~Tenant
npm test                         # Editor bridge only; does not need a web server
dotnet build src/SimpleXflow.Web --configuration Release
npm run test:e2e                  # Starts and stops its own server
npm run test:e2e -- --grep autosave
```

Use `SIMPLEXFLOW_TEST_CONFIGURATION=Debug` if running browser tests directly against a Debug build.
Set `SIMPLEXFLOW_TEST_PORT` to another free port if the default `5187` is occupied.
Existing servers are deliberately never reused. Playwright retries are disabled, so failures remain visible.

## Isolation and fixtures

- Every SQLite persistence fixture owns a separate open in-memory connection. Tests verify results
  through fresh contexts, rather than depending on EF's tracked objects. Direct contexts and Identity
  service registrations use schema version 3, matching the app, so EF model caching cannot make
  passkey mapping depend on test execution order.
- Every HTTP test host uses its own temporary database, real Identity services and cookies. It replaces
  provider configuration before startup and removes the database when disposed.
- Every browser run creates a unique database under `artifacts/browser/databases`; every test registers
  its own account/tenant. Browser contexts do not share cookies. Database files remain local diagnostic
  artifacts and are ignored by Git; CI does not upload them.
- The import fixture contains a small valid BPMN diagram. ZIP and boundary inputs are generated in
  tests, including a compressed model whose expanded size exceeds 20 MiB. Archives are never extracted
  to disk. Both upload and expanded model sizes are checked.
- Browser tests assert user-visible/persisted outcomes. Bridge tests use the browser clock to verify
  debounce and import suppression without real-time sleeps. Uncaught errors fail the main E2E page.

## Reports and coverage

Each complete run creates `artifacts/test-results/<timestamp>/` containing TRX results, individual
Cobertura XML reports and `coverage-summary.md`. The summary merges covered line hits across test
projects by assembly, source file and line. It does not sum duplicate coverage or merge branch counters.

`tests/coverage.runsettings` includes production assemblies and excludes migrations, generated files
and test assemblies. Async methods remain included. There is no artificial coverage threshold: a high
percentage is not a substitute for assertions about behavior. Browser JS coverage is separate from .NET
coverage and is not included in the summary; WebApplicationFactory also does not execute interactive
browser code in its coverage process.

Browser HTML reports are in `artifacts/browser/bridge-report` and `artifacts/browser/e2e-report`.
Failures retain screenshots and Playwright traces in the corresponding `*-results` directory:

```powershell
npx playwright show-report artifacts/browser/e2e-report
npx playwright show-trace <path-to-trace.zip>
./scripts/coverage-summary.ps1 -ResultsDirectory artifacts/test-results/<timestamp>
```

The `Tests` GitHub Actions workflow runs on pull requests and main/master pushes and uploads reports
even after failure. The container pipeline also runs both .NET and browser suites before image publishing.
No workflow changes are applied to a remote repository by creating this test baseline locally.

## Regression fixes included with this baseline

- All `SaveChanges` overloads enforce tenant guards, including the original tenant of tracked entities.
- Tenant-less administrative reads work without evaluating an unavailable tenant ID.
- Registration cannot join an existing organization by guessing its name; workspace invitations require a matching verified email.
- Invalid POST requests retain their error response instead of replaying the submission on a Razor error page.
- Model updates normalize optional logic consistently, and three missing demo diagram edges are supplied.
- Logic embedding preserves self-closing XML tags, and editor initialization can retry a failed bundle load.
- Import validates the complete XML document and BPMN namespace, prohibits DTDs and limits ZIP expansion.

## Explicit boundaries

This is a regression baseline for the existing application, not proof that every possible workflow is correct.

- PostgreSQL/SQL Server configuration, model snapshots and generated Up/Down migration scripts are tested
  offline. Executing migrations on native servers, legacy production upgrades, TLS, concurrency and
  provider-specific behavior require disposable databases in a separate integration environment.
- SQLite and the current guards do not enforce the separate composite relationship invariant that an
  attachment/version's tenant must equal the tenant of its referenced project. This requires additional
  relationship/schema hardening before exposing such writes through future endpoints.
- Existing historical versions/legacy undo and workspace model history are tested. Changed workspace
  model saves create snapshots; local diagram undo remains separate from saved history.
- Import checks XML well-formedness and the BPMN definitions namespace; full BPMN XSD and simpleXflow
  semantic validation remain separate features.
- Chromium is covered; Firefox/WebKit, accessibility audits, load tests, external providers, passkeys,
  complete two-factor/email recovery flows and deployment smoke tests are not part of the default suite.
- Interactive UI behavior is covered through E2E tests; no separate bUnit suite or tests for each generated
  Identity screen are included. The vendored modeler's internal source is not unit-tested here.

When adding a feature, extend the lowest appropriate suite and add a browser regression for a new
critical user workflow. Keep tests independent, prefer fresh persistence contexts, and validate both
accepted inputs and failures that must preserve existing data.
