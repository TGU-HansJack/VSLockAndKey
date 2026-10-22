using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
namespace VSMechanicalLocks.Gui;

public sealed class GuiDialogKeyFile : GuiDialogGeneric
{
    public string Token { get; }
    int revision;
    int selected;
    int selectedLayer;
    readonly DummySlot preview = new();
    readonly ElementBounds previewBounds = ElementBounds.Fixed(0, 90, 400, 110);
    readonly ElementBounds chartBounds = ElementBounds.Fixed(95, 225, 305, 88);
    readonly ElementBounds fileBounds = ElementBounds.Fixed(0, 355, 240, 30);
    GuiElementCustomDraw chart = null!;
    bool holding;
    bool waiting;
    bool rotating;
    int lastMouseX;
    float previewYaw;
    MeshRef? toothOutline;
    bool outlineDirty = true;
    long started;
    public override string ToggleKeyCombinationCode => null!;
    public GuiDialogKeyFile(ICoreClientAPI api, FilingStatePacket packet) : base(Lang.Get("vsmechanicallocks:keyfile-title"), api)
    {
        Token = packet.Token;
        revision = packet.Revision;
        preview.Itemstack = VSMechanicalLocksModSystem.UnpackStack(packet.Stack, api.World);
        var bg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bg.BothSizing = ElementSizing.FitToChildren;
        var composer = api.Gui.CreateCompo("vlk-filing", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(bg, true).AddDialogTitleBar(Lang.Get("vsmechanicallocks:keyfile-title"), () => TryClose())
            .BeginChildElements(bg)
            .AddStaticText(Lang.Get("vsmechanicallocks:filing-instructions"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, 400, 55));
        composer.AddInteractiveElement(new GuiElementCustomRender(api, previewBounds, (dt, b) =>
        {
            double x = b.renderX + b.OuterWidth / 2;
            double y = b.renderY + b.OuterHeight / 2;
            // Keep the rotated model inside this dialog's depth range.
            api.Render.GlPushMatrix();
            try
            {
                ApplyPreviewTransform(api.Render, x, y, previewYaw, GuiElement.scaled(1));
                api.Render.RenderItemstackToGui(preview, x, y, 100, (float)GuiElement.scaled(85), -1, true, false, false);
                RenderToothOutline();
            }
            finally { api.Render.GlPopMatrix(); }
        }), "preview");
        chart = new GuiElementCustomDraw(api, chartBounds, DrawBlade, true);
        composer.AddInteractiveElement(chart, "blade");
        composer.AddStaticText(Lang.Get("vsmechanicallocks:key-layer-a"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 232, 85, 28))
            .AddStaticText(Lang.Get("vsmechanicallocks:key-layer-b"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 274, 85, 28));
        composer.AddSmallButton(Lang.Get("vsmechanicallocks:hold-file"), () => true, fileBounds)
            .AddSmallButton(Lang.Get("vsmechanicallocks:finish"), () => { if (waiting) return true; Cancel(); Send("finish"); return true; }, ElementBounds.Fixed(260, 355, 140, 30))
            .AddDynamicText("", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 320, 400, 25), "progress")
            .EndChildElements();
        SingleComposer = composer.Compose();
        Update(packet);
    }
    internal static void ApplyPreviewTransform(IRenderAPI render, double x, double y, float yaw, double guiScale)
    {
        render.GlTranslate(x, y, 100d);
        // GUI projection is orthographic: compressing only Z after rotation leaves
        // screen size/position unchanged and retains the model's own depth ordering.
        // Normalize GUI scale so neither the background (Z=50) nor the next dialog
        // (ZSize=150) intersects the model, including at large interface scales.
        render.GlScale(1, 1, (float)(.1 / guiScale));
        render.GlRotate(yaw, 0, 1, 0);
        render.GlTranslate(-x, -y, -100d);
    }
    void RenderToothOutline()
    {
        if (preview.Itemstack?.Collectible is not ItemKey key) return;
        if (outlineDirty)
        {
            toothOutline?.Dispose();
            toothOutline = null;
            toothOutline = capi.Render.UploadMesh(key.ToothOutline(preview.Itemstack, selectedLayer * 8 + selected));
            outlineDirty = false;
        }
        // Render immediately after the item, retaining its exact GUI model-view and
        // projection matrices (including drag rotation and the item's own transform).
        var shader = capi.Render.CurrentActiveShader;
        shader.Uniform("noTexture", 1f);
        shader.Uniform("rgbaIn", new Vec4f(.95f, .12f, .10f, 1));
        try { capi.Render.RenderMesh(toothOutline!); }
        finally
        {
            shader.Uniform("noTexture", 0f);
            shader.Uniform("rgbaIn", new Vec4f(1, 1, 1, 1));
        }
    }
    void DrawBlade(Context ctx, ImageSurface surface, ElementBounds b)
    {
        var profile = KeyProfile.Read(preview.Itemstack!);
        if (!profile.Valid) return;
        for (int layer = 0; layer < 2; layer++)
        for (int i = 0; i < 8; i++)
        {
            bool active = layer == selectedLayer && i == selected;
            double cellWidth = b.InnerWidth / 8;
            double x = i * cellWidth + 2;
            double y = layer * 42 + 5;
            double width = cellWidth - 4;
            double toothHeight = 7 + profile.Heights[layer * 8 + i] * 4;
            ctx.SetSourceRGBA(.72, .67, .52, 1);
            ctx.Rectangle(x, y + 27 - toothHeight, width, toothHeight);
            ctx.Fill();
            if (active)
            {
                ctx.SetSourceRGBA(.95, .12, .10, 1);
                ctx.LineWidth = 2;
                ctx.Rectangle(x - 1, y - 1, width + 2, 30);
                ctx.Stroke();
            }
        }
    }
    public void Update(FilingStatePacket packet)
    {
        revision = packet.Revision;
        preview.Itemstack = VSMechanicalLocksModSystem.UnpackStack(packet.Stack, capi.World);
        outlineDirty = true;
        waiting = false; chart.Redraw();
        if (holding) BeginStroke();
    }
    void Send(string action) => capi.Network.GetChannel(VSMechanicalLocksModSystem.NetworkChannelId).SendPacket(new FilingPacket { Action = action, Token = Token, Revision = revision, Tooth = selectedLayer * 8 + selected });
    void BeginStroke()
    {
        var profile = KeyProfile.Read(preview.Itemstack!);
        if (!profile.Valid || profile.Heights[selectedLayer * 8 + selected] == 0 || capi.World.Player.Entity.LeftHandItemSlot.Itemstack?.Collectible is not ItemKeyFile) { holding = false; return; }
        started = capi.World.ElapsedMilliseconds; Send("start");
    }
    void Cancel()
    {
        if (holding && !waiting) Send("cancel");
        holding = false;
    }
    public override void OnMouseDown(MouseEvent args)
    {
        if (args.Button == EnumMouseButton.Left && previewBounds.PointInside(args.X, args.Y))
        {
            Cancel();
            rotating = true;
            lastMouseX = args.X;
            args.Handled = true;
            return;
        }
        if (args.Button == EnumMouseButton.Left && chartBounds.PointInside(args.X, args.Y) && !waiting)
        {
            double x = args.X - chartBounds.absX;
            double y = args.Y - chartBounds.absY;
            if (x >= 0 && x < chartBounds.InnerWidth && y >= 0 && y < chartBounds.InnerHeight)
            {
                Cancel();
                selectedLayer = y < 42 ? 0 : 1;
                selected = Math.Clamp((int)(x / (chartBounds.InnerWidth / 8)), 0, 7);
                outlineDirty = true;
                chart.Redraw();
                args.Handled = true;
                return;
            }
        }
        if (args.Button == EnumMouseButton.Left && fileBounds.PointInside(args.X, args.Y) && !waiting)
        { holding = true; BeginStroke(); args.Handled = true; return; }
        base.OnMouseDown(args);
    }
    public override void OnMouseMove(MouseEvent args)
    {
        if (rotating)
        {
            float delta = (float)((args.X - lastMouseX) / GuiElement.scaled(1) * .75);
            previewYaw = (previewYaw + delta) % 360;
            lastMouseX = args.X;
            args.Handled = true;
            return;
        }
        base.OnMouseMove(args);
    }
    public override void OnMouseUp(MouseEvent args)
    {
        if (rotating && args.Button == EnumMouseButton.Left)
        {
            rotating = false;
            args.Handled = true;
            return;
        }
        Cancel(); base.OnMouseUp(args);
    }
    public override void OnRenderGUI(float dt)
    {
        if (rotating && !capi.Input.MouseButton.Left) rotating = false;
        if (holding && !fileBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY)) Cancel();
        if (holding && !waiting && capi.World.ElapsedMilliseconds - started >= 1050)
        { Send("cut"); waiting = true; }
        string state = KeyProfile.IsFinished(preview.Itemstack!) ? "key-finished" : KeyProfile.Read(preview.Itemstack!).Cut ? "key-in-progress" : "filing-blank";
        SingleComposer.GetDynamicText("progress").SetNewText(Lang.Get("vsmechanicallocks:" + state) + (holding ? "  " + Math.Min(100, (capi.World.ElapsedMilliseconds - started) / 10) + "%" : ""));
        base.OnRenderGUI(dt);
    }
    public override void OnGuiClosed()
    {
        rotating = false;
        toothOutline?.Dispose();
        toothOutline = null;
        outlineDirty = true;
        Cancel(); Send("close"); base.OnGuiClosed();
    }
    public override void Dispose()
    {
        toothOutline?.Dispose();
        toothOutline = null;
        base.Dispose();
    }
}
