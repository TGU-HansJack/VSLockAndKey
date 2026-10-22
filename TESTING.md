# Verification / 验证

## Automated checks

The console harness uses the Vintage Story 1.22.5 assemblies and the built mod. Build Debug first, then pass the game installation directory to the runner:

```powershell
dotnet build VSMechanicalLocks.csproj
dotnet run --project tests/Mechanical.Tests.csproj -- "C:/Games/VintageStory"
```

The 1.22.5 build passed 61 assertions during this fork update. Coverage includes key profiles, shape matching and rendering geometry; serialization and chunk persistence; filing timing, durability and stale requests; install/configure/toggle/detach; container viewers; held keyrings; claim scope; spectator and distance restrictions; inventory and block-container guards; and Harmony patch signatures.

Tests substitute world, player and network interfaces while exercising production handlers and game serialization. They do not launch the game or render the native GUI.

## In-game acceptance / 游戏内验收

The following checks have not been run in a live Vintage Story 1.22.x session:

- Cast single and bulk blanks; check molds, output quantity and smithing recipes.
- File different teeth, release to cancel, reach minimum height, break the file on its last durability point, and finish the key. Check GUI at different scales in English and Chinese.
- Compare ordinary and jeweled key shape in inventory, first/third-person view, dropped items, storage, and the filing preview.
- Check matching and non-matching keys across materials and jewel variants, and require re-finishing after another cut.
- Install and configure a padlock, toggle it with matching keys, and verify held keyrings work while unrelated inventory keys do not.
- Verify open doors, linked door wings, and viewed containers cannot be locked. Check story locks and door bars.
- Verify another player with a matching key can operate the lock; check state after reconnect, server restart and chunk reload.
- Check claims, lock removal, reinforcement, destruction/replacement, and keyring contents across switching items, death, disconnect and reopening.

Multiplayer event ordering, native GUI rendering and actual asset presentation remain unverified in a running game.
