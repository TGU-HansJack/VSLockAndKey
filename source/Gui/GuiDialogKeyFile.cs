using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
namespace VSMechanicalLocks.Gui;

public sealed class GuiDialogKeyFile : GuiDialogGeneric
{
    public string Token { get; }
    int revision;
    int selected;
    int selectedLayer;
    readonly DummySlot preview = new();
    readonly ElementBounds chartBounds = ElementBounds.Fixed(95, 225, 305, 88);
    readonly ElementBounds fileBounds = ElementBounds.Fixed(0, 355, 240, 30);
    GuiElementCustomDraw chart = null!;
    bool holding;
    bool waiting;
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
        composer.AddInteractiveElement(new GuiElementCustomRender(api, ElementBounds.Fixed(0, 90, 400, 110), (dt, b) =>
        {
            api.Render.RenderItemstackToGui(preview, b.renderX + b.OuterWidth / 2, b.renderY + b.OuterHeight / 2, 100, (float)GuiElement.scaled(85), -1, true, false, false);
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
        if (args.Button == EnumMouseButton.Left && chartBounds.PointInside(args.X, args.Y) && !waiting)
        {
            double x = args.X - chartBounds.absX;
            double y = args.Y - chartBounds.absY;
            if (x >= 0 && x < chartBounds.InnerWidth && y >= 0 && y < chartBounds.InnerHeight)
            {
                Cancel();
                selectedLayer = y < 42 ? 0 : 1;
                selected = Math.Clamp((int)(x / (chartBounds.InnerWidth / 8)), 0, 7);
                chart.Redraw();
                args.Handled = true;
                return;
            }
        }
        if (args.Button == EnumMouseButton.Left && fileBounds.PointInside(args.X, args.Y) && !waiting)
        { holding = true; BeginStroke(); args.Handled = true; return; }
        base.OnMouseDown(args);
    }
    public override void OnMouseUp(MouseEvent args) { Cancel(); base.OnMouseUp(args); }
    public override void OnRenderGUI(float dt)
    {
        if (holding && !fileBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY)) Cancel();
        if (holding && !waiting && capi.World.ElapsedMilliseconds - started >= 1050)
        { Send("cut"); waiting = true; }
        string state = KeyProfile.IsFinished(preview.Itemstack!) ? "key-finished" : KeyProfile.Read(preview.Itemstack!).Cut ? "key-in-progress" : "filing-blank";
        SingleComposer.GetDynamicText("progress").SetNewText(Lang.Get("vsmechanicallocks:" + state) + (holding ? "  " + Math.Min(100, (capi.World.ElapsedMilliseconds - started) / 10) + "%" : ""));
        base.OnRenderGUI(dt);
    }
    public override void OnGuiClosed()
    {
        Cancel(); Send("close"); base.OnGuiClosed();
    }
}
