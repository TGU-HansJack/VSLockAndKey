# Verification / 验证

## Automated checks

The console harness uses the actual game DLLs and the built mod, without downloading test packages. Build Debug first; pass the same game directory to the runner. The paths below are examples.

```powershell
dotnet build VSLockAndKey_1.21.5.csproj
dotnet run --project tests/Mechanical.Tests.csproj -p:VSVersion=1.21.5 -- "C:/Games/VS1.21.5"
dotnet build VSLockAndKey_1.22.5.csproj
dotnet run --project tests/Mechanical.Tests.csproj -p:VSVersion=1.22.5 -- "C:/Games/VS1.22.5"
```

Both exact versions passed 61 assertions during this change. Coverage includes blank/cut/finished states; cut limits; shape matching across materials and jeweling; geometry in both key models; stack and cylinder serialization; chunk persistence, dimensions and multiblock resolution; destruction/replacement; server filing timing, durability, stale revisions and stack swapping; install/configure/toggle/detach; viewers; held keyrings; claim scope; distance/spectator restrictions; inventory/block-container guards; and applying every Harmony patch to the actual game methods.

World, player and network interfaces are substituted in these tests. Assertions exercise production handlers and real serialization/patch installation, but do not start a game, draw the GUI, or emulate the full engine network/event pipeline.

## 游戏内验收（尚待实机执行）

请在 1.21.5 和 1.22.5 的新世界分别执行；多人项目需要两名玩家及独立客户端/服务器。以下是待验收项目，不表示已经通过。

- 从单件/四联模具浇筑及铁砧配方取得实心钥匙坯；验证烧制、金属液面、取出数量和复用模具。
- 主手钥匙、副手锉刀，空中右键打开界面。逐齿锉削、松开取消、切换齿位、最低高度、锉刀最后一点耐久后完工。检查中英文和不同 GUI 缩放比例。
- 检查普通/镶宝石钥匙在物品栏、第一/第三人称、掉落实体、地面存储及界面预览中具有一致轮廓。更改齿形后不得继续显示旧模型。
- 镶嵌完成钥匙应保留齿形；同形备用钥匙及不同材质钥匙都可开锁，少锉或多锉任一齿均失败。重新锉削后必须再次完工。
- 未加固门/箱安装原版挂锁；首次配置不自动锁；重复转动切换状态；普通使用不自动开锁；钥匙放背包无效，手持钥匙环有效，环不能初始化锁芯。
- 打开门、开启任一联动门扇、任一玩家查看箱子时拒绝上锁。上锁后点击另一扇、箱子自动关盖等不得绕开。剧情锁和门闩仍应生效。
- 一名玩家配置并上锁，另一名玩家持同形钥匙应能解锁。换人、离线和重进不改变匹配；服务器重启、离开/重新加载区块后状态一致。
- 在其他人的领地里测试：未锁目标可用，持匹配钥匙潜行可解锁；相邻无机械锁方块仍受领地限制；不能安装/拆卸/破坏；其他模组拒绝使用的回调仍有效。
- 未锁时拆卸保留形状并可重新安装；已锁时铅垂规右键/左键不应直接移除锁或加固。普通拆锁后加固保持，破坏/替换方块不掉完整锁。
- 钥匙环放入/取出后立即切换物品、丢弃、死亡、断线、关闭后重开；检查没有复制、丢失、旧会话继续写入或跨玩家访问。
- 使用测试客户端重放过时修订号、过早/重复锉削、错误目标、跨维度/超距报文、已锁容器转移报文及未开界面的转移报文，应拒绝。两人同时转同一锁仅接受有效修订号。

No claim is made here that native GUI rendering or multiplayer gameplay has been tested in a running game. Those remain the release acceptance boundary.
