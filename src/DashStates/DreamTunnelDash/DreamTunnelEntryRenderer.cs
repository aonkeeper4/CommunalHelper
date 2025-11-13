using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.Entities;

/// <summary>
/// Handles the background and line rendering for DreamTunnelEntries.
/// Added in a LevelLoader.LoadingThread hook in DreamTunnelEntry.
/// </summary>
[Tracked(false)]
public class DreamTunnelEntryRenderer : Entity
{
    private class CustomDepthRenderer : Entity
    {
        public readonly List<DreamTunnelEntry> List = [];

        public CustomDepthRenderer(int depth)
        {
            Tag = Tags.Global | Tags.TransitionUpdate;
            Depth = depth;
        }

        public override void Render()
        {
            foreach (DreamTunnelEntry e in List.Where(e => e.Visible))
            {
                Vector2 shake = e.Shake;

                Vector2 start = shake + e.Start;
                Vector2 end = shake + e.End;

                Imports.PandorasBox.GetVisualSettingsFor(e, out Color? controllerActiveBackColor, out Color? controllerDisabledBackColor, out _, out _, out _, out _);
                Color backColor = e.PlayerHasDreamDash
                    ? controllerActiveBackColor ?? DreamBlock.activeBackColor
                    : controllerDisabledBackColor ?? DreamBlock.disabledBackColor;

                Draw.Rect(shake.X + e.X, shake.Y + e.Y, e.Width, e.Height, backColor * e.Alpha);
                if (e.WhiteFill > 0f)
                    Draw.Rect(e.X + shake.X, e.Y + shake.Y, e.Width, e.Height * e.WhiteHeight, Color.White * e.WhiteFill * e.Alpha);
                e.WobbleLine(start, end, 0f, false, true);
            }

            foreach (DreamTunnelEntry e in List.Where(e => e.Visible))
                e.WobbleLine(e.Shake + e.Start, e.Shake + e.End, 0f, true, false);
        }
    }

    private readonly Dictionary<int, CustomDepthRenderer> renderers = [];

    public DreamTunnelEntryRenderer()
    {
        Tag = Tags.Global | Tags.TransitionUpdate;
    }

    public void Track(DreamTunnelEntry entity, int depth)
    {
        // Create new renderer with specific depth if doesn't exist, or get the older one otherwise.
        if (!renderers.TryGetValue(depth, out CustomDepthRenderer renderer))
        {
            renderers.Add(depth, renderer = new CustomDepthRenderer(depth));
            entity.Scene.Add(renderer);
        }

        // Add entity
        renderer.List.Add(entity);
    }

    public void Untrack(DreamTunnelEntry entity, int depth)
    {
        if (!renderers.TryGetValue(depth, out CustomDepthRenderer renderer))
            return;
        renderer.List.Remove(entity);

        if (renderer.List.Count == 0)
        {
            // No entity with this renderer's depth exist, get rid of it.
            renderers.Remove(depth);
            renderer.RemoveSelf();
        }
    }
}
