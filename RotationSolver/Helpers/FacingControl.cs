using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace RotationSolver.Helpers;

/// <summary>
/// Duncan's fork: direct character-facing control for gaze mechanics.
///
/// Game rotation convention (verified against BossMod's Actor model, which
/// reads the game value directly): radians, 0 = facing South (+Z),
/// +PI/2 = East (+X), PI = North (-Z), -PI/2 = West (-X).
/// Facing direction for rotation r is (sin r, cos r) in (X, Z).
///
/// Facing is applied through the game client's own SetRotation, the same
/// primitive BossMod-style tools use. It is re-applied every tick while a
/// gaze handler is active, because the client's auto-face-target-on-action
/// would otherwise snap the character back toward the boss on any action
/// (which is also why actions are suppressed while looking away).
/// </summary>
internal static class FacingControl
{
	/// <summary>
	/// Rotation (radians) that faces from <paramref name="from"/> toward <paramref name="toward"/>.
	/// </summary>
	public static float AngleToFace(Vector3 from, Vector3 toward)
		=> MathF.Atan2(toward.X - from.X, toward.Z - from.Z);

	/// <summary>
	/// Rotation (radians) that faces directly away from <paramref name="source"/>.
	/// </summary>
	public static float AngleToFaceAway(Vector3 from, Vector3 source)
		=> MathF.Atan2(from.X - source.X, from.Z - source.Z);

	/// <summary>
	/// Sets the player character's facing immediately. No-op when the player object is unavailable.
	/// Must run on the game thread (framework update), like the rest of the updater pipeline.
	/// </summary>
	public static unsafe void SetFacing(float radians)
	{
		var player = Player.Object;
		if (player == null) return;
		try
		{
			var gameObject = (GameObject*)player.Address;
			gameObject->SetRotation(radians);
		}
		catch
		{
			// Never let a facing write break the update loop.
		}
	}

	/// <summary>
	/// Smallest absolute angular difference between two rotations, in radians.
	/// </summary>
	public static float AngleDifference(float a, float b)
	{
		var d = (a - b) % MathF.Tau;
		if (d > MathF.PI) d -= MathF.Tau;
		if (d < -MathF.PI) d += MathF.Tau;
		return MathF.Abs(d);
	}
}
