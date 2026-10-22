using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VSMechanicalLocks;

// Gate only the vanilla authorization handlers; reinforcement startup and storage stay active.
[HarmonyPatch]
public static class ReinforcementPermissionCommandsPatch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[]
        {
            "OnCmdGrant", "OnCmdRevoke", "OnCmdGrantGroup", "OnCmdRevokeGroup",
            "OnCmdGroupGrant", "OnCmdGroupRevoke", "OnCmdGroupGrantGroup", "OnCmdGroupRevokeGroup"
        })
            yield return AccessTools.DeclaredMethod(typeof(ModSystemBlockReinforcement), name);
    }

    public static bool Prefix(ICoreAPI ___api, ref TextCommandResult __result)
    {
        if (___api.ModLoader.GetModSystem<VSMechanicalLocksModSystem>().Settings.EnableReinforcementPermissionCommands)
            return true;

        __result = TextCommandResult.Error("vsmechanicallocks:reinforcement-permission-commands-disabled");
        return false;
    }
}
