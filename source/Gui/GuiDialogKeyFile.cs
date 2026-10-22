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
    readonly DummySlot preview = new();
    readonly ElementBounds chartBounds = ElementBounds.Fixed(0, 195, 400, 100);
    readonly ElementBounds fileBounds = ElementBounds.Fixed(0, 350, 240, 45);
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
        composer.AddInteractiveElement(new GuiElementCustomRender(api, ElementBounds.Fixed(0, 90, 400, 95), (dt, b) =>
        {
            api.Render.RenderItemstackToGui(preview, b.renderX + b.OuterWidth / 2, b.renderY + b.OuterHeight / 2, 100, (float)GuiElement.scaled(85), -1, true, false, false);
        }), "preview");
        chart = new GuiElementCustomDraw(api, chartBounds, DrawBlade, true);
        composer.AddInteractiveElement(chart, "blade");
        for (int i = 0; i < 8; i++)
        {
            int tooth = i;
            composer.AddSmallButton((i + 1).ToString(), () => { Cancel(); selected = tooth; chart.Redraw(); return true; }, ElementBounds.Fixed(i * 50, 300, 44, 30), key: "tooth" + i);
        }
        composer.AddSmallButton(Lang.Get("vsmechanicallocks:hold-file"), () => true, fileBounds)
            .AddSmallButton(Lang.Get("vsmechanicallocks:finish"), () => { if (waiting) return true; Cancel(); Send("finish"); return true; }, ElementBounds.Fixed(260, 350, 140, 45))
            .AddDynamicText("", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 410, 400, 45), "progress")
            .EndChildElements();
        SingleComposer = composer.Compose();
        Update(packet);
    }
    void DrawBlade(Context ctx, ImageSurface surface, ElementBounds b)
    {
        double scale = GuiElement.scaled(1);
        ctx.Scale(scale, scale);
        ctx.SetSourceRGBA(.75, .70, .53, 1); ctx.Rectangle(0, 5, 400, 12); ctx.Fill();
        var profile = KeyProfile.Read(preview.Itemstack!);
        if (!profile.Valid) return;
        for (int i = 0; i < 8; i++)
        {
            ctx.SetSourceRGBA(i == selected ? .95 : .64, i == selected ? .80 : .60, .35, 1);
            ctx.Rectangle(i * 50, 17, 46, 8 + profile.Heights[i] * 12); ctx.Fill();
        }
    }
    public void Update(FilingStatePacket packet)
    {
        revision = packet.Revision;
        preview.Itemstack = VSMechanicalLocksModSystem.UnpackStack(packet.Stack, capi.World);
        waiting = false; chart.Redraw();
        if (holding) BeginStroke();
    }
    void Send(string action) => capi.Network.GetChannel(VSMechanicalLocksModSystem.NetworkChannelId).SendPacket(new FilingPacket { Action = action, Token = Token, Revision = revision, Tooth = selected });
    void BeginStroke()
    {
        var profile = KeyProfile.Read(preview.Itemstack!);
        if (!profile.Valid || profile.Heights[selected] == 0 || capi.World.Player.Entity.LeftHandItemSlot.Itemstack?.Collectible is not ItemKeyFile) { holding = false; return; }
        started = capi.World.ElapsedMilliseconds; Send("start");
    }
    void Cancel()
    {
        if (holding && !waiting) Send("cancel");
        holding = false;
    }
    public override void OnMouseDown(MouseEvent args)
    {
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
        string state = KeyProfile.IsFinished(preview.Itemstack!) ? "key-finished" : KeyProfile.Read(preview.Itemstack!).Cut ? "key-in-progress" : "key-blank";
        SingleComposer.GetDynamicText("progress").SetNewText(Lang.Get("vsmechanicallocks:" + state) + (holding ? "  " + Math.Min(100, (capi.World.ElapsedMilliseconds - started) / 10) + "%" : ""));
        base.OnRenderGUI(dt);
    }
    public override void OnGuiClosed()
    {
        Cancel(); Send("close"); base.OnGuiClosed();
    }
}
