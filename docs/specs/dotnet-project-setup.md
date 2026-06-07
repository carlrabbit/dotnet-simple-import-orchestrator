# Spec: .NET project setup

## Purpose

This spec defines the .NET repository setup and test expectations for `dotnet-simple-import-orchestrator` after the revised polling and import definition rewrite.

The goal remains a small, buildable .NET 10 library with TUnit tests running on Microsoft Testing Platform. This replacement spec updates the bootstrap-era expectations that referenced the old import definition model.

## Solution

Preferred solution file:

```text
DotnetSimpleImportOrchestrator.slnx
```

Fallback:

```text
DotnetSimpleImportOrchestrator.sln
```

Use `.sln` only if the local .NET SDK/tooling does not support `.slnx`. If fallback is used, mention it in the implementation summary and make `README.md` reflect the actual file.

## Projects

Expected projects:

```text
src/DotnetSimpleImportOrchestrator/DotnetSimpleImportOrchestrator.csproj
tests/DotnetSimpleImportOrchestrator.Tests/DotnetSimpleImportOrchestrator.Tests.csproj
```

Both projects must target:

```xml
<TargetFramework>net10.0</TargetFramework>
```

## Library project requirements

The library project should be minimal and dependency-light.

Required properties:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

Recommended properties:

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<IsPackable>true</IsPackable>
```

Do not add third-party dependencies for CSV, XML, HTTP, scheduling, plugin discovery, or persistence in this task.

Use BCL APIs and `System.Text.Json`.

## Test project requirements

The test project must:

- target `net10.0`;
- reference the library project;
- use Microsoft Testing Platform;
- use TUnit;
- enable nullable reference types;
- enable implicit usings.

Package versions should be current for the environment used by the implementation agent. Prefer stable package versions if stable packages support `net10.0`; otherwise use the minimum preview versions needed by the installed SDK and document that choice in the implementation summary.

Recommended packages, adjusted to available current versions:

```xml
<PackageReference Include="TUnit" Version="..." />
<PackageReference Include="Microsoft.Testing.Platform" Version="..." />
```

If the TUnit package already brings the needed Microsoft Testing Platform integration transitively, avoid unnecessary package references and document the package set actually used.

## Directory conventions

Use this layout:

```text
src/
  DotnetSimpleImportOrchestrator/
tests/
  DotnetSimpleImportOrchestrator.Tests/
docs/
  architecture/
  specs/
  tasks/
```

Do not create user-facing documentation folders in this task.

## Build and test commands

The implementation must support:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Before finishing, attempt:

```bash
dotnet format --verify-no-changes
```

If format verification is unavailable in the environment, report that explicitly.

## Revised test expectations

Create or rewrite enough TUnit tests to validate the revised architecture.

Minimum tests:

1. `ImportDefinition<TConfiguration>` exposes ID, priority, polling interval, and strongly typed configuration.
2. `ImportDefinition<TConfiguration>` can be used through `IImportDefinition` in a mixed list.
3. Runtime state JSON round-trip preserves import state and cursor JSON.
4. Duplicate import IDs in a single runner call are rejected.
5. Empty import IDs are rejected.
6. Missing/null configuration is rejected.
7. Non-positive polling intervals are rejected.
8. Missing priority uses `ImportPriorities.Normal`.
9. Not-due imports are not polled.
10. Due imports are checked in deterministic order: priority ascending, then ID ordinal ascending.
11. A no-candidate result records a check and continues to the next due import.
12. A source failure records a failure and continues to the next due import.
13. A handler failure records a failure and continues to the next due import.
14. A successful handler result updates state and stops the runner pass.
15. One runner pass performs at most one successful import.
16. Removing an import from the supplied list does not automatically remove existing runtime state.
17. The runner snapshots the import list at the start of the call.

Tests that are no longer valid and must be removed or rewritten:

- JSON round-trip tests for the old non-generic `ImportDefinition` with source JSON.
- Tests for `Enabled = false` behavior.
- Tests that expect core `ImportPayloadFormat` on definitions or candidates.
- Tests that expect source/handler resolution by names stored on `ImportDefinition`.

## Acceptance criteria

- The solution contains the library and test projects.
- `dotnet restore` succeeds.
- `dotnet build --no-restore` succeeds.
- `dotnet test --no-build` or `dotnet test` succeeds.
- The test project uses TUnit and Microsoft Testing Platform.
- Tests validate the revised import definition and bounded polling model.
- The implementation summary reports package versions and validation results.
