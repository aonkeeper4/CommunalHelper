using System.Collections;

namespace Celeste.Mod.CommunalHelper.Entities;

public class AeroScreen_Cassette : AeroScreen
{
    private readonly int width, height;

    private float screenW, screenH;
    private float targetScreenW, targetScreenH;
    private readonly float maxTargetScreenW, maxTargetScreenH;
    private const float blinkThreshold = 0.3f;
    private const float blinkSpeedFactor = 15f;

    private bool backlit;
    private const float backlightBrightness = 0.25f;

    private CassetteListener listener;

    private Color[] colorOptions = new Color[] {
        Calc.HexToColor("49aaf0"),
        Calc.HexToColor("f049be"),
        Calc.HexToColor("fcdc3a"),
        Calc.HexToColor("38e04e")
    };
    private readonly Color screenColor;

    public AeroScreen_Cassette(int width, int height, CassetteListener listener, Color? screenColor = null)
    {
        this.width = width;
        this.height = height;

        this.listener = listener;
        this.listener.OnWillActivate += OnWillActivate;
        this.listener.OnWillDeactivate += OnWillDeactivate;

        this.screenColor = screenColor ?? colorOptions[listener.Index];
        maxTargetScreenW = targetScreenW = width - 8;
        maxTargetScreenH = targetScreenH = height - 8;
        screenW = screenH = 0;
    }

    private void OnWillActivate() => backlit = true;
    private void OnWillDeactivate() => backlit = false;

    public override void Update()
    {
        targetScreenW = listener.Activated ? maxTargetScreenW : (screenH / maxTargetScreenH > blinkThreshold ? maxTargetScreenW : 0);
        targetScreenH = listener.Activated ? (screenW / maxTargetScreenW > (1 - blinkThreshold) ? maxTargetScreenH : 1) : 1;
        screenW = Calc.Approach(screenW, targetScreenW, blinkSpeedFactor * maxTargetScreenW * Engine.DeltaTime);
        screenH = Calc.Approach(screenH, targetScreenH, blinkSpeedFactor * maxTargetScreenH * Engine.DeltaTime);
    }

    private void DrawRectCentered(float w, float h, Color col)
    {
        Draw.Rect(Block.Position + new Vector2((width - w) / 2, (height - h) / 2), w, h, col);
    }

    public override void Render()
    {
        if (backlit)
        {
            DrawRectCentered(maxTargetScreenW, maxTargetScreenH, Color.Lerp(Color.Transparent, screenColor, backlightBrightness));
        }
        DrawRectCentered(screenW, screenH, screenColor);
    }

    public override void Finish() { }
}