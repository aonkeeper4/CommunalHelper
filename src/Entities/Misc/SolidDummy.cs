namespace Celeste.Mod.CommunalHelper.Entities;

/// <summary>
/// A pseudo-<see cref="Solid"/> class used as the dummy solid in <see cref="DreamTunnelCollider"/>.
/// </summary>
/// not sure if this is worth keeping separate from DreamTunnelCollider since it's so niche and tiny
[TrackedAs(typeof(Solid))]
[Tracked]
public class SolidDummy : Solid
{
    public Entity Entity;

    public SolidDummy(Entity entity) : base(Vector2.Zero, 0, 0, false)
    {
        Collidable = Active = Visible = false;
        Entity = entity;
    }

    public override void Awake(Scene scene) { }

    public override void Update() { }

    public override void Render() { }
}
