using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
namespace VSLockAndKey;
public sealed class BlockBehaviorMechanicalLock : BlockBehavior
{
    public BlockBehaviorMechanicalLock(Block block) : base(block) { }
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection selection, ref EnumHandling handling)
    {
        var locks = VSLockAndKeyModSystem.Get(world).Locks;
        if (locks.Intercept(byPlayer, selection)) { handling = EnumHandling.PreventSubsequent; return true; }
        if (locks.IsLocked(selection.Position)) { handling = EnumHandling.PreventSubsequent; return false; }
        return base.OnBlockInteractStart(world, byPlayer, selection, ref handling);
    }
    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling) => VSLockAndKeyModSystem.Get(world).Locks.Remove(pos);
    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer player)
    {
        var state = VSLockAndKeyModSystem.Get(world).Locks.Get(pos);
        return state == null ? "" : Lang.Get("vslockandkey:status-" + (state.Profile == null ? "unconfigured" : state.Locked ? "locked" : "unlocked"));
    }
}
