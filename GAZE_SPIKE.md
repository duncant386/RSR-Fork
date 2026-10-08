# Gaze / Look-Away Research Spike

**Date:** 2026-10-08 · **Status:** complete — findings below
**Question:** is there a clean way to turn the character for gaze mechanics from the fork?

**Answer: yes — and BossModReborn already does it for you by default.**

## 1. The clean facing primitive exists (no sig-scans, no key emulation)

BossModReborn turns the character via `ActionManagerEx.FaceDirection` — a ~10-line method
portable to the fork, since the fork already references FFXIVClientStructs:

```csharp
public void FaceDirection(Angle direction)
{
    var player = (Character*)GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
    if (player != null)
    {
        var position = (Vector3)player->Position + direction.ToDirection().ToVec3();
        _inst->AutoFaceTargetPosition(&position);              // game's own auto-face path
        var pm = (PlayerMove*)player;
        pm->Move.Interpolation.DesiredRotation = direction.Rad; // don't let interpolation rotate us back
    }
}
```

Source: `/home/hatch/workspace/.cache/bmr-src/BossmodReborn-main/BossMod/Framework/ActionManagerEx.cs`
(method at line ~169). `AutoFaceTargetPosition` is confirmed present in the fork's vendored
FFXIVClientStructs (`~/.xlcore/dalamud/Hooks/dev/FFXIVClientStructs.dll`). This is the same
code path as the in-game "auto face target on action" option — server-accepted, no input hooks.

## 2. Detection: BMR computes gaze geometry internally, but IPC only exposes the count

- BMR module components (`GenericGaze`, source `BossMod/Components/Gaze.cs`) populate
  `AIHints.ForbiddenDirections` = list of `(center Angle, halfWidth Angle, activation DateTime)`.
- Over IPC, BMR exposes only **`Hints.ForbiddenDirectionsCount`** (the count, nothing else)
  — confirmed by enumerating all `Register(...)` names in `BossMod/Framework/IPCProvider.cs`.
  No per-direction center/halfWidth/activation is reachable from another plugin today.
- Without geometry, the fork cannot time the turn (BMR turns at the last ~0.5s) or pick the
  widest safe arc. A count-only fallback ("face away from boss while count > 0") would face
  away for the whole cast and tank uptime. Not recommended.

## 3. Recommendation: BMR already handles gazes — nothing to build in the fork

`BossMod/ActionTweaks/SmartRotationTweak.cs` ("Smart character orientation" → "Automatically
avoid gazes") is **on by default** and runs every frame in `UpdateDetour`:

- When a gaze activates within 0.5s (`MinTimeToAvoid`, configurable) and the player's current
  facing would be hit, it calls `FaceDirection(safeRotation)` — picking the widest safe arc,
  preferring the facing cone that keeps the target hittable (uptime preserved).
- If no safe facing exists (unavoidable gaze), it blocks casts/attacks during the window.
- Also handles inverted gazes ("Face the eye!") via the `inverted` flag in `GenericGaze`.

It works with the RSR fork installed, no fork code needed. Duncan should confirm BMR's
Action Tweaks → "Smart character orientation" is enabled (defaults are fine).

## 4. Interaction caveat for the fork's MechanicDodger

Movement re-faces the character along the movement direction. If the fork dodges via
vnavmesh during a gaze resolve window, it can undo BMR's look-away. Gazes don't make the
position unsafe, so the current v1 dodger rarely conflicts — but when in-fork gaze support
is added, add a rule: don't start a dodge move while a gaze is resolving (or re-face after).

## 5. If Duncan wants it in-fork anyway (later)

Needs gaze geometry from BMR. Options, in order of preference:
1. Upstream PR to BMR exposing e.g. `Hints.ForbiddenDirections(index)` returning the
   (center, halfWidth, activation) triple — small, benefits everyone.
2. Fork's own `GenericGaze`-style detection per module — huge, don't.
Then port `FaceDirection` + BMR's `GetSafeRotation` widest-arc logic (both in the files above).

---
*Researched against BossModReborn `main` @ 2026-10-08 (FFXIV-CombatReborn/BossmodReborn).*
*40k thread untouched — still parked per Duncan's request.*
