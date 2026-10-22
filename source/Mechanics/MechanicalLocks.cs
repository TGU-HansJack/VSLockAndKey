using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace VSLockAndKey;

/// <summary>All state changes run in server packet handlers; queries have no side effects.</summary>
public sealed class MechanicalLocks
{
    const string ChunkData = "vslockandkey:mechanical-v1";
    readonly ICoreAPI api;
    long revision = DateTime.UtcNow.Ticks;
    long lastRequest;
    string lastPosition = "";
    public MechanicalLocks(ICoreAPI api) { this.api = api; }
    public BlockPos Canonical(BlockPos input)
    {
        var pos = input.Copy();
        for (int i = 0; i < 8; i++)
        {
            if (api.World.BlockAccessor.GetBlock(pos) is not BlockMultiblock part) break;
            pos.Add(part.OffsetInv);
        }
        return pos;
    }
    static string PositionKey(BlockPos p) => $"{p.dimension}:{p.X}:{p.Y}:{p.Z}";
    Dictionary<string, InstalledLock> ReadChunk(BlockPos pos)
    {
        var bytes = api.World.BlockAccessor.GetChunkAtBlockPos(pos)?.GetModdata(ChunkData);
        if (bytes == null) return new();
        return JsonConvert.DeserializeObject<Dictionary<string, InstalledLock>>(Encoding.UTF8.GetString(bytes)) ?? new();
    }
    public InstalledLock? Get(BlockPos pos)
    {
        pos = Canonical(pos);
        var data = ReadChunk(pos);
        if (!data.TryGetValue(PositionKey(pos), out var record)) return null;
        return record.BlockCode == api.World.BlockAccessor.GetBlock(pos).Code?.ToString() ? record : null;
    }
    public bool IsLocked(BlockPos pos) => Get(pos)?.Locked == true;
    void Store(BlockPos pos, InstalledLock? state, bool broadcast)
    {
        var chunk = api.World.BlockAccessor.GetChunkAtBlockPos(pos);
        if (chunk == null) return;
        var data = ReadChunk(pos);
        if (state == null) data.Remove(PositionKey(pos)); else data[PositionKey(pos)] = state;
        chunk.SetModdata(ChunkData, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)));
        if (api.Side == EnumAppSide.Server) chunk.MarkModified();
        if (broadcast && api is ICoreServerAPI server) server.Network.GetChannel(VSLockAndKeyModSystem.NetworkChannelId).BroadcastPacket(Packet(pos, state));
    }
    static LockStatePacket Packet(BlockPos pos, InstalledLock? state) => new() { X = pos.X, Y = pos.Y, Z = pos.Z, Dimension = pos.dimension, Json = JsonConvert.SerializeObject(state) };
    public void Receive(LockStatePacket packet) => Store(new BlockPos(packet.X, packet.Y, packet.Z, packet.Dimension), JsonConvert.DeserializeObject<InstalledLock>(packet.Json), false);
    public void Remove(BlockPos pos)
    {
        // Parts may be rebuilt by door placement. Only removal of the controller destroys its lock.
        if (api.Side != EnumAppSide.Server || !pos.Equals(Canonical(pos))) return;
        if (ReadChunk(pos).ContainsKey(PositionKey(pos))) Store(pos, null, true);
    }
    public bool InReach(IPlayer player, BlockPos pos) => player.Entity.Pos.Dimension == pos.dimension
        && player.Entity.Pos.SquareDistanceTo(pos.X + .5, pos.Y + .5, pos.Z + .5) <= 36;
    public bool HeldFits(IPlayer player, InstalledLock state)
    {
        var stack = player.InventoryManager.ActiveHotbarSlot.Itemstack;
        if (stack == null) return false;
        if (stack.Collectible is ItemKey) return KeyProfile.IsFinished(stack) && (state.Profile == null || state.Profile.Matches(KeyProfile.Read(stack)));
        return state.Profile != null && stack.Collectible is ItemKeyring && ItemKeyring.GetContents(stack, api.World).Any(k => k != null && KeyProfile.IsFinished(k) && state.Profile.Matches(KeyProfile.Read(k)));
    }
    public EnumWorldAccessResponse TestAccess(IPlayer player, BlockSelection selection, EnumBlockAccessFlags flags, ref string claimant, EnumWorldAccessResponse response)
    {
        if (response != EnumWorldAccessResponse.LandClaimed || flags != EnumBlockAccessFlags.Use || selection == null || !player.Entity.Alive || player.WorldData.CurrentGameMode == EnumGameMode.Spectator) return response;
        var state = Get(selection.Position);
        if (state != null && (!state.Locked || (player.Entity.Controls.Sneak && HeldFits(player, state))))
        {
            // Vanilla returns LandClaimed before consulting CanUseBlock handlers.
            // Consult those handlers now so the local claim exception cannot skip another mod's veto.
            if (api.World is ServerMain server && !server.EventManager.TriggerCanUse((IServerPlayer)player, selection)) return EnumWorldAccessResponse.DeniedByMod;
            return EnumWorldAccessResponse.Granted;
        }
        return response;
    }
    public void Request(BlockPos input, bool query = false)
    {
        if (api is not ICoreClientAPI client) return;
        var pos = Canonical(input);
        if (!query)
        {
            string key = PositionKey(pos);
            if (lastPosition == key && api.World.ElapsedMilliseconds - lastRequest < 200) return;
            lastPosition = key; lastRequest = api.World.ElapsedMilliseconds;
        }
        client.Network.GetChannel(VSLockAndKeyModSystem.NetworkChannelId).SendPacket(new LockActionPacket { X = pos.X, Y = pos.Y, Z = pos.Z, Dimension = pos.dimension, Revision = Get(pos)?.Revision ?? 0, Query = query });
    }
    public void Notice(IServerPlayer player, string code) => ((ICoreServerAPI)api).Network.GetChannel(VSLockAndKeyModSystem.NetworkChannelId).SendPacket(new NoticePacket { Code = code }, player);
    public bool Intercept(IPlayer player, BlockSelection selection)
    {
        if (selection == null) return false;
        var held = player.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible;
        bool recognized = held is ItemPadlock || (player.Entity.Controls.Sneak && (held is ItemKey || held is ItemKeyring || (held is ItemPlumbAndSquare && Get(selection.Position) != null)));
        if (!recognized) return false;
        if (api.Side == EnumAppSide.Client) Request(selection.Position);
        return true;
    }
    public void Handle(IServerPlayer player, LockActionPacket packet)
    {
        var requested = new BlockPos(packet.X, packet.Y, packet.Z, packet.Dimension);
        if (!player.Entity.Alive || player.WorldData.CurrentGameMode == EnumGameMode.Spectator || !InReach(player, requested)) return;
        var pos = Canonical(requested);
        if (!InReach(player, pos)) return;
        var block = api.World.BlockAccessor.GetBlock(pos);
        if (!block.HasBehavior<BlockBehaviorLockable>()) return;
        var state = Get(pos);
        var channel = ((ICoreServerAPI)api).Network.GetChannel(VSLockAndKeyModSystem.NetworkChannelId);
        if (packet.Query) { channel.SendPacket(Packet(pos, state), player); return; }
        // Re-derive the selected controller, held stack and action from current server state.
        var selection = player.CurrentBlockSelection;
        if (selection == null || !pos.Equals(Canonical(selection.Position))) return;
        if (packet.Revision != (state?.Revision ?? 0)) { channel.SendPacket(Packet(pos, state), player); Notice(player, "state-changed"); return; }
        var slot = player.InventoryManager.ActiveHotbarSlot;
        var stack = slot.Itemstack;
        if (stack == null) return;
        if (stack.Collectible is ItemPadlock)
        {
            if (!api.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak)) return;
            if (state != null) { Notice(player, "already-installed"); return; }
            // A vanilla story lock or door bar is intentionally outside this system.
            state = new InstalledLock { PadlockCode = stack.Collectible.Code.ToString(), BlockCode = block.Code.ToString(), Profile = ReadCylinder(stack) };
            if (state.Profile != null && !state.Profile.Valid) return;
            slot.TakeOut(1); slot.MarkDirty();
            Notice(player, "installed");
        }
        else if (!player.Entity.Controls.Sneak || state == null) return;
        else if (stack.Collectible is ItemPlumbAndSquare)
        {
            if (!api.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak)) return;
            if (state.Locked) { Notice(player, "detach-locked"); return; }
            var item = api.World.GetItem(new AssetLocation(state.PadlockCode));
            if (item == null) return;
            var drop = new ItemStack(item);
            if (state.Profile != null) WriteCylinder(drop, state.Profile);
            Store(pos, null, true);
            if (!player.InventoryManager.TryGiveItemstack(drop)) api.World.SpawnItemEntity(drop, player.Entity.Pos.XYZ);
            Sound(pos); Notice(player, "detached"); return;
        }
        else if (stack.Collectible is ItemKey || stack.Collectible is ItemKeyring)
        {
            if (!api.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.Use)) return;
            if (state.Profile == null)
            {
                if (stack.Collectible is not ItemKey || !KeyProfile.IsFinished(stack)) { Notice(player, "finished-key-required"); return; }
                state.Profile = KeyProfile.Read(stack).Copy();
                Notice(player, "configured");
            }
            else
            {
                if (!HeldFits(player, state)) { Notice(player, "wrong-key"); return; }
                if (!state.Locked && !CanLock(pos)) { Notice(player, "close-first"); return; }
                state.Locked = !state.Locked;
                Notice(player, state.Locked ? "locked" : "unlocked");
            }
        }
        else return;
        state.Revision = ++revision;
        Store(pos, state, true); Sound(pos);
    }
    bool CanLock(BlockPos pos)
    {
        var be = api.World.BlockAccessor.GetBlockEntity(pos);
        var door = be?.GetBehavior<BEBehaviorDoor>();
        if (door?.Opened == true || door?.LeftDoor?.Opened == true || door?.RightDoor?.Opened == true) return false;
        return be is not BlockEntityContainer container || container.Inventory.openedByPlayerGUIds.Count == 0;
    }
    void Sound(BlockPos pos) => api.World.PlaySoundAt(new AssetLocation("game:sounds/tool/reinforce"), pos.X + .5, pos.InternalY + .5, pos.Z + .5);
    public const string CylinderAttribute = "vlkCylinder";
    public static KeyProfile? ReadCylinder(ItemStack stack)
    {
        string json = stack.Attributes.GetString(CylinderAttribute, "");
        if (json.Length == 0) return null;
        try { return JsonConvert.DeserializeObject<KeyProfile>(json) ?? new KeyProfile { Version = -1 }; }
        catch (JsonException) { return new KeyProfile { Version = -1 }; }
    }
    public static void WriteCylinder(ItemStack stack, KeyProfile profile) => stack.Attributes.SetString(CylinderAttribute, JsonConvert.SerializeObject(profile));
}
