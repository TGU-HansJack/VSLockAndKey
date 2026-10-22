using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
namespace VSLockAndKey;

[HarmonyPatch(typeof(ModSystemBlockReinforcement), nameof(ModSystemBlockReinforcement.IsLockedForInteract))]
public static class IsLockedForInteractPatch
{
    public static bool Prefix(BlockPos pos, IPlayer forPlayer, ref bool __result)
    {
        if (forPlayer == null) return true;
        var state = VSLockAndKeyModSystem.Get(forPlayer.Entity.World).Locks.Get(pos);
        if (state == null) return true;
        __result = state.Locked;
        return false;
    }
}
[HarmonyPatch(typeof(ItemPadlock), nameof(ItemPadlock.OnHeldInteractStart))]
public static class PadlockInteractionPatch
{
    public static bool Prefix(EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handling)
    {
        if (blockSel == null || byEntity is not EntityPlayer player) return true;
        VSLockAndKeyModSystem.Get(byEntity.World).Locks.Intercept(player.Player, blockSel);
        handling = EnumHandHandling.PreventDefault;
        return false;
    }
}
[HarmonyPatch(typeof(ItemPlumbAndSquare), nameof(ItemPlumbAndSquare.OnHeldInteractStart))]
public static class DetachInteractionPatch
{
    public static bool Prefix(EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handling)
    {
        if (blockSel == null || byEntity is not EntityPlayer player) return true;
        if (!VSLockAndKeyModSystem.Get(byEntity.World).Locks.Intercept(player.Player, blockSel)) return true;
        handling = EnumHandHandling.PreventDefault;
        return false;
    }
}
[HarmonyPatch(typeof(ItemPlumbAndSquare), nameof(ItemPlumbAndSquare.OnHeldAttackStart))]
public static class ReinforcementRemovalPatch
{
    public static bool Prefix(EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handling)
    {
        if (blockSel == null || VSLockAndKeyModSystem.Get(byEntity.World).Locks.Get(blockSel.Position) == null) return true;
        handling = EnumHandHandling.PreventDefault;
        return false;
    }
}
[HarmonyPatch(typeof(BEBehaviorDoor), nameof(BEBehaviorDoor.ToggleDoorState))]
public static class DoorTogglePatch
{
    public static bool Prefix(BEBehaviorDoor __instance, IPlayer byPlayer)
    {
        var locks = VSLockAndKeyModSystem.Get(__instance.Api.World).Locks;
        if (locks.IsLocked(__instance.Pos)) return false;
        foreach (var companion in new[] { __instance.LeftDoor, __instance.RightDoor })
        {
            if (companion == null) continue;
            if (locks.IsLocked(companion.Pos)) return false;
            if (locks.Get(__instance.Pos) != null || locks.Get(companion.Pos) != null)
            {
                if (companion.StoryLockedCode != null || companion.Blockentity.GetBehavior<BEBehaviorDoorBarLock>()?.IsLocked == true) return false;
                if (byPlayer != null && !__instance.Api.World.Claims.TryAccess(byPlayer, companion.Pos, EnumBlockAccessFlags.Use)) return false;
            }
        }
        return true;
    }
}
[HarmonyPatch(typeof(BlockEntityOpenableContainer), nameof(BlockEntityOpenableContainer.OnReceivedClientPacket))]
public static class ContainerPacketPatch
{
    public static bool Prefix(BlockEntityOpenableContainer __instance, IPlayer player, int packetid)
    {
        if (packetid == 1001) return true;
        var locks = VSLockAndKeyModSystem.Get(__instance.Api.World).Locks;
        if (locks.Get(__instance.Pos) == null) return true;
        return player.Entity.Alive && player.WorldData.CurrentGameMode != EnumGameMode.Spectator
            && locks.InReach(player, __instance.Pos) && !locks.IsLocked(__instance.Pos)
            && (packetid >= 1000 || __instance.Inventory.HasOpened(player));
    }
}
// Some block subclasses remove themselves without dispatching behavior callbacks.
[HarmonyPatch(typeof(Block), nameof(Block.OnBlockRemoved))]
public static class RemovedBlockPatch
{
    public static void Prefix(IWorldAccessor world, BlockPos pos) => VSLockAndKeyModSystem.Get(world).Locks.Remove(pos);
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetHeldItemInfo))]
public static class PadlockInfoPatch
{
    public static void Postfix(CollectibleObject __instance, ItemSlot inSlot, System.Text.StringBuilder dsc)
    {
        if (__instance is ItemPadlock && inSlot.Itemstack != null)
            dsc.AppendLine(Vintagestory.API.Config.Lang.Get("vslockandkey:padlock-" + (MechanicalLocks.ReadCylinder(inSlot.Itemstack) == null ? "new" : "configured")));
    }
}

// The inventory transport can send moves directly, without a block interaction packet.
public static class MechanicalInventoryAccess
{
    public static bool Allowed(InventoryBase? inventory, IPlayer player, bool requireOpen)
    {
        if (inventory == null) return true;
        if (inventory is KeyringInventory ring) return ring.CanPlayerAccess(player, player.Entity.Pos) && (!requireOpen || ring.HasOpened(player));
        if (inventory.Pos == null || inventory.Api == null) return true;
        var locks = VSLockAndKeyModSystem.Get(inventory.Api.World).Locks;
        var state = locks.Get(inventory.Pos);
        if (state == null) return true;
        return player.Entity.Alive && player.WorldData.CurrentGameMode != EnumGameMode.Spectator
            && !state.Locked && locks.InReach(player, inventory.Pos)
            && (!requireOpen || inventory.HasOpened(player))
            && inventory.Api.World.Claims.TryAccess(player, inventory.Pos, EnumBlockAccessFlags.Use);
    }
}
[HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.CanPlayerAccess))]
public static class InventoryAccessPatch
{
    public static void Postfix(InventoryBase __instance, IPlayer player, ref bool __result)
    {
        if (__result) __result = MechanicalInventoryAccess.Allowed(__instance, player, false);
    }
}
[HarmonyPatch(typeof(Vintagestory.Common.InventoryNetworkUtil), nameof(Vintagestory.Common.InventoryNetworkUtil.HandleClientPacket), new[] { typeof(IPlayer), typeof(int), typeof(Packet_Client) })]
public static class InventoryTransportPatch
{
    public static bool Prefix(InventoryBase ___inv, IPlayer byPlayer, int packetId, Packet_Client packet)
    {
        if (!MechanicalInventoryAccess.Allowed(___inv, byPlayer, true)) return false;
        string? source = packetId == 8 ? packet.MoveItemstack?.SourceInventoryId : packetId == 9 ? packet.Flipitemstacks?.SourceInventoryId : null;
        string? target = packetId == 8 ? packet.MoveItemstack?.TargetInventoryId : packetId == 9 ? packet.Flipitemstacks?.TargetInventoryId : null;
        return (source == null || MechanicalInventoryAccess.Allowed(byPlayer.InventoryManager.GetInventory(source) as InventoryBase, byPlayer, true))
            && (target == null || MechanicalInventoryAccess.Allowed(byPlayer.InventoryManager.GetInventory(target) as InventoryBase, byPlayer, true));
    }
}
