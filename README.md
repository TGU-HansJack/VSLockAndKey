# Mechanical Locks & Keys

Mechanical Locks & Keys is a Vintage Story fork of Arkzenir's Lock & Key mod. It replaces identity-bound locks with physical key profiles: anyone holding a finished key whose filed blade matches the cylinder can operate that lock. The fork removes player/group ownership and permission-based key matching.

This fork uses mod ID `vsmechanicallocks`, version `1.0.0`, and targets the Vintage Story `1.22.x` series. It is built and automated-tested against `1.22.5`. The new mod ID, asset domain, and storage namespace mean old `vslockandkey` worlds and item codes are not migrated; use a new world.

## Making and using keys

1. Cast or smith a solid key blank.
2. Hold the key in the main hand and a file in the offhand, then right-click air. Select one of eight blade sections and hold File. Each one-second stroke removes one height step and costs one file durability. Cuts cannot be undone.
3. Make at least one cut and choose Finish. Further filing makes the key unfinished again. Jeweling preserves its profile; material and gems do not affect matching.
4. Install a vanilla padlock on a lockable door or container with a right-click. A new lock is unconfigured and unlocked.
5. Sneak + right-click an unconfigured lock with a finished individual key to fit its cylinder. The lock remains unlocked.
6. Sneak + right-click again with a matching key to toggle the lock. A held keyring tries its stored keys; keys elsewhere in the inventory are not searched. Close doors and all container viewers before locking.
7. Remove an unlocked lock with a plumb and square while sneaking and right-clicking. The cylinder profile is preserved. Normal claim BuildOrBreak access is required to install or remove locks.

Lock profiles and state persist with their chunk. Destroying or replacing the supporting block removes the lock without dropping an intact padlock. Reinforcement remains governed by the game. Story locks and door bars are left intact. See [DESIGN.md](DESIGN.md) for mechanics and persistence details.

## Build and test

Copy `Properties/localSettings.props.template` to `Properties/localSettings.props` and set `GameDirectory_1_22_5` to a Vintage Story 1.22.5 installation. The project uses .NET 10.

```powershell
dotnet build VSMechanicalLocks.csproj
dotnet run --project tests/Mechanical.Tests.csproj -- "C:/Games/VintageStory"
dotnet build VSMechanicalLocks.csproj -c Release
```

The release build creates `Releases/vsmechanicallocks_1.0.0.zip`. Automated coverage and the in-game test checklist are in [TESTING.md](TESTING.md).

## Credits and license

This project is based on [Arkzenir's Lock & Key](https://github.com/Arkzenir/VSLockAndKey), listed on the [Vintage Story mods site](https://mods.vintagestory.at/locksandkeys). See [Arkzenir's Vintage Story profile](https://mods.vintagestory.at/show/user/6FFD71B4E8E7D178BE41). The upstream repository identifies Alper Sarı as the MIT copyright holder. This fork's mechanical-lock redesign, 1.22.x packaging, tests, and documentation are contributions by HansJack. See [NOTICE.md](NOTICE.md) and [LICENSE](LICENSE) for attribution and license terms.

# 中文

Mechanical Locks & Keys 是基于 Arkzenir 的 Lock & Key 制作的 Vintage Story Fork。它使用钥匙齿形匹配锁芯：任何人只要持有齿形吻合的完工钥匙，就能操作对应的锁。不再按玩家、群组身份或权限判断钥匙。

本 Fork 的模组 ID 为 `vsmechanicallocks`，模组版本为 `1.0.0`，面向 Vintage Story `1.22.x` 系列；当前使用 `1.22.5` 游戏程序集构建并通过自动测试。由于更换了模组 ID、资产域名和存档命名空间，旧 `vslockandkey` 世界及物品代码不会迁移，请使用新世界。

## 制作与使用

1. 浇筑或锻造实心钥匙坯。
2. 主手持钥匙、副手持锉刀，朝空中右键打开界面。选择八个齿位之一并按住锉削。每次一秒，移除一层金属并消耗一点锉刀耐久；锉掉的部分不能恢复。
3. 至少锉削一次后点击“完工”。继续锉削会使钥匙重新变为未完工。镶嵌会保留齿形；材质和宝石不参与开锁匹配。
4. 手持原版挂锁右键可锁定的门或容器安装。新锁未配置且未上锁。
5. 手持完工钥匙潜行并右键未配置的锁来配置锁芯；配置后仍未上锁。
6. 再次手持匹配钥匙潜行右键即可切换锁定状态。手持钥匙环时会检查环内钥匙；不会搜索背包。上锁前请关闭门和所有容器界面。
7. 手持铅垂规潜行右键可以拆下未上锁的锁，并保留锁芯形状。安装和拆卸需要原版领地的 BuildOrBreak 权限。

锁芯形状和状态随区块保存。破坏或替换承载方块会移除锁记录，不掉落完整挂锁；加固仍由游戏原有规则管理。剧情锁和门闩保持原样。机制与持久化细节见 [DESIGN.md](DESIGN.md)。

## 构建与测试

将 `Properties/localSettings.props.template` 复制为 `Properties/localSettings.props`，并设置 `GameDirectory_1_22_5` 为 Vintage Story 1.22.5 安装路径。项目使用 .NET 10。

```powershell
dotnet build VSMechanicalLocks.csproj
dotnet run --project tests/Mechanical.Tests.csproj -- "C:/Games/VintageStory"
dotnet build VSMechanicalLocks.csproj -c Release
```

发布包为 `Releases/vsmechanicallocks_1.0.0.zip`。自动测试结果和游戏内验收清单见 [TESTING.md](TESTING.md)。

## 致谢与许可

本项目基于 [Arkzenir 的 Lock & Key](https://github.com/Arkzenir/VSLockAndKey)，原模组也发布在 [Vintage Story 模组网站](https://mods.vintagestory.at/locksandkeys)。另见 [Arkzenir 的 Vintage Story 作者主页](https://mods.vintagestory.at/show/user/6FFD71B4E8E7D178BE41)。上游仓库的 MIT 版权声明归 Alper Sarı 所有。本 Fork 的机械锁改造、1.22.x 打包、测试和文档由 HansJack 贡献。署名和许可条款见 [NOTICE.md](NOTICE.md) 与 [LICENSE](LICENSE)。
