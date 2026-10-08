using ECommons.EzIpcManager;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace RotationSolver.IPC;

/// <summary>
/// Subscribes to vnavmesh's IPC endpoints for automated movement.
/// Used by <see cref="Updaters.MechanicDodger"/> to move the player out of
/// dangerous AoEs reported by BossModReborn.
/// </summary>
internal static class VNavmesh_IPCSubscriber
{
	private static readonly EzIPCDisposalToken[] _disposalTokens =
		EzIPC.Init(typeof(VNavmesh_IPCSubscriber), "vnavmesh", SafeWrapper.AnyException);

	internal static bool IsEnabled => IPCSubscriber_Common.IsReady("vnavmesh");

	/// <summary>
	/// True when vnavmesh has a navmesh loaded for the current territory.
	/// </summary>
	[EzIPC("Nav.IsReady", true)]
	internal static readonly Func<bool>? NavIsReady;

	/// <summary>
	/// Pathfind from the player's position to <paramref name="dest"/> and move there.
	/// Returns true if the request was accepted.
	/// </summary>
	[EzIPC("SimpleMove.PathfindAndMoveTo", true)]
	internal static readonly Func<Vector3, bool, bool>? PathfindAndMoveTo;

	/// <summary>
	/// Stop any active automated movement.
	/// </summary>
	[EzIPC("Path.Stop", true)]
	internal static readonly Action? PathStop;

	/// <summary>
	/// True while vnavmesh is actively moving the player.
	/// </summary>
	[EzIPC("Path.IsRunning", true)]
	internal static readonly Func<bool>? PathIsRunning;

	internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
}
