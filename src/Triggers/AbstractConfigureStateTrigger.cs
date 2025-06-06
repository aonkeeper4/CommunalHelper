using Celeste.Mod.CommunalHelper.Components;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.Triggers;

// unfortunately, due to this class being generic, `[Tracked(true)]` doesn't really do anything here
// all children of this class need to be marked as `[TrackedAs(typeof(AbstractConfigureStateTrigger<TOptions, TChanges>))]` in order to be tracked properly
[Tracked(true)]
public abstract class AbstractConfigureStateTriggerBase(EntityData data, Vector2 offset) : Trigger(data, offset)
{
    protected readonly bool revertOnLeave = data.Bool("revertOnLeave", false);
    protected readonly bool revertOnDeath = data.Bool("revertOnDeath", true);
    internal static void Load()
    {
        Everest.Events.Player.OnDie += OnDieBase;
    }

    internal static void Unload()
    {
        Everest.Events.Player.OnDie -= OnDieBase;
    }

    protected abstract void OnDie(Player player);
    private static void OnDieBase(Player player)
    {
        foreach (AbstractConfigureStateTriggerBase trigger in player.SceneAs<Level>().Tracker.GetEntities<AbstractConfigureStateTriggerBase>())
        {
            trigger.OnDie(player);
        }
    }
}

public abstract class AbstractConfigureStateTrigger<TOptions, TChanges> 
    : AbstractConfigureStateTriggerBase
    where TOptions : struct where TChanges : struct
{
    private readonly TOptions options;
    private TChanges? changesNeededToRevert;

    private readonly bool onlyOnce;

    public AbstractConfigureStateTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        onlyOnce = data.Bool("onlyOnce", false);

        options = GetConfiguredOptions(data);

        string flag = data.Attr("flag");
        if (!string.IsNullOrEmpty(flag))
        {
            Add(new FlagToggleComponent(flag, data.Bool("flagInverted")));
        }
    }

    protected abstract TOptions GetConfiguredOptions(EntityData data);
    protected abstract TOptions GetCurrentOptions(Player player);
    protected abstract void SaveOptions(Player player, TOptions options);

    protected abstract TChanges CalculateChangesNeededToRevert(TOptions from, TOptions to);
    protected abstract TOptions RevertChanges(TOptions current, TChanges? changesNeededToRevert);

    public override void OnEnter(Player player)
    {
        changesNeededToRevert = CalculateChangesNeededToRevert(GetCurrentOptions(player), options);
        SaveOptions(player, options);

        if (onlyOnce)
        {
            RemoveSelf();
        }
    }

    public override void OnLeave(Player player)
    {
        if (revertOnLeave && !player.Dead)
        {
            SaveOptions(player, RevertChanges(options, changesNeededToRevert));
        }
    }

    protected override sealed void OnDie(Player player)
    {
        SaveOptions(player, RevertChanges(options, changesNeededToRevert));
    }
}
