# AGENTS.md

Stardrop — a cross-platform mod manager for Stardew Valley, built with Avalonia 0.10.22 on `net8.0`.
One project: `Stardrop/Stardrop.sln`. **There are no tests** — a clean build is the only automated check.

## Commands

```powershell
dotnet build Stardrop/Stardrop.sln -c Debug          # the verification step; must be 0 errors
dotnet run --project Stardrop                        # launch the app
dotnet publish Stardrop -c Release -r win-x64 --self-contained -o out/win-x64
```

- Requires only the **.NET 8 SDK** (no VS, no workloads). `PublishSingleFile=true` is set in the csproj, so publishes need an explicit `-r <rid> --self-contained`.
- Release matrix (`package.yml`): `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.
- The build already emits ~225 warnings (CS86xx nullable, CA1416 for Windows-only `NXMProtocol` calls, NETSDK1206 for Avalonia Angle RIDs). They are pre-existing — don't chase them, but keep new code nullable-clean (`<Nullable>enable</Nullable>`).

## Where things live

- `Stardrop/Program.cs` — entrypoint. Sets the CWD to the exe folder (relative paths resolve to the build output), enforces a single instance, forwards `--nxm <url>` to the running instance via a bridge file, loads settings + translations, then starts Avalonia. Global state is static: `Program.helper`, `Program.settings`, `Program.translation`.
- `Stardrop/App.axaml.cs` — loads every theme from `Themes/` at runtime; handles URL-open (NXM) events.
- `Views/MainWindow.axaml.cs` **and** `Views/MainWindowCollections.cs` are one partial `MainWindow` holding most app behavior (~5.6k lines). Only some secondary windows use `ViewModels/` — do not assume strict MVVM.
- `Models/` — `Mod`, `Profile`, `Settings` plus `Data/`, `Nexus/`, `SMAPI/` subnamespaces.
- `Utilities/` — `Pathing` (every on-disk location), `Helper` (logging), `Translation`, `NXMProtocol`, `Internal/ThemeManager`, `Extension/TranslateExtension`, `External/NexusClient*`.
- `Themes/*.xaml` and `i18n/*.json` are `Content` copied next to the exe and read from disk at runtime, not embedded resources.

## UI conventions

- Never hardcode UI text. Declare `xmlns:i18n="clr-namespace:Stardrop.Utilities.Extension"`, use `Text="{i18n:Translate ui.some.key}"`, and add the key to `i18n/default.json` (English is the source of truth; other languages fall back to it). A missing key renders literally as `(No translation provided for key X)`.
- Languages are auto-discovered from `i18n/*.json` where the file name is the culture code (`de.json`, `pt-BR.json`); adding one needs no code change.
- Use `{DynamicResource ...}` theme brushes, never literal colors. Any new brush key belongs in `Themes/Defaults.xaml`, which backfills keys a theme omits; the active theme occupies index 0 of `Application.Styles`.
- New themes just need to be a file under `Themes/` or `Themes/Contributors/<name>/` — the `Themes\**` Content glob picks it up, no csproj edit. Theme XAML is parsed at runtime, so a broken theme fails only at startup (logged as a warning, theme skipped), never at build time.
- JSON is read with `AllowTrailingCommas = true`; `i18n/*.json` additionally allows `//` comments. Preserve that when editing.

## Runtime state — careful when testing

- All state lives in `%APPDATA%\Stardrop\Data` (`Settings.json`, `Profiles/`, `Cache/`, `Logs/`, `Nexus/`, `Selected Mods/`, `Collections/`). Running the app reads and rewrites the real user profile, and startup **deletes everything inside the `Nexus` folder**.
- Top-level exceptions are caught and only logged to `%APPDATA%\Stardrop\Data\Logs\log.txt`, so a silently non-working feature usually means "check the log".
- Launching a second instance exits early; it only forwards `--nxm` links to the first one.

## Workflow

- Default branch is `development`; PRs target it. `stable` only accepts PRs from `development` (`pr-target-action.yml` enforces this).
- Version is the `<Version>` in `Stardrop/Stardrop.csproj`; the app derives its display version from `AssemblyInformationalVersion` (git SHA suffix stripped). Releases are cut by pushing a `v*.*.*` tag (`-beta` → prerelease); `nightly` is a rolling prerelease rebuilt daily from the latest commit.
