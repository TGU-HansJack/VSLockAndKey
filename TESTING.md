# Verification / 验证

## Automated checks

The console harness uses the Vintage Story 1.22.7 assemblies and the built mod. Build Debug first, then pass the game installation directory to the runner:

```powershell
dotnet build VSMechanicalLocks.csproj
dotnet run --project tests/Mechanical.Tests.csproj -- "C:/Games/VintageStory"
```

The 1.22.7 build passed 138 assertions during this fork update. Coverage includes key profiles, legacy migration, two-layer shape matching and rendering geometry; serialization and chunk persistence; filing timing, durability and stale requests; install/configure/toggle/detach; Ctrl-gated lock actions; container viewers; held keyrings; claim scope; spectator and distance restrictions; inventory and block-container guards; Harmony patch signatures; the default-off reinforcement authorization switch, all eight blocked vanilla handlers preserving grants, and enabled vanilla grant/revoke behavior; and Chinese/English tooltip wrapping at four widths using the game's rich-text layout and native Cairo font measurements.

Tests substitute world, player and network interfaces while exercising production handlers and game serialization. Tooltip checks use the game's Cairo library from its Lib directory and verify complete text and consecutive rows, including the boundary after base item information. They do not launch the game or render the full GUI.

## In-game acceptance / 游戏内验收

The following checks have not been run in a live Vintage Story 1.22.x session:

- Cast single and bulk blanks; check molds, output quantity and smithing recipes.
- File different teeth on both blade layers, release to cancel, reach minimum height, break the file on its last durability point, and finish the key. Check GUI at different scales in English and Chinese; verify total blade thickness is unchanged.
- Verify the filing status fits one line above the bottom buttons with compact bottom padding. Check key, keyring and padlock hover descriptions for missing characters or blank rows at different GUI scales, including with the Chinese line-break mod installed.
- Compare ordinary and jeweled key shape in inventory, first/third-person view, dropped items, storage, and the filing preview.
- Check matching and non-matching keys across materials and jewel variants, and require re-finishing after another cut.
- Install and configure a padlock, toggle it with Ctrl + right-click using matching keys, verify ordinary right-click still opens doors, and check held keyrings while unrelated inventory keys do not.
- Verify keys and keyrings place in ground storage with sneak + right-click, and right-clicking a held keyring opens its inventory.
- Verify open doors, linked door wings, and viewed containers cannot be locked. Check story locks and door bars.
- Verify another player with a matching key can operate the lock; check state after reconnect, server restart and chunk reload.
- Check claims, lock removal, reinforcement, destruction/replacement, and keyring contents across switching items, death, disconnect and reopening.
- With the default config, verify `/bre` and `/gbre` grant/revoke/grantgroup/revokegroup report disabled; confirm ordinary reinforcement still works. Set `EnableReinforcementPermissionCommands` to `true`, restart the server, and verify vanilla player/group authorization commands work again.

Multiplayer event ordering, native GUI rendering and actual asset presentation remain unverified in a running game.
