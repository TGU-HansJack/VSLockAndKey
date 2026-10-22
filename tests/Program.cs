using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VSLockAndKey;

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
        Profiles(); Geometry(); Persistence(); Filing(); LockLifecycle(); Patches();
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
        Check(!profile.File(-1) && !profile.File(8), "invalid tooth bounds");
        for (int i = 0; i < 4; i++) Check(profile.File(2), "remove remaining metal");
        Check(!profile.File(2) && profile.Heights[2] == 0, "cannot add metal or file below zero");
        Check(!new KeyProfile { Heights = new[] { 1 } }.Valid && !new KeyProfile { Version = 3 }.Valid && !new KeyProfile { Spec = "other" }.Matches(profile), "reject incompatible profiles");
        var jeweled = new ItemStack(new ItemKey { ItemId = 2, Code = new AssetLocation("vslockandkey:key-jeweled-gold-ruby") });
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
        var restored = VSLockAndKeyModSystem.UnpackStack(VSLockAndKeyModSystem.PackStack(blank), world);
        Check(KeyProfile.IsFinished(restored) && KeyProfile.Read(restored).Matches(KeyProfile.Read(blank)), "network/save stack roundtrip");
    }
    static void Geometry()
    {
        foreach (string name in new[] { "key", "key-jeweled" })
        {
            var template = JsonConvert.DeserializeObject<Shape>(File.ReadAllText($"resources/assets/vslockandkey/shapes/item/{name}.json"))!;
            var item = new ItemKey();
            typeof(ItemKey).GetField("template", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(item,template);
            var stack = new ItemStack(item);
            var profile = new KeyProfile { Heights=new[]{0,1,2,3,4,5,0,5} }; profile.Write(stack,true);
            var blade = (Shape)typeof(ItemKey).GetMethod("Blade", BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(item,new object[]{stack})!;
            var teeth = blade.Elements.Single(e=>e.Name=="Teeth").Children!;
            Check(teeth.Length==8, name+" has eight physical blade sections");
            Check(teeth.Select((e,i)=>Math.Abs(e.From![1]-(1-(.125+.175*profile.Heights[i])))<.00001).All(x=>x), name+" mesh geometry follows actual cuts");
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
        var mod = new VSLockAndKeyModSystem();
        typeof(VSLockAndKeyModSystem).GetField("<Api>k__BackingField", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(mod, api);
        typeof(VSLockAndKeyModSystem).GetField("sapi", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(mod, api);
        var handler = typeof(VSLockAndKeyModSystem).GetMethod("OnFiling", BindingFlags.Instance|BindingFlags.NonPublic)!;
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
        var entity = new TestPlayer(); entity.Controls.Sneak = true;
        player.TestEntity=entity; player.TestManager=manager; player.TestWorldData=worldData;
        player.Selection = new BlockSelection { Position=pos };
        var mod = new VSLockAndKeyModSystem();
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
        var keyItem = new ItemKey { ItemId=11, Code=new AssetLocation("vslockandkey:key-copper") };
        var key2Item = new ItemKey { ItemId=12, Code=new AssetLocation("vslockandkey:key-jeweled-gold-ruby") };
        var ringItem = new ItemKeyring { ItemId=13, Code=new AssetLocation("vslockandkey:keyring-rope") };
        ringItem.VariantStrict["material"]="rope";
        ICoreServerAPI api = null!;
        var world = Stub.Make<IServerWorldAccessor>((m,a) => m.Name switch
        {
            "get_BlockAccessor"=>accessor, "get_Claims"=>claims, "get_Api"=>api,
            "GetItem"=>a[0] is AssetLocation ? padlockItem : (int)a[0]! == 11 ? keyItem : key2Item, _=>null
        });
        var channel = Stub.Make<IServerNetworkChannel>((m,a) => null);
        var network = Stub.Make<IServerNetworkAPI>((m,a) => m.Name == "GetChannel" ? channel : null);
        api = Stub.Make<ICoreServerAPI>((m,a) => m.Name switch { "get_World"=>world, "get_Network"=>network, "get_Side"=>EnumAppSide.Server, "get_ModLoader"=>loader, _=>null });
        var locks = new MechanicalLocks(api);
        typeof(VSLockAndKeyModSystem).GetField("<Locks>k__BackingField", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(mod, locks);
        void Turn(long? revision = null) => locks.Handle(player, new LockActionPacket { X=pos.X,Y=pos.Y,Z=pos.Z,Dimension=pos.dimension,Revision=revision??locks.Get(pos)?.Revision??0 });
        slot.Itemstack=new ItemStack(padlockItem); build=false; Turn();
        Check(locks.Get(pos)==null && !slot.Empty, "claim BuildOrBreak denial prevents installation");
        build=true; Turn();
        Check(slot.Empty && locks.Get(pos) is { Locked:false,Profile:null }, "install consumes padlock and leaves unfitted unlocked cylinder");
        var key=new ItemStack(keyItem); var profile=new KeyProfile(); profile.File(3); profile.Write(key,true);
        slot.Itemstack=key; Turn();
        Check(locks.Get(pos)!.Profile!.Matches(profile) && !locks.IsLocked(pos), "first finished key configures once without locking");
        Turn(0); Check(!locks.IsLocked(pos), "stale lock action cannot change state");
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
        Check(!MechanicalInventoryAccess.Allowed(inventory,player,true), "unopened inventory cannot transfer");
        inventory.openedByPlayerGUIds.Add("test");
        Check(MechanicalInventoryAccess.Allowed(inventory,player,true), "unlocked nearby opened inventory can transfer");
        mode=EnumGameMode.Spectator;
        Check(!MechanicalInventoryAccess.Allowed(inventory,player,true) && !ContainerPacketPatch.Prefix(container,player,1000), "spectator cannot access unlocked mechanical container through either packet path");
        mode=EnumGameMode.Survival;
        inventory.openedByPlayerGUIds.Clear();
        var ring=new ItemStack(ringItem); ItemKeyring.SetContents(ring,new ItemStack?[]{key,null}); slot.Itemstack=ring; Turn();
        Check(locks.IsLocked(pos), "held keyring tries matching finished keys");
        entity.Pos.X=100; Turn(); Check(locks.IsLocked(pos), "out of reach lock request rejected"); entity.Pos.X=0;
        mode=EnumGameMode.Spectator; Turn(); Check(locks.IsLocked(pos), "spectator cannot turn lock"); mode=EnumGameMode.Survival;
        string claimant="someone"; var selection=new BlockSelection { Position=pos };
        Check(locks.TestAccess(player,selection,EnumBlockAccessFlags.Use,ref claimant,EnumWorldAccessResponse.LandClaimed)==EnumWorldAccessResponse.Granted, "held matching shape permits exact Use in claim");
        Check(locks.TestAccess(player,selection,EnumBlockAccessFlags.BuildOrBreak,ref claimant,EnumWorldAccessResponse.LandClaimed)==EnumWorldAccessResponse.LandClaimed, "shape never grants BuildOrBreak");
        Check(locks.TestAccess(player,selection,EnumBlockAccessFlags.Use,ref claimant,EnumWorldAccessResponse.DeniedByMod)==EnumWorldAccessResponse.DeniedByMod, "existing mod veto preserved");
        var tool=new ItemStack(new ItemPlumbAndSquare { ItemId=14 }); slot.Itemstack=tool; Turn();
        Check(locks.Get(pos)!=null, "locked padlock cannot be detached");
        slot.Itemstack=key; Turn(); slot.Itemstack=tool; build=false; Turn();
        Check(locks.Get(pos)!=null, "detach respects BuildOrBreak");
        build=true; Turn(); Check(locks.Get(pos)==null, "unlocked padlock can detach");
        // Reinstalling uses the same persisted cylinder attribute as the detached item.
        var moved=new ItemStack(padlockItem); MechanicalLocks.WriteCylinder(moved,profile); slot.Itemstack=moved; Turn();
        Check(locks.Get(pos)!.Profile!.Matches(profile) && !locks.IsLocked(pos), "reinstalled padlock retains cylinder and starts unlocked");
    }
    static void Patches()
    {
        var harmony = new Harmony("vslockandkey.tests");
        try
        {
            harmony.PatchAll(typeof(ItemKey).Assembly);
            Check(harmony.GetPatchedMethods().Count() >= 10, "all Harmony signatures apply to this game version");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
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
