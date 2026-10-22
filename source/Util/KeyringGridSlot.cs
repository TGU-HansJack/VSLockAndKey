using Vintagestory.API.Common;
namespace VSMechanicalLocks;
/// <summary>Writes each accepted transfer immediately to the original physical keyring.</summary>
public class KeyringGridSlot : ItemSlotSurvival
{
    readonly ItemSlot? backingSlot;
    readonly ItemStack? backingStack;
    readonly int index;
    bool Valid => Inventory is not KeyringInventory inv || inv.HeldValid;
    public KeyringGridSlot(InventoryGeneric inventory, int index, ItemSlot? backingSlot) : base(inventory)
    {
        this.index = index; this.backingSlot = backingSlot; backingStack = backingSlot?.Itemstack;
    }
    public override bool CanHold(ItemSlot sourceSlot) => Valid && sourceSlot?.Itemstack?.Collectible is ItemKey && base.CanHold(sourceSlot);
    public override bool CanTake() => Valid && base.CanTake();
    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) => Valid && sourceSlot?.Itemstack?.Collectible is ItemKey && base.CanTakeFrom(sourceSlot, priority);
    public override void OnItemSlotModified(ItemStack sinkStack)
    {
        base.OnItemSlotModified(sinkStack);
        if (backingSlot == null || backingStack == null || !Valid) return;
        var contents = ItemKeyring.GetContents(backingStack, Inventory.Api.World);
        if (index < contents.Length)
        {
            contents[index] = Itemstack;
            ItemKeyring.SetContents(backingStack, contents);
        }
        backingSlot.MarkDirty();
    }
}
