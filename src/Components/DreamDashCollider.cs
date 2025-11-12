using Celeste.Mod.CommunalHelper.Entities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.Components;

[Tracked]
internal class DreamDashCollider : Component
{
    // Used as a dream block dummy, but which stores a DreamDashCollider property.
    // The reason for this is I didn't want to store a DreamDashCollider inside DreamBlockDummy.
    private sealed class ColliderDummy(Entity entity, DreamDashCollider dreamDashCollider) : DreamBlockDummy(entity)
    {
        public DreamDashCollider DreamDashCollider => dreamDashCollider;
    }

    private static readonly Color ActiveColor = Color.Teal;
    private static readonly Color InactiveColor = Calc.HexToColor("044f63"); // darker teal

    public readonly Collider Collider;
    private ColliderDummy dummy;

    private readonly Action<Player> onEnter, onExit;

    public DreamDashCollider(Collider collider, Action<Player> onEnter = null, Action<Player> onExit = null)
        : base(active: true, visible: false)
    {
        Collider = collider;
        
        this.onEnter = onEnter;
        this.onExit = onExit;
    }

    public override void EntityAdded(Scene scene)
    {
        base.EntityAdded(scene);
        
        Scene.Add(dummy = new ColliderDummy(Entity, this));
    }

    /// <summary>
    /// Checks if the player is colliding with this component.
    /// </summary>
    /// <param name="player">The player instance.</param>
    private bool Check(Player player)
    {
        if (!Active
            || Collider is null
            || Entity is null
            || !player.GetData().Data.TryGetValue(Player_canEnterDreamDashCollider, out object canEnter)
            || !canEnter.Equals(true))
            return false;
        
        Collider collider = Entity.Collider;

        Entity.Collider = Collider;
        bool check = player.CollideCheck(Entity);
        Entity.Collider = collider;

        return check;
    }

    public override void Update()
    {
        base.Update();

        if (!Util.TryGetPlayer(out Player player)
            || !Check(player)
            || !player.DashAttacking
            || player.Speed == Vector2.Zero
            || player.StateMachine.State == Player.StDreamDash)
            return;
        
        player.dreamBlock = dummy;
        player.StateMachine.State = Player.StDreamDash;
    }

    public override void DebugRender(Camera camera)
    {
        if (Collider is null)
            return;
        
        Collider collider = Entity.Collider;

        Entity.Collider = Collider;
        Collider.Render(camera, Active ? ActiveColor : InactiveColor);
        Entity.Collider = collider;
    }

    #region Hooks

    private const string Player_canEnterDreamDashCollider = "communalHelperCanEnterDreamDashCollider";

    internal static void Load()
    {
        IL.Celeste.Player.DreamDashUpdate += Player_DreamDashUpdate;
        On.Celeste.Player.DashBegin += Player_DashBegin;
        On.Celeste.Player.DreamDashEnd += Player_DreamDashEnd;
    }

    internal static void Unload()
    {
        IL.Celeste.Player.DreamDashUpdate -= Player_DreamDashUpdate;
        On.Celeste.Player.DashBegin -= Player_DashBegin;
        On.Celeste.Player.DreamDashEnd -= Player_DreamDashEnd;
    }

    private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin orig, Player self)
    {
        orig(self);
        
        self.GetData().Set(Player_canEnterDreamDashCollider, true);
    }

    private static void Player_DreamDashEnd(On.Celeste.Player.orig_DreamDashEnd orig, Player self)
    {
        DynamicData playerData = self.GetData();
        
        if (self.dreamBlock is DreamBlockDummy dummy)
            foreach (DreamDashCollider collider in dummy.Entity.Components.GetAll<DreamDashCollider>())
            {
                playerData.Set(Player_canEnterDreamDashCollider, false);
                collider.onExit?.Invoke(self);
            }
        
        orig(self);
    }

    private static void Player_DreamDashUpdate(ILContext il)
    {
        ILCursor cursor = new(il);

        if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCall<Entity>("CollideFirst")))
            return;
        
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Func<DreamBlock, Player, DreamBlock>>((dreamBlock, self)
            => self.Scene.Tracker.GetComponents<DreamDashCollider>()
                                 .Cast<DreamDashCollider>()
                                 .FirstOrDefault(collider => collider.Check(self))?
                                 .dummy ?? dreamBlock);

        if (!cursor.TryGotoNext(instr => instr.MatchStfld<Player>("dreamBlock")))
            return;

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Func<DreamBlock, Player, DreamBlock>>((dreamBlock, self) =>
        { 
            DreamBlock oldDreamBlock = self.dreamBlock;
            if (dreamBlock != oldDreamBlock && dreamBlock is ColliderDummy dummy)
                dummy.DreamDashCollider.onEnter?.Invoke(self);

            return dreamBlock;
        });
    }

    #endregion
}
