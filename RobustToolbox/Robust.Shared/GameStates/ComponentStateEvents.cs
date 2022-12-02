using Robust.Shared.GameObjects;
using Robust.Shared.Players;

namespace Robust.Shared.GameStates
{
    [ByRefEvent, ComponentEvent]
    public readonly struct ComponentHandleState
    {
        public ComponentState? Current { get; }
        public ComponentState? Next { get; }

        public ComponentHandleState(ComponentState? current, ComponentState? next)
        {
            Current = current;
            Next = next;
        }
    }

    /// <summary>
    ///     Component event for getting the component state for a specific player.
    /// </summary>
    [ByRefEvent, ComponentEvent]
    public struct ComponentGetState
    {
        /// <summary>
        ///     Output parameter. Set this to the component's state for the player.
        /// </summary>
        public ComponentState? State { get; set; }

        /// <summary>
        ///     Input parameter. The player the state is being sent to.
        /// </summary>
        public readonly ICommonSession? Player;

        public ComponentGetState(ICommonSession? player)
        {
            Player = player;
            State = null;
        }
    }

    [ByRefEvent, ComponentEvent]
    public struct ComponentGetStateAttemptEvent
    {
        /// <summary>
        ///     Input parameter. The player the state is being sent to.
        /// </summary>
        public readonly ICommonSession Player;

        public bool Cancelled = false;

        public ComponentGetStateAttemptEvent(ICommonSession player)
        {
            Player = player;
        }
    }
}
