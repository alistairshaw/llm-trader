# Local Development

## Purpose

This project uses a Docker-first development workflow on Windows. The default workflow must not require a locally installed .NET SDK, C# build tools, SQLite tools, or project-specific command-line utilities.

The repository will provide the container definitions and scripts needed to restore, build, test, and run the cross-platform application. Windows-only WPF work is the deliberate exception described below.

## Decisions

### 1. Default development environment

The default local development environment is a Linux container based on the official .NET 10 SDK image. Docker Compose will provide stable commands for common developer operations rather than requiring developers to reproduce long `docker run` commands.

The development container will contain:

- The .NET 10 SDK and runtime.
- The tools restored from the repository's .NET tool manifest.
- NuGet packages restored from the centrally managed, locked dependency graph.
- Native libraries required by the headless host and its tests.
- The SQLite runtime used through the application's .NET provider.

The source tree is bind-mounted into the container. Generated build output remains disposable. NuGet packages should use a named Docker volume so repeated restores are fast without placing package caches in the repository.

The container image is part of the project's build definition and should be pinned to an explicit .NET 10 SDK patch or immutable digest when the solution is initialized. Upgrades are made in a reviewed change and verified in CI.

### 2. Application execution

The cross-platform `Trading.Host` process is the primary local executable. It runs in a Linux container using the same .NET Generic Host composition as production-like headless environments.

The paper-execution composition keeps readiness false until migrations, expired-lease recovery, and reconciliation of
every account owning incomplete work have completed. Recovery processes durable order work before deferred broker
events and fills. Shutdown cancellation stops subsequent claims; claimed work is either durably completed or returned
to a bounded retry state with cleared lease ownership, so the next process reconstructs its action from SQLite alone.

Local infrastructure is intentionally small:

- SQLite remains the application-owned system of record.
- Simulated brokers, scripted LLM clients, fixture-backed market data, and fixture-backed research providers are the default.
- Commit-gating development and test commands do not contact real LLMs, public websites, live market-data services, or broker accounts.
- Optional external integrations will be introduced behind explicit Docker Compose profiles or separate opt-in commands. They must not be required for the normal build or test loop.

The SQLite database must not be kept in the bind-mounted OneDrive working tree while the application is running. File synchronization can interfere with SQLite locking and durability semantics. Local runtime data should be stored in a Docker named volume mounted at a stable path such as `/data`. Tests use isolated temporary databases inside the container and remove them after the test run.

#### Deterministic paper broker fixtures

`SimulatedPaperBroker` is the network-free broker adapter used by commit-gating workflows. A fixture binds one exact
paper connection, account, and named environment, then configures a bounded script per stable client order ID. Scripts
select acceptance, rejection, an unknown result, or timeout after broker acceptance and may emit acknowledgements,
rejections, cancellation, expiration, partial fills, final fills, duplicate messages, and deliberately out-of-order
messages. Cancellation outcomes are independently scriptable.

Tests supply the UTC clock, broker order IDs, source message IDs, execution IDs, and latency seam. Exact duplicate
submissions return the original broker identity; a timeout after acceptance is discoverable by client-ID lookup and
must be reconciled before retry. Duplicate scripted events retain their original source and execution identities so
inbox and fill idempotency can be exercised. Every operation validates the configured paper identity before latency
or state mutation. The simulator has no configuration surface for URLs, network clients, credentials, or a live
environment.

### 3. Build workflow

The repository-root `dev.ps1` wrapper will expose short, documented commands for these operations:

| Operation | Execution environment | Underlying action |
| --- | --- | --- |
| Restore | Linux SDK container | `dotnet restore --locked-mode` |
| Format check | Linux SDK container | `dotnet format --verify-no-changes --no-restore` |
| Full solution compile | Linux SDK container | Release build, including WPF with Windows targeting enabled |
| Publish WPF for local testing | Linux SDK container | Produce a self-contained Windows artifact in a host-visible artifacts directory |
| Full cross-platform test | Linux SDK container | Unit, architecture, SQLite integration, component, and non-UI Reqnroll tests |
| Run headless host | Linux runtime through Compose | Start `Trading.Host` with local configuration and persistent `/data` storage |
| Run WPF for manual testing | Windows host | Launch the self-contained artifact; no host .NET installation required |
| Native Windows validation build | Windows CI | Build the `net10.0-windows` projects on Windows |
| WPF UI tests | Interactive Windows CI; optionally the Windows host | Run the Reqnroll/FlaUI smoke suite |

The initial commands are `.\dev.ps1 restore`, `build`, `format`, `test`, `solution-list`, and `reference-list`. Focused tests accept `-Project` and optional `-Filter` arguments. `.\dev.ps1 verify-build-conventions` proves that compiler warnings and cross-platform uses of Windows-only APIs fail the build by compiling isolated negative fixtures. Later runtime work adds `run`, `publish-wpf`, and `run-wpf`. The wrapper hides container-specific details and returns the underlying failure status. A clean build in the container and CI, not the state of a developer's machine, is authoritative.

### 4. Test strategy locally

The normal local test loop has three levels:

1. Run the narrow project or category affected by the change.
2. Run the complete cross-platform test suite in Docker before considering a task complete.
3. Rely on Windows and Linux CI for the final platform matrix, including the WPF build and any Windows-only UI smoke tests.

The cross-platform container suite includes:

- NUnit unit tests for domain and application behavior.
- Architecture tests for dependency direction and platform boundaries.
- EF Core integration tests using the real SQLite provider and isolated temporary databases.
- Component tests using simulated or fixture-backed external systems.
- Reqnroll acceptance scenarios that are not tagged `@windows`.

Tests must be deterministic. They use an injected clock, deterministic identifiers where required, scripted LLM responses, simulated brokers, captured message dispatchers, and local fixtures. Network-dependent and credential-dependent tests are opt-in and never part of the default local or commit-gating suite.

File-backed SQLite fixtures have one asynchronous ownership boundary. Teardown first awaits hosted-service shutdown, then disposes scenario scopes, contexts, explicit connections, the service provider, and the host. Once those owners are closed, test infrastructure clears only the connection pool identified by that owned database's exact connection string and deletes the temporary directory once. An immediate deletion failure is a lifecycle defect; do not conceal it with sleeps, retries, garbage collection, ignored exceptions, or process-wide pool clearing.

The executable entry point owns the Generic Host returned by `HostBootstrap.Build` and asynchronously disposes it after `RunAsync` completes or fails. Host composition normalizes the database path once to an absolute path and builds one canonical SQLite connection string (`ReadWriteCreate`, shared cache, and pooling enabled); the connection interceptor applies the configured busy timeout. The EF registration, scoped repositories, hosted smoke workflow, and test inspection connection use that same identity. Tests that build the host directly use the same order: await shutdown, close inspection connections, asynchronously dispose the host/root provider, clear only the closed canonical `smoke.db` pool, then delete the directory on the first attempt. A failure reports the bounded path, pooling settings, and named ownership boundaries; it never reports credentials or an unbounded provider payload.

Stage 1 Reqnroll scenarios can be selected by tag through NUnit categories:

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage1"
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage1&TestCategory!=windows"
```

The second command is the deterministic cross-platform selection used when Windows-only scenarios are not applicable.

### 5. WPF build and execution boundary

The WPF client can be compiled and published without installing the .NET SDK or runtime on the Windows host.

The normal Linux SDK container restores the Windows desktop targeting packs and compiles `net10.0-windows` projects by setting `EnableWindowsTargeting=true`. For manual testing, it publishes a self-contained Windows artifact for an explicit runtime identifier such as `win-x64`. The publish output is written to an ignored, host-visible artifacts directory. A PowerShell wrapper launches that artifact on Windows.

This is the default desktop feedback loop:

1. Edit source on Windows.
2. Run the repository's WPF publish command, which builds inside the Linux container.
3. Launch the generated self-contained application on the Windows host.
4. Repeat after changes.

`./dev.ps1 publish-wpf` performs the locked `win-x64` self-contained publish inside the Linux SDK container and
writes only to the ignored `artifacts/wpf/win-x64` directory. The publish includes `wpf-test-profile.json`, a
machine-readable declaration that the automation profile has fixture Research, paper broker identity, and no network,
credential, or live-trading authority. `./dev.ps1 run-wpf` republishes, launches that executable without a host .NET
runtime, and gives the process a unique directory below `%LOCALAPPDATA%\LlmTrader\WpfTestRuns`. The launcher waits for
normal process exit, requires the redacted shutdown signal, and deletes that database, WAL, signals, and other runtime
artifacts on the first attempt.

The explicit WPF test profile migrates a fresh SQLite database, uses the fixed UTC fixture clock and stable smoke
identities, seeds two operator Bot/Portfolio journeys, fixture Research, and the simulated paper broker, and has no
configuration surface for credentials or network providers. It atomically writes bounded `ready.json` and
`shutdown.json` documents inside its isolated run directory. Startup failures expose only a bounded alphanumeric phase
and exception code. These signals are automation seams, not an external control or authorization channel.

The host process runs outside Docker, so it must use host-accessible local configuration and storage. Its development database must still remain outside the OneDrive source tree. The launcher should assign a per-developer data directory under a non-synchronized local application-data location and set safe simulated or research-only defaults.

A self-contained artifact is larger than a framework-dependent build, but it keeps the host free of a separately installed .NET runtime and makes the tested runtime version explicit. We can add a faster framework-dependent option later if developers choose to install the matching runtime.

Windows containers do not provide an interactive desktop suitable for launching, visually debugging, or automating the WPF GUI. FlaUI UIA3 tests require an interactive Windows host or Windows CI runner rather than an ordinary container.

The initial policy is therefore:

- Developers working on any part of the solution can compile it through Docker without a host .NET installation.
- The Linux development image performs the routine full-solution compile with Windows targeting enabled.
- The Linux development image produces a self-contained Windows WPF artifact for host-side manual testing.
- Windows CI performs the authoritative native WPF build on every applicable change.
- Windows UI tests run in an interactive Windows test environment when introduced.
- A developer who actively works on or debugs WPF may optionally install the .NET 10 SDK and an editor or Visual Studio workload on Windows. This is not a general project prerequisite.

Running a WPF artifact produced elsewhere is not a substitute for local WPF debugging. If desktop work becomes frequent, the team should revisit whether a small, documented Windows UI toolchain is justified.

## What Is Installed Where

### Required on the Windows host

| Dependency | Why it is required |
| --- | --- |
| Windows 10 or 11 | Host operating system and eventual WPF runtime |
| Docker Desktop with the WSL 2 backend | Builds and runs the Linux development environment |
| Git | Source control operations |
| A code editor | Editing repository files; C# language support is optional |

Docker Desktop must be configured for Linux containers. WSL 2 is used by Docker Desktop, but developers do not need to install or maintain a separate .NET toolchain inside a personal WSL distribution.

### Supplied by Docker or the repository

| Dependency | Source |
| --- | --- |
| .NET 10 SDK and runtime | Official Microsoft container images |
| NuGet CLI behavior | The .NET SDK |
| NUnit, Reqnroll, EF Core, SQLite provider, analyzers, and test SDK | Locked NuGet dependencies |
| Repository-scoped CLI tools | A committed .NET tool manifest |
| SQLite runtime and command-line diagnostics, if needed | Development container image |
| Build, test, and run entry points | Docker Compose and repository scripts |

Project tools must be declared by the repository. Documentation must not instruct developers to install mutable global .NET tools.

### Optional on the Windows host

| Dependency | Needed only for |
| --- | --- |
| .NET 10 SDK | Direct host builds, debugging, or WPF development |
| Visual Studio with the .NET desktop development workload | Full WPF designer/debugger workflow |
| C# editor extension | Host-side language services; it may use its own managed runtime |
| SQLite browser or CLI | Ad hoc inspection of an exported database copy |

These optional tools must not become an undeclared dependency of the Docker workflow or CI.

## Configuration and Secrets

Non-secret defaults belong in versioned application settings. Developer overrides and credentials do not.

- Commit a safe example showing required configuration keys.
- Ignore local environment files, secret files, databases, logs, and generated artifacts.
- Prefer environment variables or read-only secret files mounted into the container.
- Never bake credentials into an image or pass production secrets to tests.
- Keep paper and live broker configuration visibly and structurally separate.
- Make the default local execution mode simulated or research-only; never live trading.

The exact secret-provider strategy for Windows and Linux remains an architecture decision for the integration stages. Until then, no local workflow should require a real provider credential.

## Repository Support

The repository provides:

- A multi-stage `Dockerfile` with development/build/test and headless-runtime targets.
- A Docker Compose file with a default headless service and reusable build/test invocation.
- A `.dockerignore` that excludes build output, runtime data, secrets, and IDE state.
- A repository-root `dev.ps1` wrapper, including commands to publish WPF in Docker and launch it on Windows.
- An ignored, host-visible artifacts directory for the self-contained Windows publish output.
- Named volumes for NuGet cache and local application data.
- Readiness sequencing and graceful shutdown behavior for the headless service.
- Windows and Linux CI commands that call the same underlying restore, build, format, and test operations used locally.

`./dev.ps1 run` selects `Trading:SmokeMode=true`. The smoke host deletes only its dedicated `/data/smoke.db`, applies migrations, completes recovery before claiming work, and uses only scripted models, deterministic snapshots, approved Research fixtures, and the simulated paper broker. The output includes the Research and governance hashes plus an atomic Order, timeout-after-acceptance reconciliation, acknowledgement, exact 30- and 40-share Fills, a 70-share Position, 700 USD gross execution, 2 USD fees, consumed reservation, bounded audit count, zero live submissions, and recoverable shutdown. Repeated executions rebuild the dedicated database and reproduce the same business identities and outcomes.

## Definition of a Working Local Environment

The environment is ready when a developer with only the required host dependencies can clone the repository and, using documented repository commands:

1. Build the development image.
2. Restore locked dependencies.
3. Compile the full solution, including WPF, in Release mode with zero warnings.
4. Run the complete deterministic cross-platform test suite.
5. Start and stop the headless host cleanly.
6. Publish WPF as a self-contained Windows artifact and launch it on the host.
7. Persist local application data outside the source tree.

Windows CI must separately prove a native Windows build. Once UI automation is introduced, its smoke suite must run in an interactive Windows environment.
