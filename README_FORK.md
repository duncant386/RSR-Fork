# Duncan's Rotation Solver Reborn Fork

Fork of [RotationSolverReborn](https://github.com/FFXIV-CombatReborn/RotationSolverReborn)
(LGPL-3.0) adding **automatic mechanic handling** driven by BossModReborn's radar.

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

## What v2 adds

- **Gaze look-away** (`RotationSolver/Updaters/GazeHandler.cs`, toggle off by
  default): while BossModReborn reports active gaze/directional hazards
  (`Hints.ForbiddenDirectionsCount`, BMR >= 7.5.0.20), the plugin turns your
  character to face directly away from your hard target via the game client's
  `SetRotation` (rotation convention verified against BossMod's own actor model:
  0 = South, +90° = East). Actions are suppressed while turned, because the
  client's auto-face-target would otherwise snap you back toward the boss the
  moment you cast anything. Movement always wins: while you (or the dodger) are
  moving, the handler stands down since the client owns facing then.
  - Honest limits: the gaze source is *assumed* to be your hard target (true for
    typical boss gazes, not for gazes from adds). BMR's IPC exposes only the
    hazard count, not the forbidden arcs, so "look TOWARDS" (must-face-the-boss)
    mechanics are indistinguishable from look-away gazes — this handler always
    turns *away*, so disable the toggle for must-face fights.
- **Do-nothing / freeze mechanics**: when BossModReborn reports an imminent or
  active `Pyretic` or `NoMovement` special mode (acceleration bomb, "stay still"),
  the dodger stops all automated movement and holds still instead of dodging
  into death; actions are suppressed too (`MechanicDodger.IsFrozen`). During
  `Misdirection` the dodger stands down entirely (movement direction is
  scrambled, paths can't be trusted). `Freezing` ("keep moving") is left alone —
  dodging toward safety also satisfies it.
- **Arena-bounded dodge sampling**: when BMR exposes arena bounds
  (`Hints.ArenaCenter` / `Hints.ArenaRadius`), dodge candidates outside the
  arena are skipped.
- **New BMR IPC wired up**: `Hints.ForbiddenDirectionsCount`,
  `Hints.ArenaCenter`, `Hints.ArenaRadius`, `Hints.MaxCastTime` (all defensive —
  missing endpoints on older BMR just leave safe defaults). Debug window shows
  gaze count, special-mode state, arena bounds, and max cast time.

## Mechanics already covered upstream (no fork changes needed)

- Knockback → anti-knockback via `UseBmrTimeline` + `BMRKnockbackWindow`.
- Raidwide / tankbuster proactive mitigation via `UseBmrTimeline`.
- Interrupts via `InterruptTarget`.
- Cast cancelling via BMR `Hints.ForceCancelCast`, plus BMR telling RSR to stop
  casting when gaze/pyretic is imminent.
- Pyretic blocking auto-attacks and action use.

Not auto-handled (no reliable signal): stack/spread targeting (needs party
positions BMR doesn't expose), look-towards (indistinguishable from gaze via
IPC — see above).

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

## Testing the mechanics

1. In RSR config, enable **Dodge mechanics automatically (BossModReborn + vnavmesh)**
   and/or **Look away from gaze mechanics automatically (BossModReborn)**.
2. Open the RSR debug window and watch the BMR/mechanic lines.
3. Dodge test: go somewhere with obvious ground AoEs (any dungeon boss). Stand
   still in an AoE: the plugin should move you out and the debug line should read
   "Dodger active: True" while moving.
4. Freeze test: find a Pyretic / "stay still" mechanic. The debug line should read
   "Dodger frozen for do-nothing mechanic: True" and you should not move or act.
5. Gaze test: find a gaze mechanic with the boss targeted. Your character should
   turn away from the boss and "Looking away: True" should show with the boss name.
   Needs BossModReborn >= 7.5.0.20 for the gaze-hazard IPC endpoint.

## Building locally

```
dotnet build RotationSolver/RotationSolver.csproj -c Release
```
Needs .NET 10 SDK and Dalamud's dev assemblies (the CI downloads them from
`https://goatcorp.github.io/dalamud-distrib/latest.zip`). Output zip:
`RotationSolver/bin/Release/RotationSolverDuncan/latest.zip`.
