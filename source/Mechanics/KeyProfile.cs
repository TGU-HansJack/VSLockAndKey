using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VSLockAndKey;

/// <summary>Physical blade geometry. No identity or authorization data participates in matching.</summary>
public sealed class KeyProfile
{
    public const string Attribute = "vlkProfile";
    public const string FinishedAttribute = "vlkFinished";
    public int Version { get; set; } = 1;
    public string Spec { get; set; } = "standard-8";
    public int[] Heights { get; set; } = Enumerable.Repeat(5, 8).ToArray();
    public bool Valid => Version == 1 && Spec == "standard-8" && Heights is { Length: 8 } && Heights.All(h => h >= 0 && h <= 5);
    public bool Cut => Valid && Heights.Any(h => h < 5);
    public bool Matches(KeyProfile? other) => Valid && other?.Valid == true && Version == other.Version && Spec == other.Spec && Heights.SequenceEqual(other.Heights);
    public KeyProfile Copy() => new() { Version = Version, Spec = Spec, Heights = (int[])Heights.Clone() };
    public bool File(int tooth)
    {
        if (!Valid || tooth < 0 || tooth >= 8 || Heights[tooth] == 0) return false;
        Heights[tooth]--;
        return true;
    }
    public static KeyProfile Read(ItemStack stack)
    {
        var tree = stack.Attributes.GetTreeAttribute(Attribute);
        if (tree == null) return new(); // Cast and forged keys are solid blanks.
        return new() { Version = tree.GetInt("version"), Spec = tree.GetString("spec", ""), Heights = (tree["heights"] as IntArrayAttribute)?.value ?? Array.Empty<int>() };
    }
    public void Write(ItemStack stack, bool finished)
    {
        if (!Valid) throw new ArgumentException("Invalid key profile");
        var tree = new TreeAttribute();
        tree.SetInt("version", Version);
        tree.SetString("spec", Spec);
        tree["heights"] = new IntArrayAttribute((int[])Heights.Clone());
        stack.Attributes[Attribute] = tree;
        stack.Attributes.SetBool(FinishedAttribute, finished && Cut);
    }
    public static bool IsFinished(ItemStack stack) => stack.Collectible is ItemKey && Read(stack).Cut && stack.Attributes.GetBool(FinishedAttribute);
    public string MeshKey => Valid ? string.Join("", Heights) : "invalid";
}

public sealed class InstalledLock
{
    public string PadlockCode { get; set; } = "";
    public string BlockCode { get; set; } = "";
    public KeyProfile? Profile { get; set; }
    public bool Locked { get; set; }
    public long Revision { get; set; }
}
