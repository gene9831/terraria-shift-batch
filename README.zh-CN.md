# Shift Batch

[English](README.md) | **简体中文**

基于 [TerrariaModder](https://github.com/Inidar1/terraria-modder) 的客户端模组，用于设置按住左 Shift 时购买和制作物品的批量。

## 功能

- 分别设置 Shift 购买数量和制作次数。
- 可选满堆叠自动收包。

## 运行要求

- Windows 10 或 11。
- Terraria 1.4.5.8。
- TerrariaModder Core 0.4.2。

支持原版 NPC 商店和原版制作界面。其他模组的商店、制作界面可能使用自身规则。

## 安装

1. 从[最新发布页](https://github.com/gene9831/terraria-shift-batch/releases/latest)下载安装 ZIP。
2. 在 Vault 中作为本地压缩包安装，或将其中的 `TerrariaModder` 文件夹解压到 Terraria 安装目录。
3. 启动模组游戏，在模组管理器中打开 **Shift Batch 的配置页**。

卸载时，关闭游戏并删除 `TerrariaModder/mods/shift-batch`。临时停用可关闭配置页中的 **Enabled / 启用**。

## 用法

在配置页设置购买数量和制作次数，购买或制作时照常按住 **左 Shift**。持续按住鼠标会重复操作。

制作数量按**配方执行次数**计算。例如配方一次产出 25 个物品，设置 100 表示请求最多产出 2500 个。实际数量受到鼠标堆叠容量、库存、货币和材料限制；堆叠上限为 9999 的物品，每次产出 25 个时，会在 9975 个达到有效容量。

### 自动收包

开启 **Auto-stow / 满堆叠收包** 后，达到容量上限时，在同一商品或配方上继续按住 Shift 和同一鼠标按键约 **0.3 秒**，手持物品会尝试进入个人背包。松键或移开目标会取消计时。

背包只能放下一部分时，余量保留在鼠标上；完全放不下时继续手持，等待重新操作。不会为了腾出空间而丢弃物品。

## 配置

修改后立即生效，配置自动保存。

| 设置 | 默认值 | 说明 |
| --- | --- | --- |
| Enabled / 启用 | 开 | 启用自定义 Shift 批量。 |
| Shift buy / 购买数量 | 10 | 每批购买个数，整数范围 10～9999。 |
| Shift craft / 制作次数 | 10 | 每批执行配方次数，整数范围 10～9999。 |
| Auto-stow / 满堆叠收包 | 关 | 达到容量上限后，延迟将物品放入个人背包。 |
| Debug logging / 调试日志 | 关 | 在框架日志中输出诊断信息。 |

超出范围的数值限制到 10～9999，无效输入保留原值。实际数量还受物品自身堆叠上限限制，单次产出多个物品的配方也会计算完整产物所需的空间。

## 从源码构建

准备 Terraria、TerrariaModder Core、游戏的 XNA 运行时和 Windows .NET Framework C# 编译器。在仓库根目录打开 PowerShell，指定游戏安装路径：

```powershell
$terraria = 'C:/Games/Terraria'
./build.ps1 -TerrariaPath $terraria
./package.ps1 -TerrariaPath $terraria
```

DLL 输出到 `build/ShiftBatch.dll`，安装包和源码包输出到 `dist/`。

使用同一安装路径运行测试：

```powershell
./test.ps1 -TerrariaPath $terraria
```

## 故障排查

实际数量少于请求时，检查鼠标容量、货币、库存、材料和制作条件。加载失败时，确认游戏和 Core 符合上述运行要求。

需要诊断信息时，在配置页开启 **Debug logging / 调试日志** 并复现问题，然后在最新的 `TerrariaModder/core/logs/terrariamodder.client.session-*.log` 中查找 `[shift-batch]`。排查后关闭调试日志即可。

## 参与贡献与许可证

开发和问题反馈方式见 [CONTRIBUTING.md](CONTRIBUTING.md)。本项目使用 [MIT 许可证](LICENSE.txt)。

基于 [TerrariaModder](https://github.com/Inidar1/terraria-modder) 和 [Harmony](https://github.com/pardeike/Harmony) 开发。
