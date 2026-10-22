# Mechanical Locks & Keys

Release notes / 更新日志：[CHANGELOG.md](CHANGELOG.md)

Mechanical Locks & Keys is a Vintage Story fork of Arkzenir's Lock & Key mod. It replaces identity-bound locks with physical key profiles: anyone holding a finished key whose filed blade matches the cylinder can operate that lock. The fork removes player/group ownership and permission-based key matching.

This fork uses mod ID `vsmechanicallocks`, version `1.0.2`, and targets the Vintage Story `1.22.x` series. It is built and automated-tested against `1.22.7`. The new mod ID, asset domain, and storage namespace mean old `vslockandkey` worlds and item codes are not migrated; use a new world.

## Making and using keys

1. Cast or smith a solid key blank.
2. Hold the key in the main hand and a file in the offhand, then right-click air. Drag the key preview left or right with the left mouse button to rotate it through 360°. Select the outer or inner blade layer, choose one of its eight teeth, and hold File. The selected tooth has a red cuboid outline on the 3D preview that follows its remaining metal and rotates with the key. Each one-second stroke removes one height step and costs one file durability. Cuts cannot be undone.
3. Make at least one cut and choose Finish. Further filing makes the key unfinished again. Jeweling preserves its profile; material and gems do not affect matching.
4. Install a vanilla padlock on a lockable door or container with a right-click. A new lock is unconfigured and unlocked.
5. Hold Ctrl and right-click an unconfigured lock with a finished individual key to fit its cylinder. The lock remains unlocked.
6. Hold Ctrl and right-click with a matching key to toggle the lock, leaving ordinary right-click free to open the door. A held keyring tries its stored keys; keys elsewhere in the inventory are not searched. Right-click the keyring in air to open it. Sneak + right-click on ordinary ground places ground-storable keys and keyrings. Close doors and all container viewers before locking.
7. Remove an unlocked lock with a plumb and square while sneaking and right-clicking. The cylinder profile is preserved. Normal claim BuildOrBreak access is required to install or remove locks.

Lock profiles and state persist with their chunk. Destroying or replacing the supporting block removes the lock without dropping an intact padlock. Reinforcement remains governed by the game. Story locks and door bars are left intact. See [DESIGN.md](DESIGN.md) for mechanics and persistence details.

## Configuration

The server (or singleplayer server) creates `ModConfig/vsmechanicallocks.json` in its game data directory. Existing configuration files gain the new option automatically:

```json
"EnableReinforcementPermissionCommands": false
```

By default, `/bre` and `/gbre` authorization subcommands (`grant`, `revoke`, `grantgroup`, `revokegroup`) return a disabled message. Set this option to `true` and restart the server to restore them. Reinforcing blocks, reinforcement strength, existing grants and the game's other permission systems are unaffected. Mechanical locks still require a matching physical key regardless of this option.

## Build and test

Copy `Properties/localSettings.props.template` to `Properties/localSettings.props` and set `GameDirectory_1_22_7` to a Vintage Story 1.22.7 installation. The project uses .NET 10.

```powershell
dotnet build VSMechanicalLocks.csproj
dotnet run --project tests/Mechanical.Tests.csproj -- "C:/Games/VintageStory"
dotnet build VSMechanicalLocks.csproj -c Release
```

The release build creates `Releases/vsmechanicallocks_1.0.2.zip`. Automated coverage and the in-game test checklist are in [TESTING.md](TESTING.md).

## Credits and license

This project is based on [Arkzenir's Lock & Key](https://github.com/Arkzenir/VSLockAndKey), listed on the [Vintage Story mods site](https://mods.vintagestory.at/locksandkeys). See [Arkzenir's Vintage Story profile](https://mods.vintagestory.at/show/user/6FFD71B4E8E7D178BE41). The upstream repository identifies Alper Sarı as the MIT copyright holder. This fork's mechanical-lock redesign, 1.22.x packaging, tests, and documentation are contributions by HansJack. See [NOTICE.md](NOTICE.md) and [LICENSE](LICENSE) for attribution and license terms.

# 中文

Mechanical Locks & Keys 是基于 Arkzenir 的 Lock & Key 制作的 Vintage Story Fork。它使用钥匙齿形匹配锁芯：任何人只要持有齿形吻合的完工钥匙，就能操作对应的锁。不再按玩家、群组身份或权限判断钥匙。

本 Fork 的模组 ID 为 `vsmechanicallocks`，模组版本为 `1.0.2`，面向 Vintage Story `1.22.x` 系列；当前使用 `1.22.7` 游戏程序集构建并通过自动测试。由于更换了模组 ID、资产域名和存档命名空间，旧 `vslockandkey` 世界及物品代码不会迁移，请使用新世界。

## 制作与使用

1. 浇筑或锻造实心钥匙坯。
2. 主手持钥匙、副手持锉刀，右键打开界面。在钥匙预览区域按住左键左右拖拽，可 360° 旋转查看模型。分别选择外层或内层，再选择该层八个齿位之一并按住锉削。选中的齿位在三维预览中显示红色立方体描边，贴合剩余金属并随钥匙旋转。每次一秒，移除一层金属并消耗一点锉刀耐久；锉掉的部分不能恢复。两层平分原钥匙厚度。
3. 至少锉削一次后点击“完工”。继续锉削会使钥匙重新变为未完工。镶嵌会保留齿形；材质和宝石不参与开锁匹配。
4. 手持原版挂锁右键可锁定的门或容器安装。新锁未配置且未上锁。
5. 按住 Ctrl，对准未配置的锁并手持完工钥匙右键来配置锁芯；配置后仍未上锁。
6. 按住 Ctrl，手持匹配钥匙对准并右键即可切换锁定状态；普通右键仍用于开门。手持钥匙环时会检查环内钥匙，不会搜索背包。指向空中右键钥匙环打开其物品栏，潜行右键可将钥匙或钥匙环放置在地面。上锁前请关闭门和所有容器界面。
7. 手持铅垂规潜行右键可以拆下未上锁的锁，并保留锁芯形状。安装和拆卸需要原版领地的 BuildOrBreak 权限。

锁芯形状和状态随区块保存。破坏或替换承载方块会移除锁记录，不掉落完整挂锁；加固仍由游戏原有规则管理。剧情锁和门闩保持原样。机制与持久化细节见 [DESIGN.md](DESIGN.md)。

## 配置

服务器（单人游戏为本地服务器）启动后，会在游戏数据目录生成 `ModConfig/vsmechanicallocks.json`；已有配置文件会自动补入新选项：

```json
"EnableReinforcementPermissionCommands": false
```

默认关闭 `/bre`、`/gbre` 的 `grant`、`revoke`、`grantgroup`、`revokegroup` 授权子命令，执行时提示已关闭。改为 `true` 并重启服务器可恢复这些指令。方块加固、加固强度、已有授权记录和游戏其他权限机制保持原有规则；无论此选项如何设置，机械锁仍按实体钥匙齿形匹配。

## 构建与测试

将 `Properties/localSettings.props.template` 复制为 `Properties/localSettings.props`，并设置 `GameDirectory_1_22_7` 为 Vintage Story 1.22.7 安装路径。项目使用 .NET 10。

```powershell
dotnet build VSMechanicalLocks.csproj
dotnet run --project tests/Mechanical.Tests.csproj -- "C:/Games/VintageStory"
dotnet build VSMechanicalLocks.csproj -c Release
```

发布包为 `Releases/vsmechanicallocks_1.0.2.zip`。自动测试结果和游戏内验收清单见 [TESTING.md](TESTING.md)。

## 致谢与许可

本项目基于 [Arkzenir 的 Lock & Key](https://github.com/Arkzenir/VSLockAndKey)，原模组也发布在 [Vintage Story 模组网站](https://mods.vintagestory.at/locksandkeys)。另见 [Arkzenir 的 Vintage Story 作者主页](https://mods.vintagestory.at/show/user/6FFD71B4E8E7D178BE41)。上游仓库的 MIT 版权声明归 Alper Sarı 所有。本 Fork 的机械锁改造、1.22.x 打包、测试和文档由 HansJack 贡献。署名和许可条款见 [NOTICE.md](NOTICE.md) 与 [LICENSE](LICENSE)。
