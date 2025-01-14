using Celeste.Mod.CommunalHelper.Entities;
using Celeste.Mod.CommunalHelper.States;
using MonoMod.Utils;
using System.Linq;
using DreamTunnelDash = Celeste.Mod.CommunalHelper.DashStates.DreamTunnelDash;

namespace Celeste.Mod.CommunalHelper.Components;

/// <summary>
/// Collider component that behaves as if it were a Solid during Dream Tunnel Dash.
/// </summary>
[Tracked]
public class DreamTunnelCollider : Component
{
    public sealed class ColliderDummy : SolidDummy
    {
        public DreamTunnelCollider DreamTunnelCollider { get; }

        public ColliderDummy(Entity entity, DreamTunnelCollider collider) : base(entity)
        {
            DreamTunnelCollider = collider;
        }
    }

    public static readonly Color ActiveColor = Color.Magenta;
    public static readonly Color InactiveColor = Color.DarkMagenta;

    public Collider Collider;
    public ColliderDummy Dummy;

    public DreamTunnelCollider(Collider collider) : base(true, true)
    {
        Collider = collider;
        Dummy = new(Entity, this);
    }

    public override void Added(Entity entity)
    {
        base.Added(entity);
        Dummy.Entity = entity;
    }

    /// <summary>
    /// Checks if the player is colliding with this component.
    /// </summary>
    /// <param name="player">The player instance.</param>
    public bool Check(Player player, Vector2? dir = null)
    {
        // no need for a Player_canEnterDreamTunnelCollider check like DreamDashCollider, as DreamTunnelDashAttacking is enough
        // the check is necessary in DreamDashCollider because some things give dash attack without the player needing to (be able to) dash
        if (Active && Collider is not null && Entity != null)
        {
            Collider collider = Entity.Collider;

            Entity.Collider = Collider;
            bool check = player.CollideCheck(Entity, player.Position + (dir ?? Vector2.Zero));
            Entity.Collider = collider;

            return check;
        }
        return false;
    }

    public override void Update()
    {
        base.Update();
        if (
            Util.TryGetPlayer(out Player player) &&
            Check(player) &&
            DreamTunnelDash.DreamTunnelDashAttacking && player.DashAttacking &&
            player.Speed != Vector2.Zero &&
            player.StateMachine.State != St.DreamTunnelDash
        )
        {
            DynamicData playerData = player.GetData();
            player.StateMachine.State = St.DreamTunnelDash;
            Dummy.Components.GetAll<DreamTunnelInteraction>().ToList().ForEach(i => i.OnPlayerEnter(player));
            playerData.Set(DreamTunnelDash.Player_solid, Dummy);
            playerData.Set("dashAttackTimer", 0f);
            playerData.Set("gliderBoostTimer", 0f);
        }
    }

    public override void DebugRender(Camera camera)
    {
        if (Collider != null)
        {
            Collider collider = Entity.Collider;

            Entity.Collider = Collider;
            Collider.Render(camera, Active ? ActiveColor : InactiveColor);
            Entity.Collider = collider;
        }
    }
}
