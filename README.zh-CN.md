# QS3D AutoCAD

**语言：** [English](README.md) · [Tiếng Việt](README.vi.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [Русский](README.ru.md) · [Français](README.fr.md)

QS3D AutoCAD 是 QS3D 面向 Autodesk AutoCAD 的宿主插件，用于结构建模与工程量工作流。

## 支持的 AutoCAD 版本代际

- AutoCAD 2021–2024：使用一个旧版 .NET Framework 4.8 宿主载荷，基于 Autodesk 所有的 `AutoCAD.NET` 24.0.0 编译，并从 `R24.0-R24.3` bundle 系列加载
- AutoCAD 2025：.NET 8 宿主载荷，基于 Autodesk 所有的 `AutoCAD.NET` 25.0.1 编译
- AutoCAD 2026：继续使用同一 .NET 8 目标载荷家族；由于 AutoCAD 2026.1.2+ 将宿主切换到 .NET 10，native acceptance 会记录真实观察到的宿主 CLR
- AutoCAD 2027：.NET 10 宿主载荷，基于 Autodesk 所有的 `AutoCAD.NET` 26.0.0 编译
- Autodesk 专用代码与宿主无关的 QS3D Core 相隔离
- 部署使用 AutoCAD `.bundle`
- 发布流水线同时生成便携式 bundle zip 和自包含安装程序 `QS3D-AutoCAD-<version>-Setup.exe`

旧版载荷仅基于 AutoCAD 2021 managed SDK 构建一次，并复用于 AutoCAD 2021、2022、2023 和 2024。Autodesk 的 managed 兼容矩阵明确支持在较新的 R24.x 宿主上使用较旧的 R24.x managed SDK，因此 QS3D 不会重复构建四份等价的 net48 二进制文件。旧版载荷与现代 .NET 8/.NET 10 载荷独立构建和打包；增加 2022–2024 的发现支持不会降低 2025–2027 二进制文件的目标版本。

AutoCAD 2026 具有额外的原生运行时边界：发布的 QS3D 2026 载荷仍以 .NET 8 为目标，而真实 AutoCAD 2026 宿主根据更新级别可能报告 CLR major 8 或 10。托管 CI 只验证源码/打包兼容性，不能替代在真实 AutoCAD 2026 安装中加载完全相同的候选包并记录实际观察到的 CLR/native 检查结果。

## 已实现的建模工作流

运行 `QS3D` 可延迟加载插件并打开可停靠的 QS3D 工作区。当前宿主实现了：

- `QS3DINIT` — 初始化/重命名存储在 DWG 中的 QS3D 项目
- `QS3DLEVEL` — 标高标记
- `QS3DGRID` — 轴网轴线
- `QS3DCOLUMN` — 3D 结构柱
- `QS3DBEAM` — 按平面方向创建的 3D 梁
- `QS3DSLAB` — 矩形 3D 板
- `QS3DWALL` — 按平面方向创建的 3D 墙
- `QS3DCURTAIN` — 模块化幕墙面板
- `QS3DSECTION` — 剖面标记
- `QS3DBOQ` — 从带 QS3D 标记的实体生成工程量汇总
- `QS3DEDIT` — 编辑 QS3D 属性，并在尺寸变化时重建物理实体
- `QS3DASSIGNLEVEL` — 将结构构件绑定到 QS3D Level，并移动/重建到该标高
- `QS3DLEVELMOVE` — 修改 Level 标高，并把 Z 向位移传播到所有绑定该 Level 的结构构件
- `QS3DBINDGRID` — 为结构构件附加一个或两个语义 Grid 引用
- `QS3DGRIDSNAP` — 同步重建与 Grid 绑定的结构几何和元数据
- `QS3DREFERENCERENAME` — 重命名 Level/Grid 引用，同时保留语义 ID 和绑定关系
- `QS3DLEVELSEQUENCE` — 按标高对 Level 名称排序
- `QS3DGRIDSEQUENCE` — 按空间顺序重新排列一组平行 Grid
- `QS3DCLEARREFS` — 删除 Level/Grid 布置引用而不移动几何
- `QS3DGRIDARRAY` — 以固定间距创建一系列命名的平行 Grid
- `QS3DREFERENCEDELETE` — 删除未使用的 Level/Grid；若仍存在依赖项则拒绝删除
- `QS3DREFERENCES` — 列出 Level/Grid 引用及其依赖数量
- `QS3DCOLUMNJIG`、`QS3DBEAMJIG`、`QS3DSLABJIG`、`QS3DWALLJIG`、`QS3DCURTAINJIG` — 临时 live-solid 创建预览，提供尺寸/方向反馈，并仅在提交时持久化
- `QS3DRIBBON` — 通过 AutoCAD 已加载的 `Autodesk.Windows` 运行时 UI 类型协调/创建 QS3D Ribbon
- `QS3DREFRESH` — 刷新模型浏览器
- `QS3DABOUT` — 显示宿主/运行时信息

可停靠工作区包含 Tools、Project 和 Levels & Grids 选项卡。项目浏览器列出 QS3D 所有的实体，与 AutoCAD pickfirst 选择同步，展示几何、工程量和布置引用属性，并可启动安全编辑。Palette 控件可在越南语和英语之间切换。

生成的几何包含类型化的 QS3D XData。项目身份/名称存储在 DWG Named Objects Dictionary 中，因此 QS3D 状态会随图纸一起保存。当前元数据使用向后兼容的 `QS3D2` schema 表示 Level/Grid 引用，同时继续读取旧版 `QS3D1` 实体。修改实体属性或 Level 布置时，QS3D 会保留语义 ID，同时替换或移动物理几何，从而避免 BOQ 元数据与可见模型发生偏离。

JIG/Grid-manager 在源码层面已完成，但仍需要真实宿主的 native acceptance。托管构建不能证明光标预览、Ribbon 视觉、undo/redo 或持久化在每个受支持的 AutoCAD 代际中都正确运行。

### Ribbon 边界

Ribbon bridge 明确**不**直接编译依赖 `AdWindows.dll` 或 `Autodesk.Windows`。托管 CI 无法替代或模拟这一 AutoCAD 原生 UI 依赖。`QS3DRIBBON` 在运行时解析已加载的 AutoCAD UI assembly/type，创建幂等的 QS3D tab（Model/References/Review panels），并在 Ribbon API 不可用时软失败，使 palette/model 命令仍可使用。

托管编译成功只能证明 bridge 源码保持宿主安全。`ribbon_surface` 与 `ribbon_visual_qa` 仍是 native acceptance gate；AutoCAD 2021–2024 旧版矩阵可以单独 qualification，而默认生产 qualification 矩阵仍为 AutoCAD 2025、2026 和 2027，直到 release policy 被明确修改。

## 构建与交付

GitHub `CI` 会构建并 smoke-test 与宿主无关的 Core，使用 Autodesk 所有的软件包编译一个 AutoCAD 2021–2024 legacy net48 载荷、AutoCAD 2025–2026 net8 载荷以及 AutoCAD 2027 net10 载荷；同时验证 command/bundle 架构、打包 engineering release candidate，并端到端校验 release provenance/checksum。Autodesk assembly 仅作为编译期依赖，不包含在 QS3D 发布载荷中。

CI 也会验证 native-acceptance 工具自身，并证明包含 `pending` 检查项的合成 evidence 会被默认现代矩阵和独立的 AutoCAD 2021–2024 legacy 矩阵共同拒绝。托管 CI 永远不会产生 native PASS。

`./scripts/package.ps1 -Version <version>` 会生成：

- `artifacts/QS3D-AutoCAD-<version>.zip`
- `artifacts/QS3D-AutoCAD-<version>-Setup.exe`
- `artifacts/RELEASE-PROVENANCE.json`
- `artifacts/SHA256SUMS.txt`

`RELEASE-PROVENANCE.json` 记录精确的 source commit、version、三个 runtime payload 家族、签名状态、artifact 大小和 SHA-256 hash。`./scripts/verify-artifacts.ps1 -Version <version>` 会独立验证该契约。

Setup executable 内嵌 bundle，并安装到所有用户共用的 Autodesk `ApplicationPlugins` 目录。安装/升级采用 staging 并支持安全 rollback；当 AutoCAD 正在运行时，Setup 会拒绝 install、upgrade 或 `--uninstall`。

Tag 发布采用 fail-closed：tagged SHA 必须位于 `main`，必须与 repository variable `QS3D_NATIVE_ACCEPTED_SHA` 完全一致，并且必须配置真实的 Authenticode PFX/password secrets。Workflow 会对插件 assembly 和 Setup.exe 签名，验证签名/provenance 后才创建 GitHub prerelease。手动 packaging 仍可用于 engineering validation，但当 provenance 报告 `signed=false` 时，不得将其描述为已签名的生产发布。

当前插件不发送 telemetry，也不执行 production licensing call。当前隐私状态见 `docs/PRIVACY.md`，release/signing gates 见 `docs/RELEASE-SECURITY.md`。

源码构建为绿色不等同于 native runtime qualification。生成的精确 bundle 仍必须在真实 AutoCAD 中进行 acceptance testing。默认正式 release 矩阵仍为 AutoCAD 2025/2026/2027。AutoCAD 2021/2022/2023/2024 使用单独的 legacy evidence 矩阵，每个被测试宿主都必须拥有真实宿主 evidence，才能称为 native-qualified。AutoCAD 2026 evidence 还必须记录 QS3D 加载后实际观察到的 CLR，以避免掩盖 AutoCAD 2026.1.2+ 向 .NET 10 宿主的过渡。完整 evidence workflow 见 `docs/NATIVE-ACCEPTANCE.md`。

架构、构建和 native acceptance gates 详见 `docs/IMPLEMENTATION-PLAN.md` 与 `docs/BUILD.md`。
