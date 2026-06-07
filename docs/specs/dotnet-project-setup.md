# Spec: .NET project setup

## Purpose

This spec defines the initial .NET repository setup for `dotnet-simple-import-orchestrator`.

The goal is a small, buildable .NET 10 library with TUnit tests running on Microsoft Testing Platform.

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

Create these projects:

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

Do not add third-party dependencies for CSV, XML, HTTP, scheduling, or persistence in this task.

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

Use this initial layout:

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

## Initial test expectations

Create enough TUnit tests to validate that the repository setup and initial architecture work.

Minimum tests:

1. `ImportDefinition` JSON round-trip preserves key fields.
2. `ImportRuntimeState` JSON round-trip preserves cursor JSON.
3. A minimal import run can pass a stream from a file-backed source to a handler.
4. Disabled imports are skipped.

## Acceptance criteria

- The solution contains the library and test projects.
- `dotnet restore` succeeds.
- `dotnet build --no-restore` succeeds.
- `dotnet test --no-build` or `dotnet test` succeeds.
- The test project uses TUnit and Microsoft Testing Platform.
- The implementation summary reports package versions and validation results.
