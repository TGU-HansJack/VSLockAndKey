using System.Reflection;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VSMechanicalLocks.Gui;
[assembly: ModInfo("Mechanical Locks & Keys", "vsmechanicallocks")]
namespace VSMechanicalLocks;

public class VSMechanicalLocksModSystem : ModSystem
{
    public const string NetworkChannelId = "vsmechanicallocks";
    const string HarmonyId = "vsmechanicallocks.mechanical";
    public ICoreAPI Api { get; private set; } = null!;
    public MechanicalLocks Locks { get; private set; } = null!;
    public ModConfig Settings { get; private set; } = new();
    ICoreServerAPI? sapi;
    ICoreClientAPI? capi;
    Harmony? harmony;
    GuiDialogKeyFile? filingDialog;
    readonly Dictionary<string, FilingSession> sessions = new();
    readonly Dictionary<string, KeyringInventory> keyrings = new();
    GuiDialogKeyring? keyringDialog;
    sealed class FilingSession
    {
        public string Token = Guid.NewGuid().ToString("N");
        public ItemSlot Slot = null!;
        public ItemStack Key = null!;
        public ItemStack? File;
        public int Revision;
        public int Tooth = -1;
        public long Started;
    }
    public static VSMechanicalLocksModSystem Get(IWorldAccessor world) => world.Api.ModLoader.GetModSystem<VSMechanicalLocksModSystem>();
    public override void StartPre(ICoreAPI api)
    {
        if (api.Side == EnumAppSide.Server)
        {
            Settings = api.LoadModConfig<ModConfig>("vsmechanicallocks.json") ?? new();
            Settings.KeyringSlotsByMaterial ??= ModConfig.DefaultKeyringSlotsByMaterial();
            Settings.DefaultKeyringSlots = Math.Clamp(Settings.DefaultKeyringSlots, 0, 64);
            foreach (var key in Settings.KeyringSlotsByMaterial.Keys.ToArray()) Settings.KeyringSlotsByMaterial[key] = Math.Clamp(Settings.KeyringSlotsByMaterial[key], 0, 64);
            api.StoreModConfig(Settings, "vsmechanicallocks.json");
            api.World.Config.SetString("vsmechanicallocks:config", Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Settings))));
        }
        else
        {
            try { Settings = JsonConvert.DeserializeObject<ModConfig>(Encoding.UTF8.GetString(Convert.FromBase64String(api.World.Config.GetString("vsmechanicallocks:config", "")))) ?? new(); }
            catch { Settings = new(); }
        }
    }
    public override void Start(ICoreAPI api)
    {
        Api = api;
        Locks = new(api);
        api.RegisterItemClass("ItemKey", typeof(ItemKey));
        api.RegisterItemClass("ItemKeyFile", typeof(ItemKeyFile));
        api.RegisterItemClass("ItemKeyring", typeof(ItemKeyring));
        api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<FilingPacket>().RegisterMessageType<FilingStatePacket>()
            .RegisterMessageType<LockActionPacket>().RegisterMessageType<LockStatePacket>().RegisterMessageType<NoticePacket>()
            .RegisterMessageType<OpenKeyringPacket>().RegisterMessageType<KeyringContentsPacket>();
        api.Event.OnTestBlockAccess += Locks.TestAccess;
        if (!Harmony.HasAnyPatches(HarmonyId))
        {
            harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
    public override void AssetsFinalize(ICoreAPI api)
    {
        foreach (var item in api.World.Items)
            if (item is ItemPadlock) item.MaxStackSize = 1;
        foreach (var block in api.World.Blocks)
        {
            if (block.HasBehavior<BlockBehaviorLockable>() && !block.HasBehavior<BlockBehaviorMechanicalLock>())
                block.BlockBehaviors = new BlockBehavior[] { new BlockBehaviorMechanicalLock(block) }.Concat(block.BlockBehaviors).ToArray();
        }
    }
    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        TooltipText.Register();
        api.Network.GetChannel(NetworkChannelId)
            .SetMessageHandler<KeyringContentsPacket>(OnKeyringContentsPacket)
            .SetMessageHandler<FilingStatePacket>(p =>
            {
                if (p.Token.Length == 0) { filingDialog?.TryClose(); return; }
                if (filingDialog?.IsOpened() != true || filingDialog.Token != p.Token)
                {
                    filingDialog?.TryClose();
                    filingDialog?.Dispose();
                    filingDialog = new GuiDialogKeyFile(api, p);
                    filingDialog.TryOpen();
                }
                else filingDialog.Update(p);
            })
            .SetMessageHandler<LockStatePacket>(Locks.Receive)
            .SetMessageHandler<NoticePacket>(p => api.TriggerIngameError(this, p.Code, Vintagestory.API.Config.Lang.Get("vsmechanicallocks:" + p.Code)));
        api.Event.RegisterGameTickListener(dt =>
        {
            var pos = api.World.Player.CurrentBlockSelection?.Position;
            if (pos != null) Locks.Request(pos, true);
        }, 1000);
    }
    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        api.Network.GetChannel(NetworkChannelId)
            .SetMessageHandler<FilingPacket>(OnFiling)
            .SetMessageHandler<LockActionPacket>(Locks.Handle)
            .SetMessageHandler<OpenKeyringPacket>(OnOpenKeyringPacket);
        api.Event.RegisterGameTickListener(dt =>
        {
            foreach (var uid in keyrings.Keys.ToArray())
            {
                var inv = keyrings[uid];
                if (api.World.PlayerByUid(uid) == null || !inv.HeldValid)
                {
                    inv.TakeLocked = inv.PutLocked = true;
                    inv.Owner.InventoryManager.CloseInventory(inv);
                    keyrings.Remove(uid);
                }
            }
            foreach (var uid in sessions.Keys.ToArray())
            {
                var player = api.World.PlayerByUid(uid) as IServerPlayer;
                if (player == null || !ValidSession(player, sessions[uid]))
                {
                    sessions.Remove(uid);
                    if (player != null) api.Network.GetChannel(NetworkChannelId).SendPacket(new FilingStatePacket(), player);
                }
            }
        }, 100);
    }
    bool ValidSession(IServerPlayer player, FilingSession s) => player.Entity.Alive
        && player.WorldData.CurrentGameMode != EnumGameMode.Spectator
        && ReferenceEquals(player.InventoryManager.ActiveHotbarSlot, s.Slot)
        && ReferenceEquals(s.Slot.Itemstack, s.Key)
        && ReferenceEquals(player.Entity.LeftHandItemSlot.Itemstack, s.File);
    public static byte[] PackStack(ItemStack stack)
    {
        var tree = new TreeAttribute(); tree.SetItemstack("stack", stack);
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
        tree.ToBytes(w); return ms.ToArray();
    }
    public static ItemStack UnpackStack(byte[] bytes, IWorldAccessor world)
    {
        var tree = new TreeAttribute();
        using var ms = new MemoryStream(bytes); using var r = new BinaryReader(ms); tree.FromBytes(r);
        var stack = tree.GetItemstack("stack"); stack.ResolveBlockOrItem(world); return stack;
    }
    void Reply(IServerPlayer player, FilingSession s) => sapi!.Network.GetChannel(NetworkChannelId).SendPacket(new FilingStatePacket { Token = s.Token, Revision = s.Revision, Stack = PackStack(s.Key) }, player);
    void OnFiling(IServerPlayer player, FilingPacket packet)
    {
        if (packet.Action == "open")
        {
            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (!player.Entity.Alive || player.WorldData.CurrentGameMode == EnumGameMode.Spectator || slot.Itemstack?.Collectible is not ItemKey || player.Entity.LeftHandItemSlot.Itemstack?.Collectible is not ItemKeyFile || !KeyProfile.Read(slot.Itemstack).Valid) return;
            var fresh = new FilingSession { Slot = slot, Key = slot.Itemstack, File = player.Entity.LeftHandItemSlot.Itemstack };
            sessions[player.PlayerUID] = fresh;
            Reply(player, fresh); return;
        }
        if (!sessions.TryGetValue(player.PlayerUID, out var s) || s.Token != packet.Token) return;
        if (!ValidSession(player, s))
        {
            sessions.Remove(player.PlayerUID);
            sapi!.Network.GetChannel(NetworkChannelId).SendPacket(new FilingStatePacket(), player);
            return;
        }
        if (packet.Action == "close") { sessions.Remove(player.PlayerUID); return; }
        if (packet.Revision != s.Revision) { Reply(player, s); return; }
        var profile = KeyProfile.Read(s.Key);
        switch (packet.Action)
        {
            case "start":
                if (s.File?.Collectible is not ItemKeyFile || packet.Tooth < 0 || packet.Tooth >= 16 || profile.Heights[packet.Tooth] == 0) return;
                s.Tooth = packet.Tooth; s.Started = Api.World.ElapsedMilliseconds; return;
            case "cancel": s.Tooth = -1; return;
            case "cut":
                if (s.File?.Collectible is not ItemKeyFile || s.Tooth != packet.Tooth) { Reply(player, s); return; }
                if (Api.World.ElapsedMilliseconds - s.Started < 1000) { Reply(player, s); return; }
                s.Tooth = -1;
                if (!profile.File(packet.Tooth)) return;
                profile.Write(s.Key, false); s.Slot.MarkDirty();
                s.File.Collectible.DamageItem(Api.World, player.Entity, player.Entity.LeftHandItemSlot, 1);
                s.File = player.Entity.LeftHandItemSlot.Itemstack;
                break;
            case "finish":
                if (!profile.Cut) { Locks.Notice(player, "cut-required"); return; }
                profile.Write(s.Key, true); s.Slot.MarkDirty(); s.Tooth = -1; break;
            default: return;
        }
        s.Revision++; Reply(player, s);
    }
    public override void Dispose()
    {
        if (capi != null) TooltipText.Unregister();
        filingDialog?.TryClose(); filingDialog?.Dispose(); keyringDialog?.TryClose(); keyringDialog?.Dispose(); sessions.Clear(); keyrings.Clear();
        if (Api != null) Api.Event.OnTestBlockAccess -= Locks.TestAccess;
        harmony?.UnpatchAll(HarmonyId);
        base.Dispose();
    }
    void OnOpenKeyringPacket(IServerPlayer fromPlayer, OpenKeyringPacket packet)
    {
        if (sapi == null || !fromPlayer.Entity.Alive || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Spectator) return;

        ItemSlot keyringSlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
        if (keyringSlot?.Itemstack?.Collectible is not ItemKeyring) return;

        ItemStack?[] contents = ItemKeyring.GetContents(keyringSlot.Itemstack, fromPlayer.Entity.World);
        int slotCount = contents.Length;

        if (keyrings.Remove(fromPlayer.PlayerUID, out var previous))
        {
            previous.TakeLocked = previous.PutLocked = true;
            fromPlayer.InventoryManager.CloseInventory(previous);
        }
        KeyringInventory inv = new(slotCount, "keyring-" + Guid.NewGuid().ToString("N"), sapi, fromPlayer, keyringSlot);
        keyrings[fromPlayer.PlayerUID] = inv;

        for (int i = 0; i < slotCount; i++)
        {
            inv[i].Itemstack = contents[i];
        }

        fromPlayer.InventoryManager.OpenInventory(inv);

        TreeAttribute tree = new();
        inv.ToTreeAttributes(tree);
        using MemoryStream ms = new();
        using (BinaryWriter writer = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            tree.ToBytes(writer);
        }

        sapi.Network.GetChannel(NetworkChannelId).SendPacket(new KeyringContentsPacket
        {
            InventoryId = inv.InventoryID,
            DialogTitle = keyringSlot.Itemstack.GetName(),
            SlotCount = slotCount,
            TreeBytes = ms.ToArray()
        }, fromPlayer);
    }

    void OnKeyringContentsPacket(KeyringContentsPacket packet)
    {
        if (capi == null) return;

        keyringDialog?.TryClose(); keyringDialog?.Dispose();
        InventoryGeneric inv = new(packet.SlotCount, packet.InventoryId, capi,
            (id, self) => new KeyringGridSlot(self, id, null));

        TreeAttribute tree = new();
        using (MemoryStream ms = new(packet.TreeBytes))
        using (BinaryReader reader = new(ms))
        {
            tree.FromBytes(reader);
        }
        inv.FromTreeAttributes(tree);
        inv.ResolveBlocksOrItems();

        capi.World.Player.InventoryManager.OpenInventory(inv);

        keyringDialog = new GuiDialogKeyring(packet.DialogTitle, inv, capi);
        keyringDialog.TryOpen();
    }
}
