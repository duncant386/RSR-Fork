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

	private static Vector3? _dodgeTarget;
	private static Vector3 _lastPos;
	private static bool _hasLastPos;

	public static void Update()
	{
		IsDodging = false;

		if (!Service.Config.DodgeMechanics) return;
		if (!DataCenter.IsActivated()) { Reset(); return; }
		if (!DataCenter.InCombat) { Reset(); return; }
		if (!DataCenter.BMREnabled || !DataCenter.BMRHasActiveModule) { Reset(); return; }
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
				if (DataCenter.IsMovementDestinationSafe(candidate))
				{
					return candidate;
				}
			}
		}
		return null;
	}

	private static void Reset()
	{
		_dodgeTarget = null;
		_hasLastPos = false;
	}
}
