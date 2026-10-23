# AGENTS.md

Guidance for AI coding agents working in this repository.

## Commands

```sh
dotnet restore --locked-mode                              # restore; fails if a lock file is stale
dotnet build -c Release --no-restore                      # build; warnings are errors
dotnet test -c Release --no-build                         # run tests
dotnet format whitespace --no-restore                     # apply formatting
sh scripts/development/check.sh                           # everything CI checks
dotnet publish src/*/*.csproj -c Release -r <rid> --self-contained   # NativeAOT publish
```

Run `scripts/development/check.sh` before declaring work finished. After changing
package references, run a plain `dotnet restore` to regenerate `packages.lock.json`
and commit the result.

## Layout

- `src/` is the NativeAOT console app; `tests/` is the xUnit v3 test project.
- All build settings live in `Directory.Build.props`; package versions live in
  `Directory.Packages.props`. A `.csproj` should stay nearly empty.
- `BannedSymbols.txt` lists banned APIs; `.editorconfig` sets analyzer severities.

## Rules

- **Never weaken the build.** Do not set `TreatWarningsAsErrors=false`, turn off
  nullable, disable the AOT/trim analyzers, remove lock files, or lower a severity in
  `.editorconfig` to get a build through. `Directory.Build.targets` fails the build
  (STRICT0001-0004) if you try. Fix the code instead.
- **No `#pragma warning disable` or `[SuppressMessage]`** unless there is no
  alternative; include a comment naming the reason. This applies to banned APIs
  (`RS0030`) too.
- **NativeAOT:** no reflection-based serialization (use a `JsonSerializerContext`),
  no `Activator.CreateInstance`, no runtime code generation.
- **Async:** await tasks; never `.Wait()`, `.Result` or `Thread.Sleep`. Use
  `.ConfigureAwait(false)` on library awaits (CA2007).
- **Time:** do not use `DateTime.Now`/`UtcNow` or `DateTimeOffset.Now`/`UtcNow`;
  inject a clock abstraction.
- **Hashing:** no MD5 or SHA1.
- **Style:** StyleCop is enforced. Public and internal members need XML docs;
  `using` directives go outside the namespace, `System` first; files end with a newline.
  Namespaces and type names are PascalCase.
- **Dependencies:** add a `PackageVersion` to `Directory.Packages.props` and a
  versionless `PackageReference` in the project. Prefer no new dependency.
- **Tests:** add or update a test for behavior you change. Test classes must be
  `public` (xUnit cannot discover internal ones).
