# ShuttleLab — Godot 4.5 Port

3D badminton game for Android, ported from Unity 6 to **Godot 4.5** (GDScript).
Package `com.shuttlelab.badminton`, version `0.1.0`. The Unity project lives
in the parent folder; this `godot/` folder is the complete, playable game.

## Play

- **Main menu** → pick a difficulty (Beginner → Legend) → **Quick Match**
  (Đấu nhanh), Practice (Luyện tập), Career (Sự nghiệp), Tournament (Giải đấu).
- Touch: left virtual joystick to move, shot buttons to hit (swipe to aim),
  Serve button / Space to serve. Keyboard: WASD + J/K/L/U/I/O shots.
- BWF rules: 21 points, win by 2, cap 30, best-of-3 games.

## Build (cloud — recommended)

GitHub Actions builds the APK for free, no Unity license or quota needed.
Requires `.github/workflows/godot-android.yml` in the repo root (see the
repo root for the file). Push to `main` (or Run workflow manually) and
download the APK from the run's **Artifacts**.

## Build (local)

```sh
cd godot
godot --headless --import
godot --headless --export-debug "Android" ../ShuttleLab-Android-v0.1.0.apk
```

Needs Godot 4.5 stable + export templates, Android SDK (API 35,
build-tools 35.0.0), JDK 17. Debug export signs with the debug keystore.

## Structure

| Folder | What it holds |
|---|---|
| `main.gd` / `main.tscn` | Scene root: arena + camera + shuttle + menu/match flow |
| `scripts/core/` | CourtDimensions (BWF geometry) |
| `scripts/gameplay/` | ShotData (15 shot profiles), ShotSolver, ShuttleAerodynamics (drag + RK2 240 Hz), Shuttlecock, ShotController, RacketController |
| `scripts/match/` | MatchRules (BWF scoring), MatchManager, GameBootstrap, GameMode, GameState, Scoreboard |
| `scripts/ai/` | AIDifficulty (6 levels), AIPerception, AIShotSelection, OpponentAI |
| `scripts/players/` | PlayerController, PlayerMovement, PlayerStamina, FootworkController, PlayerVisual (procedural), PlayerAnimation, AraCharacterRig |
| `scripts/input/` | MobileInputManager, VirtualJoystick, ShotButton |
| `scripts/camera/` | CameraManager (6 presets) |
| `scripts/arena/` | ArenaBuilder (v0.2 TV arena), CrowdBuilder (1008 spectators), BroadcastHud |
| `scripts/graphics/` | GraphicsQuality (4 presets, 30–120 fps, render scale), FpsCounter |
| `scripts/progression/` | Career, Economy, WeeklyMissions, EquipmentManager (33 items), CharacterRoster (6), TournamentManager, TrainingManager |
| `scripts/ui/` | MainMenu, HudManager, SettingsPanel, ResultsScreen, CareerUI, GameSettings, UITheme |
| `scripts/audio/` | AudioManager (all 8 SFX synthesised in code + crowd ambience) |
| `models/` | Ara 3D Gen characters (see below) |

## Characters

Player models are realistic humans from **Ara 3D Gen** (free, MIT-licensed,
built on CC0 MakeHuman data): `models/Athletic_man.glb` (user) and
`models/Athletic_woman.glb` (AI opponent). Both are rigged (Mixamo-compatible,
69 bones) with Idle/Walk/Run/Jump/Sword slash/Throw grenade clips.
`scripts/players/ara_character_rig.gd` loads them and falls back to the
procedural humanoid if a GLB is missing, so the game builds and runs with
or without the model files.

Note: the procedural pose clips (Ready/Smash/Run) are **not** retargeted
onto the Mixamo skeleton — their rest poses differ (A-pose vs arms-down),
so applying them misplaces the arm and racket. The rig therefore plays
the GLB's own Idle clip and only tracks joint rotations in proxies.
Retargeting the swing poses onto the Mixamo bones is the next visual step.

## Physics / gameplay notes

- Shuttle drag is quadratic (`F = -0.5·rho·Cd·A·|v|·v`), integrated with
  RK2 at 240 Hz. The solver is a shooting method (ballistic guess +
  drag-aware correction, 12 iterations, 0.15 m convergence).
- **ServeLow and the flat shots were broken in the Unity original**: their
  short flight time + low apex forced a ~36 m/s launch, and drag killed
  the vertical arc, so every serve/drive clipped the net. Fixed in this
  port by lengthening the flight time and raising the apex (ServeLow
  1.20 s / 3.5 m; Drive/Defensive Drive/Push/Block/Net Shot 0.90 s /
  3.5 m). Verified with a solver probe: every shot clears the net
  (height at z=0 above 1.60 m) and lands within 0.3 m of its target.
  Net Kill is only valid from a high contact near the net.

## Graphics

v0.2 TV-broadcast direction: dark arena, 6 spotlights (5600 K, 1 casting
shadows), 3-tier stands, 1008 spectators (9 draw calls via MultiMesh),
LED boards, blue TV court, broadcast HUD (top scoreboard, bottom stamina
bars). Graphics presets: Tiết kiệm / Cân bằng / Cao / Tối đa, FPS
30/60/90/120, render scale 0.6–1.0, auto-detect by device.
Brand names in-game are fictional (SHUTTLELAB) — no real tournament or
sponsor brands are used.
