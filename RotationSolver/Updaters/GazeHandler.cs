using ECommons.DalamudServices;
using ECommons.GameHelpers;
using RotationSolver.Helpers;

namespace RotationSolver.Updaters;

/// <summary>
/// Duncan's fork: automatic look-away for gaze mechanics.
///
/// Trigger: BossModReborn reports active gaze/directional hazards through
/// <c>Hints.ForbiddenDirectionsCount</c> (BMR >= 7.5.0.20, polled into
/// <see cref="DataCenter.BMRForbiddenDirectionsCount"/>).
///
/// Response: turn the character to face directly away from the current hard
/// target via <see cref="FacingControl"/>, re-applied every tick while the
/// gaze is active. While <see cref="IsLookingAway"/> is true, RSR suppresses
/// all actions (see <see cref="ActionUpdater.CanDoAction"/>) because the
/// client's auto-face-target-on-action would otherwise snap the character
/// back toward the boss the moment anything is cast.
///
/// Heuristic and limits (be honest about them):
/// - The gaze source is assumed to be the current hard target. That holds for
///   the common case (boss casts gaze while targeted) but not for gazes from
///   adds or arena objects while another target is selected.
/// - BMR's IPC exposes only the hazard count, not the forbidden arcs, so a
///   "look TOWARDS" mechanic (face the boss or else) is indistinguishable
///   from a look-away gaze. This handler always turns away from the target;
///   disable the toggle for fights with must-face mechanics.
/// - Movement wins: while the player is moving (manually or via the dodger)
///   the client controls facing, so the handler stands down.
/// </summary>
internal static class GazeHandler
{
	/// <summary>
	/// True while the handler is actively holding the character facing away
	/// from the gaze source. RSR suppresses all actions while this is true.
	/// </summary>
	public static bool IsLookingAway { get; private set; }

	/// <summary>Last gaze source position used, for the debug window.</summary>
	public static string GazeSourceName { get; private set; } = string.Empty;

	private static Vector3 _lastPos;
	private static bool _hasLastPos;

	public static void Update()
	{
		IsLookingAway = false;
		GazeSourceName = string.Empty;

		if (!Service.Config.GazeLookAway) return;
		if (!DataCenter.IsActivated()) { Reset(); return; }
		if (!DataCenter.InCombat) { Reset(); return; }
		if (!DataCenter.BMREnabled || !DataCenter.BMRHasActiveModule) { Reset(); return; }
		if (DataCenter.BMRForbiddenDirectionsCount <= 0) { Reset(); return; }

		var player = Player.Object;
		if (player == null || player.CurrentHp == 0) { Reset(); return; }

		var pos = player.Position;

		// While anyone is moving the player, the client owns facing.
		// The dodger also takes precedence: escaping a lethal AoE matters more than the gaze.
		if (MechanicDodger.IsDodging)
		{
			_lastPos = pos;
			_hasLastPos = true;
			return;
		}

		if (_hasLastPos && Vector3.Distance(pos, _lastPos) > 0.05f)
		{
			_lastPos = pos;
			return;
		}
		_lastPos = pos;
		_hasLastPos = true;

		// Heuristic: the gaze comes from the current hard target.
		var target = Svc.Targets.Target;
		if (target == null || !target.IsEnemy())
		{
			return;
		}

		var away = FacingControl.AngleToFaceAway(pos, target.Position);
		FacingControl.SetFacing(away);

		IsLookingAway = true;
		GazeSourceName = target.Name.TextValue;
	}

	private static void Reset()
	{
		_hasLastPos = false;
	}
}
