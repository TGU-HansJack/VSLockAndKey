# Lock & Key 2.0 — 机械锁

本版本把原版挂锁改为按实体钥匙形状工作的机械锁。任何人持有形状匹配的完工钥匙都能解锁；没有玩家或群组绑定、锁主身份、管理员开锁例外、背包自动找钥匙或钥匙使用损耗。

**仅用于新世界。** 不迁移 1.x 的绑定钥匙、原版权限锁或旧锁数据。客户端与服务端必须安装对应游戏版本的同一模组版本。

## 制作与操作

1. 浇筑或锻造实心钥匙坯。普通和批量模具仍可复用。
2. 主手持钥匙、副手持锉刀，朝空中右键打开锉削界面。选择八个齿位之一并按住“锉削”：每次一秒，移除一层金属并消耗一点锉刀耐久。每齿有六档高度，只能减料；不能撤销、补料或一键复制。
3. 至少锉削一次后点击“完工”。再锉削会使钥匙重新进入未完工状态。镶嵌宝石会保留齿形及完工状态，材质和宝石不参与开锁匹配。
4. 手持原版挂锁，右键可上锁的门或容器安装，无需先加固。新锁未配置且未锁定。
5. 手持一把完工钥匙，潜行并右键新锁，首次配置锁芯；此时仍未上锁。锁芯配置后不能重新配形。
6. 手持匹配钥匙，潜行并右键切换锁定状态。门及相连门扇必须关闭，容器必须无人查看才能上锁。解锁状态会持续保存；普通右键使用不会自动转动锁，也不会扫描背包。
7. 主手持钥匙环时，潜行并右键锁具会尝试环内的完工钥匙。配置新锁需要单独手持钥匙。钥匙环朝空中右键管理内容；切换手持物会使存储会话失效。
8. 锁解开后，主手持铅垂规（plumb and square），潜行并右键拆下锁。锁芯随挂锁保留，重新安装后仍匹配原齿形。已锁定的挂锁不能直接拆卸。

锁的齿形、锁定状态和位置按区块保存。破坏或替换承载方块会销毁锁，不掉落完整挂锁。方块加固仍按原版规则承受破坏；拆锁不会移除加固。

原版领地仍管理安装、拆卸及破坏。已安装机械锁的准确目标有局部使用例外：未锁定时任何人可使用；手持匹配钥匙并潜行时可解锁。例外不会授予建造、拆卸或破坏能力，也不覆盖死亡、旁观及其他模组的拒绝。剧情锁、门闩继续存在。

## 配置

`ModConfig/vslockandkey.json` 只保留钥匙环容量。旧权限配置字段会在保存时移除。

```json
{
  "KeyringSlotsByMaterial": {
    "rope": 2,
    "tinbronze": 3,
    "bismuthbronze": 3,
    "blackbronze": 3,
    "iron": 4,
    "meteoriciron": 5,
    "steel": 6
  },
  "DefaultKeyringSlots": 4
}
```

容量限制为 0–64；0 禁用该钥匙环存储。降低容量会隐藏超额格子，其数据仍保留，恢复容量后可取出。

## 构建与验证

复制 `Properties/localSettings.props.template` 为 `Properties/localSettings.props`，填写相应游戏路径。1.21.5 使用 .NET 8，1.22.5 使用 .NET 10。

```powershell
dotnet build VSLockAndKey_1.21.5.csproj -c Release
dotnet build VSLockAndKey_1.22.5.csproj -c Release
```

发布包输出到 `Releases/vslockandkey_2.0.0_vs{游戏版本}.zip`。两版本共用源码，仅对锻造回调和地面模型接口进行条件编译。

自动验证、实际验证边界及游戏内验收步骤见 [TESTING.md](TESTING.md)。设计及数据格式见 [DESIGN.md](DESIGN.md)。

# English

Version 2 replaces vanilla padlock identity checks with physical blade matching. Anyone holding a matching finished key can unlock it. There are no player/group bindings, owner/admin exceptions, inventory searches, or key use wear. **New worlds only; no migration from 1.x or existing vanilla identity locks.** Install matching builds on both client and server.

Cast or smith a solid blank. Hold the key in your main hand and a file in your offhand, then right-click air. Select one of eight blade sections and hold File: each one-second stroke removes one height step and costs one file durability. Each section has six heights; removed metal cannot be restored. Make at least one cut and choose Finish. Further filing makes the key unfinished again. Jeweling preserves the blade and finish; materials and gems do not affect matching.

Right-click a lockable door/container with a vanilla padlock to install it without reinforcement. Sneak + right-click with an individual finished key fits the cylinder once, leaving the lock unlocked. Repeat with a matching key to toggle its persistent state. A held keyring tries its contents; keys elsewhere in your inventory do nothing. Ordinary interaction never unlocks automatically. Close doors and linked wings, and close all container viewers before locking.

Sneak + right-click an unlocked padlock with a plumb and square to remove it, preserving the cylinder. Removal needs normal claim BuildOrBreak access. Destruction/replacement destroys the lock without an intact padlock drop; reinforcement remains vanilla. Local claim Use exceptions allow anyone to use an unlocked mechanical target, or unlock that exact target with a matching held key. Other mod vetoes, death/spectator restrictions, story locks and door bars remain applicable.

Only keyring capacities are configurable (see JSON above). Build the appropriate project with the matching game assemblies; release ZIPs are generated automatically. See [TESTING.md](TESTING.md) for automated checks and the in-game acceptance checklist.
