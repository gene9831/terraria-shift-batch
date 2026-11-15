# Shift Batch

[English](README.md) | **简体中文**

为泰拉瑞亚原版的 **左 Shift 批量购买和制作**添加数量设置。Shift Batch 是基于 [TerrariaModder](https://github.com/Inidar1/terraria-modder) 的轻量客户端模组。

购买和制作分别配置，默认值均为 **10**，保持原版批量大小；可设置范围为 **10～9999**。实际执行数量还会受到物品剩余堆叠容量限制。

## 目录

- [功能](#功能)
- [运行要求](#运行要求)
- [安装](#安装)
- [用法](#用法)
- [配置](#配置)
- [从源码构建](#从源码构建)
- [测试](#测试)
- [兼容性与限制](#兼容性与限制)
- [故障排查](#故障排查)
- [参与贡献](#参与贡献)
- [许可证与致谢](#许可证与致谢)

## 功能

- 在 **F6** 模组面板分别设置 Shift 购买数量和制作次数。
- 默认每批购买 **10 个物品**、制作 **10 次配方**。
- 修改后立即生效，配置自动保存。
- 校验整数输入、限制数值范围，输入无效时保留原值。
- 考虑鼠标空置、已有同类堆叠及单次产出多个物品等情况。
- 沿用原版价格、扣款、库存、材料检查和长按连续操作。
- 可在设置中关闭功能，恢复原版行为。
- 可选延迟收包，满堆叠后持续长按即可继续购买／制作，**默认关闭**。
- 可选原版提示音，在达到容量上限时立即提示，给玩家留出松键取消的时间。

## 运行要求

| 组件 | 已测试版本／要求 |
| --- | --- |
| 操作系统 | Windows；上游框架面向 Windows 10、11 |
| Terraria | **1.4.5.8**；本版本会核对游戏程序版本 |
| TerrariaModder Core | 基于程序集 **0.4.2.0** 构建和测试 |
| 启动方式 | TerrariaModder Vault 或 `TerrariaInjector.exe` |

框架安装方式见其[安装指南](https://inidar1.github.io/terraria-modder/installation/)。开发依赖见[从源码构建](#从源码构建)。

## 安装

1. 获取 `ShiftBatch-1.1.0.zip`，或使用源码中的 `package.ps1` 自行构建。
2. 在 Vault 中安装本地 ZIP；也可将 ZIP 中的 `TerrariaModder` 文件夹合并到包含 `Terraria.exe` 的游戏目录。
3. 通过 Vault 或 `TerrariaInjector.exe` 重新启动模组游戏。
4. 打开 **F6 → Shift Batch**。

安装后的目录结构：

```text
<Terraria 游戏目录>/
└── TerrariaModder/mods/shift-batch/
    ├── ShiftBatch.dll
    ├── manifest.json
    ├── README.md
    ├── README.zh-CN.md
    └── LICENSE.txt
```

卸载时，关闭游戏并移走本模组的 `shift-batch` 文件夹。临时恢复原版行为可关闭模组设置中的 **Enabled / 启用**。

## 用法

1. 按 **F6**，选择 **Shift Batch**，修改所需的批量设置。
2. 点击数值，输入整数，按回车或点击输入框外确认。配置会自动保存并立即生效。
3. 在原版 NPC 商店中，按住 **左 Shift**，照常点击或长按购买。
4. 在原版制作栏中，按住 **左 Shift**，照常点击或长按制作。

**制作数量表示配方执行次数，而不是最终产物个数。** 例如某配方一次产出 25 个物品，填写 100 表示请求制作最多 2500 个，再根据容量和材料限制实际执行。

长按仍会连续操作并使用原版加速。配置值是每批数量，不是整个长按过程的最终目标总量。

### 可选自动收包

在 F6 中开启 **Auto-stow / 满堆叠收包**。手持物品达到有效容量上限时，可选的 **Limit sound / 满堆叠提示音** 立即播放；继续按住 Shift 和同一有效鼠标按键，并停留在同一商品或选中的配方上约 **0.3 秒**，物品即尝试进入个人背包，原版购买／制作可继续进行。

提示后及时松开 Shift 或鼠标即可取消。移开目标、切换手持物品、暂停、窗口失焦或打开阻挡操作的模组面板也会取消计时。原版因物品重用、背包整理、切换配方或尚有服务器制作请求而暂时禁止操作时，也不会收包。声音在**达到上限时**播放，每轮满堆叠仅一次，收包本身不会再播放一次。

入包沿用原版规则，可合并已有堆叠并使用空位及普通钱币／弹药栏；只能放下一部分时，余量保留在鼠标上。完全放不下时保留手持物品，等待松键后重新操作，不反复尝试。不会丢到地上，也不会放入虚空袋或其他储物模组。

## 配置

| F6 设置 | JSON 键 | 默认值 | 有效值 |
| --- | --- | --- | --- |
| Enabled / 启用 | `Enabled` | `true` | `true`、`false` |
| Shift buy / 购买数量 | `PurchaseBatch` | `"10"` | 整数文本，10～9999 |
| Shift craft / 制作次数 | `CraftBatch` | `"10"` | 整数文本，10～9999 |
| Auto-stow / 满堆叠收包 | `AutoStow` | `false` | `true`、`false` |
| Limit sound / 满堆叠提示音 | `LimitSound` | `true` | 仅在自动收包开启时生效 |

配置保存位置：

```text
<Terraria 游戏目录>/TerrariaModder/core/configs/shift-batch.client.json
```

```json
{
  "_version": 2,
  "Enabled": true,
  "PurchaseBatch": "10",
  "CraftBatch": "10",
  "AutoStow": false,
  "LimitSound": true
}
```

- 大于 9999 时限制为 9999，小于 10 时限制为 10。旧配置中的较小值会迁移到 10；实际操作仍可能因容量、库存、货币或材料限制而少于 10。
- 字母、小数、空白、整数溢出等输入会保留上次有效值，初始值为 10；同时记录日志，进入世界时还会显示游戏消息。
- 手动编辑 JSON 时请使用普通整数文本。原始浮点数／科学记数法会在 Core 转换前被拒绝，避免精度丢失后误变为整数。

### 堆叠容量示例

有效堆叠上限取物品自身的 `maxStack`，并额外限制不超过 9999；鼠标上已有兼容物品时扣除已有数量，物品不兼容时停止。购买还受商品库存和货币限制，制作保留原版材料及制作条件检查。

| 场景 | 最多新增操作 |
| --- | --- |
| 购买，上限 9999，鼠标已有 9900 个 | 再购买 99 个 |
| 每次配方产出 25 个，鼠标为空，上限 9999 | 执行 399 次，产出 9975 个 |
| 每次配方产出 25 个，鼠标已有 9900 个 | 执行 3 次，再增加 75 个 |

空间放不下一次完整配方产物时停止。开启自动收包后，这也被视为有效上限：每次产出 25 个时，9975 个即可触发收包，而无需等待无法凑到的 9999。不会截掉部分产物，长按加速后的请求同样受容量限制。

## 从源码构建

### 开发前提

- 本地已安装 Terraria 1.4.5.8、TerrariaModder Core 及其 Harmony 依赖。
- 已安装游戏运行前提中的 XNA 程序集。
- Windows PowerShell 或 PowerShell，以及位于 `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe` 的 .NET Framework C# 编译器。

主要脚本使用 C# 5 编译并引用本地依赖。在已验证的开发机器上，无需额外下载 SDK、Developer Pack 或 Visual Studio。

### 构建 DLL

克隆或下载本仓库，在仓库根目录打开 PowerShell，设置自己的游戏路径：

```powershell
$terraria = 'C:\Program Files (x86)\Steam\steamapps\common\Terraria'
.\build.ps1 -TerrariaPath $terraria
```

输出为 `build/ShiftBatch.dll`。省略 `-TerrariaPath` 时，脚本默认使用 `D:\SteamLibrary\steamapps\common\Terraria`；安装位置不同时请传入参数。

### 构建安装包与源码包

```powershell
.\package.ps1 -TerrariaPath $terraria
```

脚本先运行测试、构建 DLL，然后将安装包和源码包输出到 `dist/`。可用 `-OutputDirectory` 指定其他输出目录。包内仅包含模组自己的文件，Terraria、Core、Harmony、XNA 均从本地引用。

### 可选 SDK 工程

`ShiftBatch.csproj` 的目标为 **.NET Framework 4.8（`net48`）**。如果使用已安装的 .NET SDK，还需要通过 Developer Pack 或支持的包恢复提供 .NET Framework 4.8 引用程序集：

```powershell
dotnet build .\ShiftBatch.csproj -c Release "-p:TerrariaPath=$terraria"
```

SDK 工程作为备用方式提供，已验证的构建路径为 `build.ps1`。

## 测试

```powershell
.\test.ps1 -TerrariaPath $terraria
```

当前自动测试通过 **239 项断言**，覆盖输入校验、Core 配置读写、原始数值精度边界、堆叠计算、真实游戏 IL 变换、原版 Harmony 补丁安装／卸载、生命周期、延迟取消、提示音时机，以及隔离 x86 进程中的原版入包与数量守恒检查。

测试不启动游戏入口，不打开用户角色或世界；测试存档路径重定向到 `build/game-sandbox`。

**手动验证：**维护者于 **2026-11-15** 反馈 **1.0.0** 游戏内验证通过。新增的 **1.1.0** 自动收包已通过自动测试，仍需游戏内验证。联机行为尚未验证。

## 兼容性与限制

- 支持原版 NPC 商店和原版鼠标制作栏。Storage Hub 等模组自己的制作界面继续使用自身规则。
- 这是 TerrariaModder 客户端模组。联机交易和任意第三方补丁组合尚未验证。
- 已测试的 Core 程序集为 0.4.2.0，但其内部框架标识仍为 0.4.1，因此清单最低标识使用 0.4.1 避免误报；这不代表已支持所有 Core 构建。
- 安装补丁前检查原版方法；游戏版本或指令结构不匹配时，模组撤销自身补丁并记录错误。

实现细节见[开发说明](https://github.com/gene9831/terraria-shift-batch/blob/main/docs/DEVELOPMENT.md)。

## 故障排查

| 症状 | 检查方法 |
| --- | --- |
| F6 中找不到 Shift Batch | 检查安装目录和 `manifest.json`，通过模组启动器重新启动。 |
| Shift 仍使用默认批量 | 确认已启用并确认输入；在最新 Core 日志中检查补丁错误。 |
| 实际数量少于请求 | 检查鼠标容量、物品兼容性、货币、库存、材料及制作条件。 |
| 构建提示缺少引用 | 检查 `-TerrariaPath`、Core/Harmony 安装及 XNA 运行程序集。 |
| 找不到编译器或引用程序集 | 安装所需的 .NET Framework 开发工具，或使用提供 net48 引用程序集的 SDK 工程。 |

日志位于 `<Terraria 游戏目录>/TerrariaModder/core/logs/terrariamodder.client.session-*.log`。选择最新会话，查找 `[shift-batch]` 项。

## 参与贡献

构建、测试和问题反馈方式见 [CONTRIBUTING.md](https://github.com/gene9831/terraria-shift-batch/blob/main/CONTRIBUTING.md)。版本记录见 [CHANGELOG.md](https://github.com/gene9831/terraria-shift-batch/blob/main/CHANGELOG.md)。

## 许可证与致谢

本项目使用 [MIT 许可证](LICENSE.txt)。Terraria 及第三方依赖遵循各自的许可证，不包含在本仓库中。

感谢 [TerrariaModder](https://github.com/Inidar1/terraria-modder)、[Harmony](https://github.com/pardeike/Harmony) 和 Terraria 开发者。
