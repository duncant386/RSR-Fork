# Duncan's Rotation Solver Reborn Fork

Fork of [RotationSolverReborn](https://github.com/FFXIV-CombatReborn/RotationSolverReborn)
(LGPL-3.0) adding **automatic mechanic dodging** driven by BossModReborn's radar.

## What the fork adds (v1)

- **Mechanic dodging** (`RotationSolver/Updaters/MechanicDodger.cs`): every tick,
  asks BossModReborn (`Hints.IsPositionSafe`) whether your position is safe. If not,
  spiral-samples nearby spots for a safe one and moves there via vnavmesh
  (`SimpleMove.PathfindAndMoveTo`).
- **Action pause while dodging**: `ActionUpdater.CanDoAction()` returns false while
  the dodger is actively moving you, so RSR stops firing actions mid-dodge.
- **Config toggle**: "Dodge mechanics automatically (BossModReborn + vnavmesh)"
  (off by default).
- **Safety rules**: only in combat with an active BossMod module; never fights your
  own movement (if you moved since last tick, it backs off); if no safe spot is
  found it does nothing instead of wandering.
- **Debug**: the RSR debug window shows dodger status (enabled / actively dodging /
  vnavmesh IPC state).

Not yet implemented: gaze/look-away mechanics (no clean facing API — research spike
needed), "stop actions" mechanics beyond dodge-pause.

## Fork identity

- `InternalName`: `RotationSolverDuncan` (distinct from upstream, so both can be
  listed; **disable upstream RSR while testing** — both register `/rotation` and
  `/rsr` chat commands and will conflict).
- IPC provider prefix is still `RotationSolverReborn`, so external plugins
  (AutoDuty etc.) keep working with the fork.

## Publishing to GitHub (one-time setup)

1. Create a new **public** GitHub repo (e.g. `rotationsolver-duncan`).
2. Push this code:
   ```
   git init
   git add .
   git commit -m "Duncan's RSR fork with mechanic dodging"
   git branch -M main
   git remote add origin https://github.com/<you>/<repo>.git
   git push -u origin main
   ```
3. In `repo.json`, replace `GITHUB_USER` / `GITHUB_REPO` with your GitHub username
   and repo name, commit, and push.
4. Cut a release: tag it `1.0.0.0` (four-part version, e.g. `git tag 1.0.0.0 &&
   git push origin 1.0.0.0`). The `Publish` workflow builds the plugin and
   attaches `latest.zip` to the release. (`repo.json` points at
   `/releases/latest/download/latest.zip`, so it never needs updating.)
5. In-game: `/xlsettings` → Experimental → Custom Plugin Repositories → add
   `https://raw.githubusercontent.com/<you>/<repo>/main/repo.json` → Save.
6. `/xlplugins` → install **Rotation Solver Reborn (Duncan's Fork)**.
7. Also install **BossModReborn**, **vnavmesh**, and **Avarice** (RSR dependency)
   if you don't have them.

## Testing the dodger

1. In RSR config, enable **Dodge mechanics automatically (BossModReborn + vnavmesh)**.
2. Open the RSR debug window and watch the dodger lines.
3. Go somewhere with obvious ground AoEs (any dungeon boss). Stand still in an AoE:
   the plugin should move you out and the debug line should read
   "actively dodging: True" while moving.

## Building locally

```
dotnet build RotationSolver/RotationSolver.csproj -c Release
```
Needs .NET 10 SDK and Dalamud's dev assemblies (the CI downloads them from
`https://goatcorp.github.io/dalamud-distrib/latest.zip`). Output zip:
`RotationSolver/bin/Release/RotationSolverDuncan/latest.zip`.
