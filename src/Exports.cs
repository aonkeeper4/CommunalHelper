using Celeste.Mod.CommunalHelper.Components;
using Celeste.Mod.CommunalHelper.DashStates;
using Celeste.Mod.CommunalHelper.States;
using MonoMod.ModInterop;
using DreamTunnelDash = Celeste.Mod.CommunalHelper.DashStates.DreamTunnelDash;

namespace Celeste.Mod.CommunalHelper;

public static class ModExports
{
    internal static void Initialize()
    {
        typeof(DashStates).ModInterop();
    }

    [ModExportName("CommunalHelper.DashStates")]
    public static class DashStates
    {
        #region DreamTunnel

        public static int GetDreamTunnelDashState()
        {
            return St.DreamTunnelDash;
        }

        public static bool HasDreamTunnelDash()
        {
            return DreamTunnelDash.DreamTunnelDashCount > 0;
        }

        public static int GetDreamTunnelDashCount()
        {
            return DreamTunnelDash.DreamTunnelDashCount;
        }

        public static bool IsDreamTunnelDashAttacking()
        {
            return DreamTunnelDash.DreamTunnelDashAttacking;
        }

        #region Components

        // i'd love to be able to merge these 2 into 1 but i don't think we'll be able to for backwards compat reasons
        public static Component DreamTunnelInteraction(Action<Player> onPlayerEnter, Action<Player> onPlayerExit)
        {
            return new DreamTunnelInteraction(onPlayerEnter, onPlayerExit);
        }

        public static Component DreamTunnelInteraction(Action<Player> onPlayerEnter, Action<Player> onPlayerExit, bool evenIfIntermediate)
        {
            return new DreamTunnelInteraction(onPlayerEnter, onPlayerExit, evenIfIntermediate);
        }

        public static Component DreamTunnelCollider(Collider collider, bool ignoreEntityCollidable)
        {
            return new DreamTunnelCollider(collider, ignoreEntityCollidable);
        }

        public static Component DreamTunnelCollider(Collider collider, bool ignoreEntityCollidable, Action<Player> onPlayerEnter, Action<Player> onPlayerExit, bool evenIfIntermediate)
        {
            return new DreamTunnelCollider(collider, ignoreEntityCollidable, onPlayerEnter, onPlayerExit, evenIfIntermediate);
        }

        public static bool DreamTunnelColliderCheck(Component collider, Player player)
        {
            return (collider as DreamTunnelCollider)?.Check(player) ?? false;
        }

        #endregion

        #endregion

        #region Seeker

        public static bool HasSeekerDash()
        {
            return SeekerDash.HasSeekerDash;
        }

        public static bool IsSeekerDashAttacking()
        {
            return SeekerDash.SeekerAttacking;
        }

        #endregion
    }
}
