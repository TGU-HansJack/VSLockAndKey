namespace VSMechanicalLocks;
public class ModConfig
{
    public bool EnableReinforcementPermissionCommands = false;
    public Dictionary<string, int> KeyringSlotsByMaterial = DefaultKeyringSlotsByMaterial();
    public int DefaultKeyringSlots = 4;
    public static Dictionary<string, int> DefaultKeyringSlotsByMaterial() => new()
    {
        ["rope"] = 2, ["tinbronze"] = 3, ["bismuthbronze"] = 3, ["blackbronze"] = 3,
        ["iron"] = 4, ["meteoriciron"] = 5, ["steel"] = 6
    };
}
