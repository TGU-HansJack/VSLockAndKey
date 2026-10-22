using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VSMechanicalLocks;

class Program
{
    static int checks;
    static string game = "";
    static void Main(string[] args)
    {
        game = Path.GetFullPath(args[0]);
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (string folder in new[] { game, Path.Combine(game, "Lib"), Path.Combine(game, "Mods") })
            {
                string path = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            return null;
        };
        Run();
        Console.WriteLine($"PASS: {checks} assertions against {args[0]}");
    }
    static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        Profiles(); Geometry(); Persistence(); Filing(); LockLifecycle(); Patches(); Tooltips();
    }
    static void Tooltips()
    {
        NativeLibrary.SetDllImportResolver(typeof(Cairo.Context).Assembly, (name, assembly, path) =>
        {
            string file = Path.Combine(game, "Lib", name);
            if (!File.Exists(file) && OperatingSystem.IsWindows()) file += ".dll";
            return File.Exists(file) ? NativeLibrary.Load(file) : IntPtr.Zero;
        });
        string previousLocale = Lang.CurrentLocale;
        Lang.AvailableLanguages["tooltip-test"] = Stub.Make<ITranslationService>((m, a) =>
            m.Name == "get_LineBreakBehavior" ? EnumLinebreakBehavior.AfterWord : null);
        Lang.ChangeLanguage("tooltip-test");
        RuntimeEnv.GUIScale = 1;
        var api = Stub.Make<ICoreClientAPI>((m, a) => m.Name == "get_Logger" ? Stub.Make<ILogger>((_, _) => null) : null);
        var font = new CairoFont(16, "Microsoft YaHei", new double[] { 1, 1, 1, 1 });
        var type = typeof(ItemKey).Assembly.GetType("VSMechanicalLocks.Gui.TooltipText")!;
        type.GetMethod("Register")!.Invoke(null, null);
        try
        {
            foreach (string locale in new[] { "zh-cn", "en" })
            {
                var lang = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText($"resources/assets/vsmechanicallocks/lang/{locale}.json"))!;
                foreach (string[] keys in new[] { new[] { "key-blank", "key-help" }, new[] { "keyring-help" }, new[] { "padlock-new" } })
                {
                    var text = new StringBuilder("Stack: 1\r\n\r\n");
                    foreach (string key in keys)
                        typeof(ItemKey).GetMethod("AppendInfoLine", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { text, lang["vsmechanicallocks:" + key] });
                    var components = VtmlUtil.Richtextify(api, text.ToString(), font);
                    foreach (int width in new[] { 150, 225, 305, 400 })
                    {
                        var element = new GuiElementRichtext(api, components, ElementBounds.Fixed(0, 0, width, 1));
                        element.CalcHeightAndPositions();
                        var lines = components.OfType<RichTextComponent>().SelectMany(c => c.Lines).Where(l => l.Text.Length > 0).ToArray();
                        string actual = string.Concat(lines.Select(l => l.Text));
                        string expected = "Stack: 1" + string.Concat(keys.Select(k => lang["vsmechanicallocks:" + k]));
                        Check(string.Concat(actual.Where(c => !char.IsWhiteSpace(c))) == string.Concat(expected.Where(c => !char.IsWhiteSpace(c))), $"{locale} tooltip preserves every character at width {width}");
                        Check(lines.Zip(lines.Skip(1)).All(p => p.Second.Bounds.Y - p.First.Bounds.Y <= p.First.Bounds.Height + 1), $"{locale} tooltip has no empty rows at width {width}");
                    }
                }
            }
        }
        finally
        {
            type.GetMethod("Unregister")!.Invoke(null, null);
            Lang.AvailableLanguages.Remove("tooltip-test");
            Lang.ChangeLanguage(previousLocale);
        }
    }
    static void Profiles()
    {
        var blank = new ItemStack(new ItemKey { ItemId = 1 });
        Check(KeyProfile.Read(blank).Valid && !KeyProfile.Read(blank).Cut && !KeyProfile.IsFinished(blank), "cast blank");
        new KeyProfile().Write(blank, true);
        Check(!KeyProfile.IsFinished(blank), "blank cannot be finished");
        var profile = new KeyProfile(); profile.File(2); profile.Write(blank, true);
        Check(KeyProfile.IsFinished(blank), "cut key can finish");
        var copy = profile.Copy(); copy.File(2);
        Check(!copy.Matches(profile) && profile.Heights[2] == 4, "independent geometry copies");
        Check(!profile.File(-1) && !profile.File(16), "invalid tooth bounds");
        for (int i = 0; i < 4; i++) Check(profile.File(2), "remove remaining metal");
        Check(!profile.File(2) && profile.Heights[2] == 0, "cannot add metal or file below zero");
        Check(!new KeyProfile { Heights = new[] { 1 } }.Valid && !new KeyProfile { Version = 3 }.Valid && !new KeyProfile { Spec = "other" }.Matches(profile), "reject incompatible profiles");
        var legacy = new KeyProfile { Version = 1, Spec = "standard-8", Heights = new[] { 0, 1, 2, 3, 4, 5, 4, 3 } };
        Check(legacy.UpgradeLegacy() && legacy.Valid && legacy.Heights.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 4, 3, 0, 1, 2, 3, 4, 5, 4, 3 }), "legacy key profiles migrate to matching two-layer cuts");
        var jeweled = new ItemStack(new ItemKey { ItemId = 2, Code = new AssetLocation("vsmechanicallocks:key-jeweled-gold-ruby") });
        typeof(CollectibleObject).GetField("api", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(jeweled.Collectible, Stub.Make<ICoreAPI>((m,a) => null));
        var inputs = new[] { new DummySlot(blank) }; var output = new DummySlot(jeweled);
        jeweled.Collectible.OnCreatedByCrafting(inputs, output, new GridRecipe { AverageDurability = false });
        Check(KeyProfile.Read(blank).Matches(KeyProfile.Read(jeweled)) && KeyProfile.IsFinished(jeweled), "jeweling preserves geometry and finished flag across materials");
        var changed = KeyProfile.Read(jeweled); changed.File(1); changed.Write(jeweled, false);
        Check(!KeyProfile.IsFinished(jeweled) && !changed.Matches(KeyProfile.Read(blank)), "refiling invalidates finish and original match");
        var padlock = new ItemStack(new ItemPadlock { ItemId = 3 });
        MechanicalLocks.WriteCylinder(padlock, profile);
        Check(profile.Matches(MechanicalLocks.ReadCylinder(padlock)), "detachable cylinder roundtrip");
        padlock.Attributes.SetString(MechanicalLocks.CylinderAttribute, "broken json");
        Check(MechanicalLocks.ReadCylinder(padlock)?.Valid == false, "corrupt cylinder cannot become fresh unconfigured lock");
        var world = Stub.Make<IWorldAccessor>((m,a) => m.Name == "GetItem" ? blank.Item : null);
        var restored = VSMechanicalLocksModSystem.UnpackStack(VSMechanicalLocksModSystem.PackStack(blank), world);
        Check(KeyProfile.IsFinished(restored) && KeyProfile.Read(restored).Matches(KeyProfile.Read(blank)), "network/save stack roundtrip");
    }
    static void Geometry()
    {
        foreach (string name in new[] { "key", "key-jeweled" })
        {
            var template = JsonConvert.DeserializeObject<Shape>(File.ReadAllText($"resources/assets/vsmechanicallocks/shapes/item/{name}.json"))!;
            var item = new ItemKey();
            typeof(ItemKey).GetField("template", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(item,template);
            var stack = new ItemStack(item);
            var profile = new KeyProfile();
            profile.Heights = new[] { 0,1,2,3,4,5,0,5, 5,4,3,2,1,0,2,4 };
            profile.Write(stack,true);
            var blade = (Shape)typeof(ItemKey).GetMethod("Blade", BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(item,new object[]{stack})!;
            var teeth = blade.Elements.Single(e=>e.Name=="Teeth").Children!;
            Check(teeth.Length==16, name+" has eight tooth sections on each blade layer");
            Check(teeth.Select((e,i)=>Math.Abs(e.From![1]-(1-(.125+.175*profile.Heights[i])))<.00001 && Math.Abs((e.To![0]-e.From[0])-.25f)<.00001).All(x=>x), name+" mesh geometry follows each layer's cuts without changing total thickness");
            Check(template.Elements.Single(e=>e.Name=="Teeth").Children!.All(e=>e.From![1]==0), "rendering never mutates shared asset");
        }
    }
    static void Persistence()
    {
        var data = new Dictionary<string, byte[]>();
        var block = new Block { Code = new AssetLocation("game:chest-east") };
        var part = new BlockMultiblock { OffsetInv = new Vec3i(0,-1,0) };
        Type chunkType = typeof(IBlockAccessor).GetMethod("GetChunkAtBlockPos", new[] { typeof(BlockPos) })!.ReturnType;
        object chunk = Stub.Make(chunkType, (m,a) =>
        {
            if (m.Name == "GetModdata") return data.GetValueOrDefault((string)a[0]!);
            if (m.Name == "SetModdata") data[(string)a[0]!] = (byte[])a[1]!;
            return null;
        });
        bool multiblock = false;
        var accessor = Stub.Make<IBlockAccessor>((m,a) => m.Name == "GetChunkAtBlockPos" ? chunk : m.Name == "GetBlock" ? multiblock && ((BlockPos)a[0]!).Y == 3 ? part : block : null);
        var world = Stub.Make<IWorldAccessor>((m,a) => m.Name == "get_BlockAccessor" ? accessor : null);
        var api = Stub.Make<ICoreAPI>((m,a) => m.Name == "get_World" ? world : m.Name == "get_Side" ? EnumAppSide.Server : null);
        var locks = new MechanicalLocks(api); var pos = new BlockPos(2,2,2,0);
        var profile = new KeyProfile(); profile.File(4);
        var state = new InstalledLock { BlockCode = block.Code.ToString(), PadlockCode = "game:padlock-iron", Profile = profile, Locked = true, Revision = 7 };
        void Save(BlockPos p) => locks.Receive(new LockStatePacket { X=p.X,Y=p.Y,Z=p.Z,Dimension=p.dimension,Json=JsonConvert.SerializeObject(state) });
        Save(pos);
        Check(locks.IsLocked(pos) && locks.Get(pos)!.Revision == 7, "persistent lock state");
        Check(new MechanicalLocks(api).Get(pos)!.Profile!.Matches(profile), "reload from chunk moddata");
        Check(locks.Get(new BlockPos(2,2,2,1)) == null, "dimension isolation even within same mocked chunk");
        multiblock = true;
        Check(locks.IsLocked(new BlockPos(2,3,2,0)), "multiblock part resolves to controller");
        locks.Remove(new BlockPos(2,3,2,0));
        Check(locks.IsLocked(pos), "removing auxiliary part does not orphan controller lock");
        state.Locked = false; Save(pos);
        Check(!locks.IsLocked(pos) && locks.Get(pos)!.Profile!.Matches(profile), "unlock preserves cylinder");
        block.Code = new AssetLocation("game:other");
        Check(locks.Get(pos) == null, "replacement cannot inherit old lock");
        locks.Remove(pos); block.Code = new AssetLocation("game:chest-east");
        Check(locks.Get(pos) == null, "destroy removes record rather than leaving stale lock");
    }
    static void Filing()
    {
        long elapsed = 0;
        var key = new DummySlot(new ItemStack(new ItemKey { ItemId = 1 }));
        var fileItem = new TestFile { ItemId = 2 };
        var file = new DummySlot(new ItemStack(fileItem));
        var entity = new TestPlayer { FileSlot = file };
        var manager = Stub.Make<IPlayerInventoryManager>((m,a) => m.Name == "get_ActiveHotbarSlot" ? key : null);
        var worldData = Stub.Make<IWorldPlayerData>((m,a) => m.Name == "get_CurrentGameMode" ? EnumGameMode.Survival : null);
        var player = (TestServerPlayer)RuntimeHelpers.GetUninitializedObject(typeof(TestServerPlayer));
        player.TestEntity = entity; player.TestManager = manager; player.TestWorldData = worldData;
        FilingStatePacket? reply = null;
        var channel = Stub.Make<IServerNetworkChannel>((m,a) => { if (m.Name == "SendPacket" && a[0] is FilingStatePacket p) reply = p; return null; });
        var network = Stub.Make<IServerNetworkAPI>((m,a) => m.Name == "GetChannel" ? channel : null);
        var world = Stub.Make<IServerWorldAccessor>((m,a) => m.Name == "get_ElapsedMilliseconds" ? elapsed : null);
        var api = Stub.Make<ICoreServerAPI>((m,a) => m.Name == "get_World" ? world : m.Name == "get_Network" ? network : null);
        var mod = new VSMechanicalLocksModSystem();
        typeof(VSMechanicalLocksModSystem).GetField("<Api>k__BackingField", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(mod, api);
        typeof(VSMechanicalLocksModSystem).GetField("sapi", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(mod, api);
        var handler = typeof(VSMechanicalLocksModSystem).GetMethod("OnFiling", BindingFlags.Instance|BindingFlags.NonPublic)!;
        void Send(string action, string? token = null, int? revision = null) => handler.Invoke(mod, new object[] { player, new FilingPacket { Action=action, Token=token??reply!.Token, Revision=revision??reply?.Revision??0, Tooth=0 } });
        Send("open", ""); var token = reply!.Token;
        Send("start"); elapsed=999; Send("cut");
        Check(KeyProfile.Read(key.Itemstack!).Heights[0] == 5 && fileItem.Damage == 0, "early packet cannot file");
        elapsed=1000; Send("cut");
        Check(KeyProfile.Read(key.Itemstack!).Heights[0] == 4 && fileItem.Damage == 1, "one second cuts once and wears file once");
        Send("cut"); Check(fileItem.Damage == 1, "replayed cut cannot repeat");
        Send("finish"); Check(KeyProfile.IsFinished(key.Itemstack!), "server finishes cut blade");
        Send("start"); elapsed=2000; Send("cut", token, 0);
        Check(fileItem.Damage == 1, "stale revision cannot cut");
        Send("cut"); Check(!KeyProfile.IsFinished(key.Itemstack!) && fileItem.Damage == 2, "refiling clears finished flag");
        var original = key.Itemstack; key.Itemstack = new ItemStack(new ItemKey { ItemId=1 });
        Send("finish"); Check(reply!.Token == "" && !KeyProfile.IsFinished(key.Itemstack!) && !KeyProfile.IsFinished(original!), "swapping stack invalidates physical session");
    }
    static void LockLifecycle()
    {
        var data = new Dictionary<string, byte[]>();
        var pos = new BlockPos(1,1,1,0);
        var block = new Block { Code = new AssetLocation("game:chest-east") };
        block.BlockBehaviors = new BlockBehavior[] { new BlockBehaviorLockable(block) };
        var slot = new DummySlot();
        var manager = Stub.Make<IPlayerInventoryManager>((m,a) => m.Name == "get_ActiveHotbarSlot" ? slot : null);
        EnumGameMode mode = EnumGameMode.Survival;
        var worldData = Stub.Make<IWorldPlayerData>((m,a) => m.Name == "get_CurrentGameMode" ? mode : null);
        var player = (TestServerPlayer)RuntimeHelpers.GetUninitializedObject(typeof(TestServerPlayer));
        var entity = new TestPlayer(); entity.Controls.Sneak = false;
        player.TestEntity=entity; player.TestManager=manager; player.TestWorldData=worldData;
        // Lock actions use the vanilla block selection and relative hit position.
        player.Selection = new BlockSelection { Position=pos, HitPosition=new Vec3d(.1, .1, .1) };
        var mod = new VSMechanicalLocksModSystem();
        var loader = Stub.Make<IModLoader>((m,a) => m.Name == "GetModSystem" ? mod : null);
        Type chunkType = typeof(IBlockAccessor).GetMethod("GetChunkAtBlockPos", new[] { typeof(BlockPos) })!.ReturnType;
        object chunk = Stub.Make(chunkType, (m,a) =>
        {
            if (m.Name == "GetModdata") return data.GetValueOrDefault((string)a[0]!);
            if (m.Name == "SetModdata") data[(string)a[0]!] = (byte[])a[1]!;
            return null;
        });
        TestContainer? container = null;
        var accessor = Stub.Make<IBlockAccessor>((m,a) => m.Name switch { "GetChunkAtBlockPos" => chunk, "GetBlock" => block, "GetBlockEntity" => container, _ => null });
        bool build=true;
        var claims = Stub.Make<ILandClaimAPI>((m,a) => m.Name == "TryAccess" ? (EnumBlockAccessFlags)a[2]! != EnumBlockAccessFlags.BuildOrBreak || build : null);
        var padlockItem = new ItemPadlock { ItemId=10, Code=new AssetLocation("game:padlock-iron") };
        var keyItem = new ItemKey { ItemId=11, Code=new AssetLocation("vsmechanicallocks:key-copper") };
        var key2Item = new ItemKey { ItemId=12, Code=new AssetLocation("vsmechanicallocks:key-jeweled-gold-ruby") };
        var ringItem = new ItemKeyring { ItemId=13, Code=new AssetLocation("vsmechanicallocks:keyring-rope") };
        ringItem.VariantStrict["material"]="rope";
        ICoreServerAPI api = null!;
        var world = Stub.Make<IServerWorldAccessor>((m,a) => m.Name switch
        {
            "get_BlockAccessor"=>accessor, "get_Claims"=>claims, "get_Api"=>api,
            "GetItem"=>a[0] is AssetLocation ? padlockItem : (int)a[0]! == 11 ? keyItem : key2Item, _=>null
        });
        string? lastNotice = null;
        var channel = Stub.Make<IServerNetworkChannel>((m,a) =>
        {
            if (m.Name == "SendPacket" && a.FirstOrDefault() is NoticePacket notice) lastNotice = notice.Code;
            return null;
        });
        var network = Stub.Make<IServerNetworkAPI>((m,a) => m.Name == "GetChannel" ? channel : null);
        api = Stub.Make<ICoreServerAPI>((m,a) => m.Name switch { "get_World"=>world, "get_Network"=>network, "get_Side"=>EnumAppSide.Server, "get_ModLoader"=>loader, _=>null });
        var locks = new MechanicalLocks(api);
        typeof(VSMechanicalLocksModSystem).GetField("<Locks>k__BackingField", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(mod, locks);
        void Turn(long? revision = null, bool ctrl = true) => locks.Handle(player, new LockActionPacket { X=pos.X,Y=pos.Y,Z=pos.Z,Dimension=pos.dimension,Revision=revision??locks.Get(pos)?.Revision??0,Ctrl=ctrl });
        slot.Itemstack = new ItemStack(keyItem);
        Check(!locks.Intercept(player, player.Selection), "unlocked vanilla block does not swallow key placement input");
        slot.Itemstack=new ItemStack(padlockItem); build=false; Turn();
        Check(locks.Get(pos)==null && !slot.Empty, "claim BuildOrBreak denial prevents installation");
        build=true; Turn();
        Check(slot.Empty && locks.Get(pos) is { Locked:false,Profile:null }, "install consumes padlock and leaves unfitted unlocked cylinder");
        var key=new ItemStack(keyItem); var profile=new KeyProfile(); profile.File(3); profile.Write(key,true);
        slot.Itemstack=key; Turn();
        Check(locks.Get(pos)!.Profile!.Matches(profile) && !locks.IsLocked(pos), "first finished key configures once without locking");
        Turn(0); Check(!locks.IsLocked(pos), "stale lock action cannot change state");
        Turn(ctrl:false); Check(!locks.IsLocked(pos), "key action without Ctrl does not intercept door right-click");
        var inventory = new InventoryGeneric(1,"test-inventory",null); inventory.Pos=pos; inventory.Api=api;
        container=new TestContainer { Storage=inventory, Api=api, Pos=pos };
        inventory.openedByPlayerGUIds.Add("viewer"); Turn();
        Check(!locks.IsLocked(pos), "cannot lock container while viewed");
        inventory.openedByPlayerGUIds.Clear(); Turn();
        Check(locks.IsLocked(pos), "matching held key locks closed container");
        Check(!MechanicalInventoryAccess.Allowed(inventory,player,false) && !ContainerPacketPatch.Prefix(container,player,1000), "locked container rejects inventory and block entity open paths");
        Check(!ContainerPacketPatch.Prefix(container,player,1) && ContainerPacketPatch.Prefix(container,player,1001), "locked container rejects transfer but permits closing");
        slot.Itemstack=new ItemStack(key2Item); var wrong=new KeyProfile(); wrong.File(5); wrong.Write(slot.Itemstack,true); Turn();
        Check(locks.IsLocked(pos), "different shape cannot unlock");
        profile.Write(slot.Itemstack,true); Turn();
        Check(!locks.IsLocked(pos), "same shape in a different material and decoration unlocks");
        slot.Itemstack=key; Turn(); Check(locks.IsLocked(pos), "vanilla door selection position can relock");
        Turn(); Check(!locks.IsLocked(pos), "vanilla door selection position can unlock");
        player.Selection = new BlockSelection { Position=new BlockPos(pos.X + 1,pos.Y,pos.Z,pos.dimension) };
        Turn();
        Check(!locks.IsLocked(pos) && lastNotice == "aim-at-lock", "wrong vanilla selection is rejected with a clear prompt");
        player.Selection = new BlockSelection { Position=pos, HitPosition=new Vec3d(.1,.1,.1) };
        Check(!MechanicalInventoryAccess.Allowed(inventory,player,true), "unopened inventory cannot transfer");
        inventory.openedByPlayerGUIds.Add("test");
        Check(MechanicalInventoryAccess.Allowed(inventory,player,true), "unlocked nearby opened inventory can transfer");
        entity.Pos.X=100;
        Check(MechanicalInventoryAccess.Allowed(inventory,player,true) && ContainerPacketPatch.Prefix(container,player,1000), "unlocked container access follows vanilla open state without an extra reach limit");
        entity.Pos.X=0;
        mode=EnumGameMode.Spectator;
        Check(!MechanicalInventoryAccess.Allowed(inventory,player,true) && !ContainerPacketPatch.Prefix(container,player,1000), "spectator cannot access unlocked mechanical container through either packet path");
        mode=EnumGameMode.Survival;
        inventory.openedByPlayerGUIds.Clear();
        var ring=new ItemStack(ringItem); ItemKeyring.SetContents(ring,new ItemStack?[]{key,null}); slot.Itemstack=ring; Turn();
        Check(locks.IsLocked(pos), "held keyring tries matching finished keys");
        entity.Pos.X=100; Turn(); Check(!locks.IsLocked(pos), "vanilla selected lock is not subject to an extra fixed reach limit"); entity.Pos.X=0; Turn();
        Check(locks.IsLocked(pos), "vanilla selected lock can be relocked after returning");
        mode=EnumGameMode.Spectator; Turn(); Check(locks.IsLocked(pos), "spectator cannot turn lock"); mode=EnumGameMode.Survival;
        string claimant="someone"; var selection=new BlockSelection { Position=pos };
        Check(locks.TestAccess(player,selection,EnumBlockAccessFlags.Use,ref claimant,EnumWorldAccessResponse.LandClaimed)==EnumWorldAccessResponse.Granted, "held matching shape permits exact Use in claim");
        Check(locks.TestAccess(player,selection,EnumBlockAccessFlags.BuildOrBreak,ref claimant,EnumWorldAccessResponse.LandClaimed)==EnumWorldAccessResponse.LandClaimed, "shape never grants BuildOrBreak");
        Check(locks.TestAccess(player,selection,EnumBlockAccessFlags.Use,ref claimant,EnumWorldAccessResponse.DeniedByMod)==EnumWorldAccessResponse.DeniedByMod, "existing mod veto preserved");
        var tool=new ItemStack(new ItemPlumbAndSquare { ItemId=14 }); slot.Itemstack=tool; Turn();
        Check(locks.Get(pos)!=null, "locked padlock cannot be detached");
        slot.Itemstack=key; Turn(); slot.Itemstack=tool; entity.Controls.Sneak=true; build=false; Turn();
        Check(locks.Get(pos)!=null, "detach respects BuildOrBreak");
        build=true; Turn(); Check(locks.Get(pos)==null, "unlocked padlock can detach"); entity.Controls.Sneak=false;
        // Reinstalling uses the same persisted cylinder attribute as the detached item.
        var moved=new ItemStack(padlockItem); MechanicalLocks.WriteCylinder(moved,profile); slot.Itemstack=moved; Turn();
        Check(locks.Get(pos)!.Profile!.Matches(profile) && !locks.IsLocked(pos), "reinstalled padlock retains cylinder and starts unlocked");
    }
    static void ReinforcementCommands()
    {
        Check(!new ModConfig().EnableReinforcementPermissionCommands, "reinforcement authorization commands default off");
        Check(!JsonConvert.DeserializeObject<ModConfig>("{\"DefaultKeyringSlots\":4}")!.EnableReinforcementPermissionCommands, "old configs default authorization commands off");
        var mod = new VSMechanicalLocksModSystem();
        var loader = Stub.Make<IModLoader>((m, a) => m.Name == "GetModSystem" ? mod : null);
        var target = Stub.Make<IServerPlayerData>((m, a) => m.Name == "get_PlayerUID" ? "target" : null);
        var playerData = Stub.Make<IPlayerDataManager>((m, a) => m.Name == "GetPlayerDataByLastKnownName" ? target : null);
        var api = Stub.Make<ICoreServerAPI>((m, a) => m.Name switch { "get_ModLoader" => loader, "get_PlayerData" => playerData, _ => null });
        var reinforcement = new ModSystemBlockReinforcement();
        AccessTools.Field(typeof(ModSystemBlockReinforcement), "api").SetValue(reinforcement, api);
        var grants = (Dictionary<string, ReinforcedPrivilegeGrants>)AccessTools.Field(typeof(ModSystemBlockReinforcement), "privGrantsByOwningPlayerUid").GetValue(reinforcement)!;
        var groupGrants = AccessTools.Field(typeof(ModSystemBlockReinforcement), "privGrantsByOwningGroupUid").GetValue(reinforcement)!;
        grants["test"] = new ReinforcedPrivilegeGrants();
        grants["test"].PlayerGrants["existing"] = EnumBlockAccessFlags.Use;
        string before = JsonConvert.SerializeObject(new object[] { grants, groupGrants });
        foreach (string name in new[] { "OnCmdGrant", "OnCmdRevoke", "OnCmdGrantGroup", "OnCmdRevokeGroup", "OnCmdGroupGrant", "OnCmdGroupRevoke", "OnCmdGroupGrantGroup", "OnCmdGroupRevokeGroup" })
        {
            // Invoke the patched vanilla handler; a disabled command must return before parsing or touching grants.
            var result = (TextCommandResult)AccessTools.Method(typeof(ModSystemBlockReinforcement), name).Invoke(reinforcement, new object[] { new TextCommandCallingArgs() })!;
            Check(result.Status == EnumCommandStatus.Error && result.StatusMessage == "vsmechanicallocks:reinforcement-permission-commands-disabled", name + " returns disabled feedback");
            Check(JsonConvert.SerializeObject(new object[] { grants, groupGrants }) == before, name + " preserves existing player and group grants");
        }
        mod.Settings.EnableReinforcementPermissionCommands = JsonConvert.DeserializeObject<ModConfig>("{\"EnableReinforcementPermissionCommands\":true}")!.EnableReinforcementPermissionCommands;
        var player = (TestServerPlayer)RuntimeHelpers.GetUninitializedObject(typeof(TestServerPlayer));
        var args = new TextCommandCallingArgs
        {
            Caller = new Caller { Player = player },
            Parsers = new List<ICommandArgumentParser>
            {
                Stub.Make<ICommandArgumentParser>((m, a) => m.Name == "GetValue" ? "target" : null),
                Stub.Make<ICommandArgumentParser>((m, a) => m.Name == "GetValue" ? "use" : null)
            }
        };
        AccessTools.Method(typeof(ModSystemBlockReinforcement), "OnCmdGrant").Invoke(reinforcement, new object[] { args });
        Check(grants["test"].PlayerGrants["target"] == EnumBlockAccessFlags.Use, "enabled command reaches vanilla grant handler");
        AccessTools.Method(typeof(ModSystemBlockReinforcement), "OnCmdRevoke").Invoke(reinforcement, new object[] { args });
        Check(!grants["test"].PlayerGrants.ContainsKey("target"), "enabled command reaches vanilla revoke handler");
        mod.Settings.EnableReinforcementPermissionCommands = false;
        reinforcement.SetPlayerPrivilege(player, "api-target", EnumBlockAccessFlags.Use);
        Check(grants["test"].PlayerGrants["api-target"] == EnumBlockAccessFlags.Use, "command switch preserves vanilla privilege API");
    }
    static void Patches()
    {
        var harmony = new Harmony("vsmechanicallocks.tests");
        string previousLocale = Lang.CurrentLocale;
        Lang.AvailableLanguages["command-test"] = Stub.Make<ITranslationService>((m, a) =>
            m.Name == "HasTranslation" ? true : m.ReturnType == typeof(string) && a.Length > 0 ? a[0] as string : null);
        Lang.ChangeLanguage("command-test");
        try
        {
            harmony.PatchAll(typeof(ItemKey).Assembly);
            Check(harmony.GetPatchedMethods().Count() >= 10, "all Harmony signatures apply to this game version");
            ReinforcementCommands();
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Lang.AvailableLanguages.Remove("command-test");
            Lang.ChangeLanguage(previousLocale);
        }
    }
}
public class Stub : DispatchProxy
{
    public System.Func<MethodInfo,object?[],object?> Handler = null!;
    public static T Make<T>(System.Func<MethodInfo,object?[],object?> handler) where T:class => (T)Make(typeof(T), handler);
    public static object Make(Type type, System.Func<MethodInfo,object?[],object?> handler)
    {
        object result = Create(type, typeof(Stub)); ((Stub)result).Handler = handler; return result;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        object? value = Handler(method!,args!);
        return value ?? (method!.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null);
    }
}
class TestPlayer : EntityPlayer
{
    public ItemSlot FileSlot = null!;
    public override ItemSlot LeftHandItemSlot => FileSlot;
}
class TestFile : ItemKeyFile
{
    public int Damage;
    public override void DamageItem(IWorldAccessor world, Vintagestory.API.Common.Entities.Entity byEntity, ItemSlot slot, int damage = 1
#if !VS121
        , bool destroyOnZeroDurability = true
#endif
        ) => Damage += damage;
}
// Inherits engine implementations of internal interface members; only public test state is substituted.
class TestServerPlayer : Vintagestory.Server.ServerPlayer, IServerPlayer
{
    public TestServerPlayer() : base(null!, null!) { }
    public EntityPlayer TestEntity = null!;
    public IPlayerInventoryManager TestManager = null!;
    public IWorldPlayerData TestWorldData = null!;
    EntityPlayer IPlayer.Entity => TestEntity;
    IPlayerInventoryManager IPlayer.InventoryManager => TestManager;
    IWorldPlayerData IPlayer.WorldData => TestWorldData;
    string IPlayer.PlayerUID => "test";
    public BlockSelection Selection = null!;
    BlockSelection IPlayer.CurrentBlockSelection => Selection;
}


class TestContainer : BlockEntityOpenableContainer
{
    public InventoryBase Storage = null!;
    public override InventoryBase Inventory => Storage;
    public override string InventoryClassName => "test";
    public override bool OnPlayerRightClick(IPlayer player, BlockSelection selection) => true;
}
