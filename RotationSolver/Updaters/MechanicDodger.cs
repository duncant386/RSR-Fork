using ECommons.GameHelpers;
using RotationSolver.IPC;

namespace RotationSolver.Updaters;

/// <summary>
/// Duncan's fork: automatic mechanic dodging driven by BossModReborn's radar data.
///
/// Each tick, asks BossModReborn whether the player's current position is safe.
/// When it isn't, finds the nearest safe spot (spiral sampling) and moves there
/// via vnavmesh. While actively dodging, <see cref="IsDodging"/> is true and
/// RSR suppresses actions (see <see cref="ActionUpdater.CanDoAction"/>).
///
/// Freeze mechanics ("do nothing"): when BossModReborn reports an imminent or
/// active <see cref="SpecialMode.Pyretic"/> or <see cref="SpecialMode.NoMovement"/>
/// (acceleration bomb / "stay still" mechanics), the dodger stops all automated
/// movement and <see cref="IsFrozen"/> is true, which also suppresses actions.
/// Moving during those mechanics kills you, so doing nothing is the mechanic.
/// <see cref="SpecialMode.Misdirection"/> likewise disables dodging because the
/// client alters movement direction and automated paths can't be trusted.
/// <see cref="SpecialMode.Freezing"/> ("keep moving") is left alone: dodging
/// toward safety also satisfies "be moving".
///
/// Safety rules:
/// - Only runs in combat with an active BossMod module, so it never moves you in town.
/// - Never fights your own movement: if you moved since the last tick and the
///   dodger isn't the one moving you, it backs off.
/// - If no safe spot is found, it does nothing (keeps DPS up rather than wandering).
/// </summary>
internal static class MechanicDodger
{
	/// <summary>
	/// True while the dodger is actively moving the player to safety.
	/// RSR suppresses all actions while this is true.
	/// </summary>
	public static bool IsDodging { get; private set; }

	/// <summary>
	/// True while a freeze-type mechanic (Pyretic / NoMovement) is imminent or
	/// active and the dodger is holding still on purpose. RSR suppresses all
	/// actions while this is true.
	/// </summary>
	public static bool IsFrozen { get; private set; }

	/// <summary>
	/// How far ahead of a freeze mechanic's activation we stop moving (seconds).
	/// Small lead time to absorb reaction/network latency.
	/// </summary>
	private const float FreezeLeadTime = 0.5f;

	private static Vector3? _dodgeTarget;
	private static Vector3 _lastPos;
	private static bool _hasLastPos;

	public static void Update()
	{
		IsDodging = false;
		IsFrozen = false;

		if (!Service.Config.DodgeMechanics) return;
		if (!DataCenter.IsActivated()) { Reset(); return; }
		if (!DataCenter.InCombat) { Reset(); return; }
		if (!DataCenter.BMREnabled || !DataCenter.BMRHasActiveModule) { Reset(); return; }

		// Freeze mechanics don't need vnavmesh: holding still is the whole point.
		// BMR's SpecialModeType is the *imminent* mode; SpecialModeIn is seconds
		// until it activates (<= 0 once active, until the mode resolves).
		var specialMode = DataCenter.BMRSpecialModeType;
		var specialIn = DataCenter.BMRSpecialModeIn;
		if ((specialMode == SpecialMode.Pyretic || specialMode == SpecialMode.NoMovement)
			&& specialIn <= FreezeLeadTime)
		{
			StopMovement();
			IsFrozen = true;
			return;
		}

		// Misdirection: the client scrambles movement direction, so any automated
		// path is untrustworthy. Stand down entirely while it is imminent/active.
		if (specialMode == SpecialMode.Misdirection && specialIn <= FreezeLeadTime)
		{
			StopMovement();
			Reset();
			return;
		}

		if (DataCenter.BMRIsPositionSafe == null) { Reset(); return; }
		if (!VNavmesh_IPCSubscriber.IsEnabled) { Reset(); return; }
		if (!(VNavmesh_IPCSubscriber.NavIsReady?.Invoke() ?? false)) { Reset(); return; }

		var player = Player.Object;
		if (player == null || player.CurrentHp == 0) { Reset(); return; }

		var pos = player.Position;

		// Don't fight the player's own movement. If they moved since last tick
		// and we aren't the ones moving them, assume they're handling it.
		if (_hasLastPos && !_dodgeTarget.HasValue && Vector3.Distance(pos, _lastPos) > 0.05f)
		{
			_lastPos = pos;
			return;
		}
		_lastPos = pos;
		_hasLastPos = true;

		bool safe;
		try
		{
			safe = DataCenter.BMRIsPositionSafe(pos);
		}
		catch
		{
			Reset();
			return;
		}

		if (safe)
		{
			if (_dodgeTarget.HasValue)
			{
				try { VNavmesh_IPCSubscriber.PathStop?.Invoke(); } catch { }
				_dodgeTarget = null;
			}
			return;
		}

		var target = FindSafeSpot(pos);
		if (target == null)
		{
			_dodgeTarget = null;
			return;
		}

		// Only (re)issue the move when the target changed or the path died.
		bool pathRunning = false;
		try { pathRunning = VNavmesh_IPCSubscriber.PathIsRunning?.Invoke() ?? false; } catch { }

		if (!_dodgeTarget.HasValue
			|| Vector3.Distance(_dodgeTarget.Value, target.Value) > 0.5f
			|| !pathRunning)
		{
			bool started = false;
			try { started = VNavmesh_IPCSubscriber.PathfindAndMoveTo?.Invoke(target.Value, false) ?? false; } catch { }
			if (started)
			{
				_dodgeTarget = target;
			}
		}

		IsDodging = _dodgeTarget.HasValue;
	}

	/// <summary>
	/// Spiral sampling around the player: rings at increasing radius, denser
	/// further out. Returns the nearest position BossMod considers safe.
	/// When BMR exposes arena bounds, candidates outside the arena are skipped
	/// so the dodger never paths into walls or off the platform.
	/// </summary>
	private static Vector3? FindSafeSpot(Vector3 from)
	{
		foreach (var radius in new[] { 3f, 5f, 8f, 12f, 16f, 22f })
		{
			var steps = Math.Max(8, (int)(radius * 2));
			for (var i = 0; i < steps; i++)
			{
				var angle = (float)(i * 2 * Math.PI / steps);
				var candidate = from + new Vector3(
					(float)Math.Cos(angle) * radius, 0,
					(float)Math.Sin(angle) * radius);
				if (DataCenter.BMRHasArenaBounds && !IsInsideArena(candidate))
				{
					continue;
				}
				if (DataCenter.IsMovementDestinationSafe(candidate))
				{
					return candidate;
				}
			}
		}
		return null;
	}

	private static bool IsInsideArena(Vector3 candidate)
	{
		var dx = candidate.X - DataCenter.BMRArenaCenter.X;
		var dz = candidate.Z - DataCenter.BMRArenaCenter.Y;
		// Small margin so we don't hug the exact arena edge.
		var r = DataCenter.BMRArenaRadius - 1f;
		return dx * dx + dz * dz <= r * r;
	}

	/// <summary>
	/// Stops any vnavmesh movement the dodger started. Safe to call when idle.
	/// </summary>
	private static void StopMovement()
	{
		try { VNavmesh_IPCSubscriber.PathStop?.Invoke(); } catch { }
		_dodgeTarget = null;
	}

	private static void Reset()
	{
		_dodgeTarget = null;
		_hasLastPos = false;
	}
}
