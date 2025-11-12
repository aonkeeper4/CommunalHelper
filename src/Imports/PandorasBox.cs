using Celeste.Mod.CommunalHelper.Components;
using Celeste.Mod.CommunalHelper.Entities;
using MonoMod.ModInterop;
using System.Collections.Generic;

namespace Celeste.Mod.CommunalHelper.Imports;

public static class PandorasBox
{
    #region Dream Dash Controller
    
    [ModImportName("PandorasBox.DreamDashController")]
    public static class DreamDashController
    {
        public static Action<List<Type>> AddSetupIgnoringTypes;
        public static Action<List<Type>> RemoveSetupIgnoringTypes;
    
        public static Action<List<Type>> AddControlledTypes;
        public static Action<List<Type>> RemoveControlledTypes;

        public static Func<Entity, (bool?, bool?, bool?, bool?, bool?, bool?, bool?, float?, float?)> GetGameplaySettingsFor;
        public static Func<Entity, (Color?, Color?, Color?, Color?, List<List<Color>>)> GetVisualSettingsFor;
    }
    
    public static void AddSetupIgnoringTypes(List<Type> types)
        => DreamDashController.AddSetupIgnoringTypes?.Invoke(types);
    public static void RemoveSetupIgnoringTypes(List<Type> types)
        => DreamDashController.RemoveSetupIgnoringTypes?.Invoke(types);
    
    public static void AddControlledTypes(List<Type> types)
        => DreamDashController.AddControlledTypes?.Invoke(types);
    public static void RemoveControlledTypes(List<Type> types)
        => DreamDashController.RemoveControlledTypes?.Invoke(types);

    public static (bool?, bool?, bool?, bool?, bool?, bool?, bool?, float?, float?) GetGameplaySettingsFor(Entity entity)
        => DreamDashController.GetGameplaySettingsFor?.Invoke(entity) ?? default;
    public static (Color?, Color?, Color?, Color?, List<List<Color>>) GetVisualSettingsFor(Entity entity)
        => DreamDashController.GetVisualSettingsFor?.Invoke(entity) ?? default;
    
    #endregion
    
    private static readonly List<Type> SetupIgnoringTypes =
    [
        typeof(CustomDreamBlock),
        typeof(ConnectedDreamBlock),
        typeof(DreamCrumbleWallOnRumble),
        typeof(DreamFallingBlock),
        typeof(DreamFloatySpaceBlock),
        typeof(DreamMoveBlock),
        typeof(DreamSwapBlock),
        typeof(DreamSwitchGate),
        typeof(DreamZipMover),
        typeof(ChainedDreamFallingBlock)
    ];
    private static readonly List<Type> ControlledTypes =
    [
        typeof(DreamSprite.DreamSpriteMarker),
        typeof(DreamTunnelEntry)
    ];
    
    internal static void Initialize()
    {
        AddSetupIgnoringTypes(SetupIgnoringTypes);
        AddControlledTypes(ControlledTypes);
    }
}
