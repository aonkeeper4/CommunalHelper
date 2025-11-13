using Celeste.Mod.CommunalHelper.Components;
using Celeste.Mod.CommunalHelper.Imports;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.Entities;

[Tracked]
internal class DreamSpriteRenderer : Entity
{
    private readonly int rendererDepth;
    private readonly List<DreamSprite> sprites = [];

    private static readonly AlphaTestEffect AlphaTestEffect = new(Engine.Graphics.GraphicsDevice)
    {
        VertexColorEnabled = true,
        DiffuseColor = Color.White.ToVector3(),
        AlphaFunction = CompareFunction.GreaterEqual,
        ReferenceAlpha = 1,
        World = Matrix.Identity,
        View = Matrix.Identity
    };

    private static readonly DepthStencilState DrawToStencilState = new()
    {
        StencilEnable = true,
        StencilFunction = CompareFunction.Always,
        StencilPass = StencilOperation.Replace,
        ReferenceStencil = 1,
        DepthBufferEnable = false,
    };
    private static readonly DepthStencilState DrawWithStencilState = new()
    {
        StencilEnable = true,
        StencilFunction = CompareFunction.LessEqual,
        StencilPass = StencilOperation.Keep,
        ReferenceStencil = 1,
        DepthBufferEnable = false,
    };

    private static MTexture[] particleTextures;
    private float animTimer;

    public DreamSpriteRenderer(int rendererDepth)
    {
        Depth = this.rendererDepth = rendererDepth;
        Tag = Tags.Global | Tags.TransitionUpdate;

        Add(new BeforeRenderHook(BeforeRender));
    }

    public void Track(DreamSprite sprite) => sprites.Add(sprite);
    public void Untrack(DreamSprite sprite) => sprites.Remove(sprite);

    public override void Update()
    {
        base.Update();
        animTimer += 6f * Engine.DeltaTime;
    }

    private void BeforeRender()
    {
        // cannot store buffer as it may be deep-cloned by state-saving
        CommunalHelperGFX.QueryDreamSpriteBuffers(rendererDepth, out var buffer);

        Camera camera = SceneAs<Level>().Camera;

        Engine.Graphics.GraphicsDevice.SetRenderTarget(buffer);
        Engine.Graphics.GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.Stencil, Color.Transparent, 0f, 0);

        foreach (DreamSprite sprite in sprites)
        {
            Vector2 spritePosition = sprite.Position;
            Vector2 spriteScale = sprite.Scale;
            float spriteRotation = sprite.Rotation;
            if (sprite.InvertedGravityHandler is { } handler &&
                ((sprite.Entity as Actor)?.GetGravity() ?? GravityType.Normal) == GravityType.Inverted)
                handler(ref spritePosition, ref spriteScale, ref spriteRotation);

            Engine.Graphics.GraphicsDevice.Clear(ClearOptions.Stencil, Color.Transparent, 0f, 0);

            Vector2 posOnScreen = spritePosition + sprite.Entity.Position - camera.Position;
            Rectangle boundsOnScreen = new(
                (int) posOnScreen.X + sprite.ParticleBounds.X,
                (int) posOnScreen.Y + sprite.ParticleBounds.Y,
                sprite.ParticleBounds.Width,
                sprite.ParticleBounds.Height
            );

            if (!boundsOnScreen.Intersects(buffer.Bounds) || !sprite.Visible)
                continue;
            
            PandorasBox.GetVisualSettingsFor(sprite.Marker,
                out Color? controllerActiveBackColor,
                out Color? controllerDisabledBackColor,
                out Color? controllerActiveLineColor,
                out Color? controllerDisabledLineColor,
                out _, out _);

            // outline
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null);

            Color outlineColor = (sprite.Enabled
                ? controllerActiveLineColor ?? DreamSprite.ActiveLineColor
                : controllerDisabledLineColor ?? DreamSprite.DisabledLineColor).Mult(sprite.Color);
            sprite.Texture.Draw(posOnScreen + Vector2.UnitX, sprite.Origin, outlineColor, spriteScale, spriteRotation, sprite.Effects);
            sprite.Texture.Draw(posOnScreen - Vector2.UnitX, sprite.Origin, outlineColor, spriteScale, spriteRotation, sprite.Effects);
            sprite.Texture.Draw(posOnScreen + Vector2.UnitY, sprite.Origin, outlineColor, spriteScale, spriteRotation, sprite.Effects);
            sprite.Texture.Draw(posOnScreen - Vector2.UnitY, sprite.Origin, outlineColor, spriteScale, spriteRotation, sprite.Effects);

            Draw.SpriteBatch.End();

            // back
            AlphaTestEffect.Projection = Matrix.CreateOrthographicOffCenter(0, CommunalHelperGFX.GameplayBufferWidth, CommunalHelperGFX.GameplayBufferHeight, 0, 0, 1);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DrawToStencilState, RasterizerState.CullNone, AlphaTestEffect);

            Color backColor = (sprite.Enabled
                ? controllerActiveBackColor ?? DreamSprite.ActiveBackColor
                : controllerDisabledBackColor ?? DreamSprite.DisabledBackColor).Mult(sprite.Color);
            sprite.Texture.Draw(posOnScreen, sprite.Origin, Color.Lerp(backColor, Color.White, sprite.Flash), spriteScale, spriteRotation, sprite.Effects);

            Draw.SpriteBatch.End();

            // particles
            if (sprite.Particles is null)
                continue;
            
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DrawWithStencilState, RasterizerState.CullNone, null);

            for (int i = 0; i < sprite.Particles.Length; i++)
            {
                int layer = sprite.Particles[i].Layer;

                Vector2 particlePos = sprite.Particles[i].Position;
                particlePos -= camera.Position * (0.7f - 0.25f * layer); // should be consistent with dream blocks
                while (particlePos.X < boundsOnScreen.Left) particlePos.X += boundsOnScreen.Width;
                while (particlePos.X > boundsOnScreen.Right) particlePos.X -= boundsOnScreen.Width;
                while (particlePos.Y < boundsOnScreen.Top) particlePos.Y += boundsOnScreen.Height;
                while (particlePos.Y > boundsOnScreen.Bottom) particlePos.Y -= boundsOnScreen.Height;

                MTexture mTexture;
                switch (layer)
                {
                    case 0:
                        int num = (int) ((sprite.Particles[i].TimeOffset * 4f + animTimer) % 4f);
                        mTexture = particleTextures[3 - num];
                        break;
                    
                    case 1:
                        int num2 = (int) ((sprite.Particles[i].TimeOffset * 2f + animTimer) % 2f);
                        mTexture = particleTextures[1 + num2];
                        break;
                    
                    default:
                        mTexture = particleTextures[2];
                        break;
                }

                Color particleColor = (sprite.Enabled ? sprite.Particles[i].EnabledColor : sprite.Particles[i].DisabledColor).Mult(sprite.Color);
                mTexture.DrawCentered(particlePos, particleColor);
            }

            Draw.SpriteBatch.End();
        }
    }

    public override void Render()
    {
        CommunalHelperGFX.QueryDreamSpriteBuffers(rendererDepth, out RenderTarget2D buffer);
        Draw.SpriteBatch.Draw(buffer, SceneAs<Level>().Camera.Position, Color.White);
    }

    internal static void InitializeTextures()
    {
        particleTextures = [
            GFX.Game["objects/dreamblock/particles"].GetSubtexture(14, 0, 7, 7),
            GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7),
            GFX.Game["objects/dreamblock/particles"].GetSubtexture(0, 0, 7, 7),
            GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7),
        ];
    }

    public static DreamSpriteRenderer GetDreamSpriteRenderer(Scene scene, int depth)
    {
        if (scene.Tracker.GetEntities<DreamSpriteRenderer>()
                 .Concat(scene.Entities.ToAdd)
                 .FirstOrDefault(r => r is DreamSpriteRenderer && r.Depth == depth)
            is DreamSpriteRenderer renderer)
            return renderer;
        
        scene.Add(renderer = new DreamSpriteRenderer(depth));
        Util.Log(LogLevel.Info, $"creating new DreamSpriteRenderer at depth {depth}.");
        return renderer;
    }
}
