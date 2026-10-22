using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
namespace VSLockAndKey;
/// <summary>Transport session access, unrelated to lock matching or lock ownership.</summary>
public sealed class KeyringInventory : InventoryGeneric
{
    public IPlayer Owner { get; }
    readonly ItemSlot backingSlot;
    readonly ItemStack backingStack;
    bool invalidated;
    public bool HeldValid
    {
        get
        {
            if (!Owner.Entity.Alive || Owner.WorldData.CurrentGameMode == EnumGameMode.Spectator || !ReferenceEquals(Owner.InventoryManager.ActiveHotbarSlot, backingSlot) || !ReferenceEquals(backingSlot.Itemstack, backingStack)) invalidated = true;
            return !invalidated;
        }
    }
    public KeyringInventory(int count, string id, ICoreAPI api, IPlayer player, ItemSlot slot)
        : base(count, id, api, (i, inv) => new KeyringGridSlot(inv, i, slot))
    { Owner = player; backingSlot = slot; backingStack = slot.Itemstack!; }
    public override bool CanPlayerAccess(IPlayer player, EntityPos position) => player.PlayerUID == Owner.PlayerUID && HeldValid;
}
