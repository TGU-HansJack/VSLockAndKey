using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
namespace VSMechanicalLocks;

public class ItemKey : Item, IContainedMeshSource
{
    readonly Dictionary<string, MultiTextureMeshRef> meshes = new();
    Shape? template;
    readonly object shapeLock = new();
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (blockSel != null && byEntity is EntityPlayer player && VSMechanicalLocksModSystem.Get(byEntity.World).Locks.Intercept(player.Player, blockSel))
        { handling = EnumHandHandling.PreventDefault; return; }
        if (blockSel == null && byEntity.LeftHandItemSlot.Itemstack?.Collectible is ItemKeyFile)
        {
            handling = EnumHandHandling.PreventDefault;
            if (byEntity.World.Api is ICoreClientAPI client) client.Network.GetChannel(VSMechanicalLocksModSystem.NetworkChannelId).SendPacket(new FilingPacket { Action = "open" });
            return;
        }
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }
    public override void GetHeldItemInfo(ItemSlot slot, StringBuilder text, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(slot, text, world, withDebugInfo);
        var profile = KeyProfile.Read(slot.Itemstack!);
        text.AppendLine(Lang.Get("vsmechanicallocks:key-" + (!profile.Valid ? "invalid" : !profile.Cut ? "blank" : KeyProfile.IsFinished(slot.Itemstack!) ? "finished" : "in-progress")));
        text.AppendLine(Lang.Get("vsmechanicallocks:key-help"));
    }
#if VS121
    public override void OnCreatedByCrafting(ItemSlot[] inputs, ItemSlot outputSlot, GridRecipe recipe)
#else
    public override void OnCreatedByCrafting(ItemSlot[] inputs, ItemSlot outputSlot, IRecipeBase recipe)
#endif
    {
        base.OnCreatedByCrafting(inputs, outputSlot, recipe);
        var input = inputs.FirstOrDefault(s => s.Itemstack?.Collectible is ItemKey)?.Itemstack;
        if (input != null) KeyProfile.Read(input).Write(outputSlot.Itemstack!, KeyProfile.IsFinished(input));
    }
    Shape Blade(ItemStack stack)
    {
        // Never mutate the shared asset or another thread's shape.
        lock (shapeLock)
        {
            template ??= api.Assets.TryGet(Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
            var shape = template.Clone();
            var profile = KeyProfile.Read(stack);
            var heights = profile.Valid ? profile.Heights : new KeyProfile().Heights;
            foreach (var root in shape.Elements)
                if (root.Name == "Teeth")
                    for (int i = 0; i < 8; i++) root.Children![i].From![1] = 1 - (.125 + .175 * heights[i]);
            return shape;
        }
    }
    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack stack, EnumItemRenderTarget target, ref ItemRenderInfo info)
    {
        base.OnBeforeRender(capi, stack, target, ref info);
        string key = KeyProfile.Read(stack).MeshKey;
        if (!meshes.TryGetValue(key, out var mesh))
        {
            // Bound GPU memory. Old shapes are rebuilt if needed after eviction.
            if (meshes.Count >= 128) { var first = meshes.First(); first.Value.Dispose(); meshes.Remove(first.Key); }
            capi.Tesselator.TesselateShape(this, Blade(stack), out var data);
            meshes[key] = mesh = capi.Render.UploadMultiTextureMesh(data);
        }
        info.ModelRef = mesh;
    }
#if VS121
    public MeshData GenMesh(ItemStack stack, ITextureAtlasAPI atlas, BlockPos atBlockPos) => GenBladeMesh(stack, atlas);
    public string GetMeshCacheKey(ItemStack stack) => Code + ":" + KeyProfile.Read(stack).MeshKey;
#else
    public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI atlas, BlockPos atBlockPos) => GenBladeMesh(slot.Itemstack!, atlas);
    public string GetMeshCacheKey(ItemSlot slot) => Code + ":" + KeyProfile.Read(slot.Itemstack!).MeshKey;
#endif
    MeshData GenBladeMesh(ItemStack stack, ITextureAtlasAPI atlas)
    {
        var capi = (ICoreClientAPI)api;
        capi.Tesselator.TesselateShape(Code.ToString(), Blade(stack), out var data, new AtlasTextures(capi, this, atlas));
        return data;
    }
    public override void OnUnloaded(ICoreAPI api)
    {
        foreach (var mesh in meshes.Values) mesh.Dispose();
        meshes.Clear(); template = null; base.OnUnloaded(api);
    }
    sealed class AtlasTextures : ITexPositionSource
    {
        readonly Dictionary<string, TextureAtlasPosition> positions = new();
        public Size2i AtlasSize { get; }
        public TextureAtlasPosition this[string code] => positions[code];
        public AtlasTextures(ICoreClientAPI api, Item item, ITextureAtlasAPI atlas)
        {
            AtlasSize = atlas.Size;
            foreach (var pair in item.Textures)
            {
                pair.Value.Bake(api.Assets);
                atlas.GetOrInsertTexture(pair.Value.Baked.BakedName, out _, out var pos);
                positions[pair.Key] = pos;
            }
        }
    }
}
