namespace Celeste.Mod.CommunalHelper.Components;

public class DreamTunnelInteraction : Component
{
    public Action<Player> OnPlayerEnter;
    public Action<Player> OnPlayerExit;

    // normally OnPlayerEnter/OnPlayerExit only get called when the player enters/exits dream tunnel state into/from this solid.
    // if EvenIfIntermediate is true, they will get called whenever the player enters/leaves this solid while in dream tunnel state.
    public bool EvenIfIntermediate;

    public DreamTunnelInteraction(Action<Player> onPlayerEnter, Action<Player> onPlayerExit, bool evenIfIntermediate = false)
        : base(false, false)
    {
        OnPlayerEnter = onPlayerEnter;
        OnPlayerExit = onPlayerExit;
        EvenIfIntermediate = evenIfIntermediate;
    }
}
