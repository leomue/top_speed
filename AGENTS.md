# Repository Guidelines

## Project Structure & Module Organization
`top_speed_net/` contains the main solution (`TopSpeed.sln`). Core projects are:
- `TopSpeed/`: client game (`net472`), menus, input, race logic, and multiplayer client flow.
- `TopSpeed.Server/`: dedicated server runtime (`net8.0`).
- `TopSpeed.Shared/`: shared protocol, physics/data contracts, and constants.
- `TS.Audio/`, `MiniAudioExNET/`, `SteamAudio.NET/`: audio engine and wrappers.

Game data/assets live in:
- `top_speed_net/Sounds/`, `top_speed_net/Tracks/`, `top_speed_net/Vehicles/`.

Supporting material:
- `.github/workflows/dev-build.yml` (CI build + dev release packaging)
- `docs/` (project documentation)

## Build, Test, and Development Commands
From repository root:
- `dotnet restore top_speed_net/TopSpeed.sln` — restore all dependencies.
- `dotnet build top_speed_net/TopSpeed.sln -c Debug` — build client/server/shared projects.
- `dotnet build top_speed_net/TopSpeed.sln -c Release` — production-style build.
- `dotnet publish top_speed_net/TopSpeed.Server/TopSpeed.Server.csproj -c Release -r win-x64 --self-contained true` — publish server binary.

Note: there is currently no active test project in `TopSpeed.sln`; validation is primarily build + runtime smoke testing.

## Coding Style & Naming Conventions
- Language: C#; use 4-space indentation and file-scoped responsibility.
- Naming: `PascalCase` for types/methods/properties, `camelCase` for locals/parameters, `_fieldName` for private fields.
- Keep files/folders organized by responsibility (input, race, network, menu, settings) and avoid mixed-purpose "god" files.
- Prefer small, focused partial classes where already used.

## Testing Guidelines
- Minimum gate: `dotnet build ... -c Debug` must pass with 0 errors.
- For gameplay/network changes, smoke test in runtime:
  - menu navigation + screen reader output,
  - race loop behavior,
  - client/server connect, room join/leave, ping/pong sounds.
- If adding tests, place them in a dedicated test project and include clear arrange/act/assert naming.

## Commit & Pull Request Guidelines
- Follow existing history style: imperative, scoped subjects (e.g., `Fix ...`, `Add ...`, `Refactor ...`).
- Keep commits focused to one concern; include user-impact in commit body when behavior changes.
- PRs should include:
  - concise summary,
  - why the change is needed,
  - verification steps (commands + runtime checks),
  - screenshots/log snippets only when relevant (UI/audio/network behavior).

## Security & Configuration Notes
- Do not commit secrets, tokens, or local machine paths.
- Keep custom content inside approved asset folders; avoid introducing file access paths outside project roots.
