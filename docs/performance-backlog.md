# 性能优化待办

## 运行内存峰值（P1）

- 记录日期：2026-10-02
- 用户观察：游戏运行时系统监视器显示内存使用约 5 GB。
- 当前判断：需要区分游戏进程 RSS、Unity Editor/Hub、姿态与合照服务、macOS 压缩内存和离线审查缓存后再下结论。一次独立启动已构建游戏的本机采样中，游戏进程 RSS 约为 860 MB（启动加载期），15 秒约 602 MB，35 秒约 359 MB；目前没有证据表明单独游戏进程稳定占用 5 GB。
- 初步风险点：高分辨率角色/场景纹理、角色切换时的模型与材质生命周期、合照 RenderTexture/Texture2D/PNG 编码副本、战斗特效对象，以及开发审查导出的帧缓存可能造成峰值或总内存误判。

### 优化时机

在街机体验的主要功能稳定后，安排一次真实运行剖析；优先处理可复现的峰值，不为了单纯降低数字牺牲角色画质或合照清晰度。

### 计划与验收

1. 分别采样等待、角色切换、战斗、大招、合照倒数、合照预览和重开后的 RSS/Unity Native/Managed 内存。
2. 检查角色切换是否销毁旧模型、材质、动画和纹理；检查合照保存后是否释放 RenderTexture、Texture2D 和临时 PNG 缓冲。
3. 让特效和粒子使用复用或上限，确认暂停、重开、重拍不会线性增长。
4. 将高分辨率纹理按屏幕实际尺寸和平台质量档位加载，保留 1080P/2K 合照输出能力。
5. 验收目标：完整一局（含一次大招和一次合照）结束后 RSS 回落到启动峰值附近；连续三局不得持续增长；画面、动作识别、合照预览和 674 项回归检查保持通过。

### 2026-10-02 首轮完整流程证据

使用独立构建运行完整引导流程，覆盖选角、战斗、大招、合照、重拍和再开一局；采样的是 `迪迦体感训练场` 游戏进程的 RSS/VSZ，而不是系统总内存。结果如下：

| 阶段 | RSS 观测 |
| --- | ---: |
| 启动加载峰值 | 约 635 MB |
| 战斗 | 约 265–350 MB |
| 大招 | 约 299–366 MB |
| 合照与预览 | 约 193–364 MB |
| 第二局重新进入战斗 | 约 265 MB |

本次流程持续 162.9 秒，自动合照 2 次、重拍和再开局均通过，未出现 RSS 随流程线性增长；完整识别回归仍为通过。当前结论是：暂不为了一个未复现的 5GB 总内存读数盲目降低素材质量；下一次性能窗口优先补做三局连续循环和角色切换/合照释放检查。若用户看到的是系统总内存，则需要同时记录 Unity Editor/Hub、姿态服务、合照网关和开发审查缓存的进程占用。

本次原始采样：[memory-profile-20261002.tsv](../logs/memory-profile-20261002.tsv)；完整流程报告：[guided-validation.json](../artifacts/memory-profile-20261002/guided-validation.json)。

### 开发构建遥测与后续动作

已加入仅在 Development Build/Debug Build 输出的低频 `[RuntimeMemory]` 记录，区分 Unity allocated、reserved、Mono used 和 Mono heap，并标注战斗阶段与合照是否打开。重新构建后的完整体感流程（151.6 秒，键鼠事件 0，自动合照 2 次，重拍和再开局通过）记录到：

- Unity allocated：约 242–255 MB
- Unity reserved：约 597–605 MB
- 合照阶段没有持续单调增长，返回第二局时仍约 247 MB allocated / 605 MB reserved

这项遥测不进入发行版的交互逻辑，也不触发自动卸载。下一次优化只在连续三局或角色切换能够复现增长时处理资源生命周期；若 Activity Monitor 仍显示 5GB，则应同时抓取 Unity、Hub、姿态服务和网关进程的 RSS。

本次遥测日志：[guided-runtime-memory.log](../logs/guided-runtime-memory.log)；流程报告：[guided-validation.json](../artifacts/guided-runtime-memory/guided-validation.json)。

### 2026-10-02：记录后的第一项低风险修复

本轮没有因为系统监视器的 5 GB 总内存读数盲目卸载资源；先修复了一个会污染性能/画面判断的开发期取样问题。Unity 近景审查在同一状态、同一时间被重复调用时，哥尔赞的手腕和手指约束会二次叠加，曾出现 0.375765 个场景单位的零时间位移。运行时现在对相同战斗状态和时间的零增量取样直接复用已解算姿态；状态、时间或动作任一改变仍会正常重新求解。

修复后 `ClawPoseReview.Stability` 在 15/30/60 Hz 的交换、连续攻击、暂停恢复和新局复位共 12 组全部通过，`zeroTimeError=0.000000`；`scripts/check.sh` 通过 674 项，macOS 播放器已重新构建。它不会降低素材质量，也不会改变游戏战斗时序。内存专项仍按“连续三局 + 角色切换 + 合照释放”验收，等出现可复现的进程 RSS 增长后再处理资源生命周期。

证据：`logs/claw-current-stability.log`、`artifacts/claw-current/stability-only/stability.txt`、`logs/check-idempotent-claw.log`、`logs/build-idempotent-claw.log`。

修复后的 macOS 构建再次完成无人值守全流程，耗时 154.9 秒：键鼠事件 0、自动合照 2 次、重拍、照片预览、合照断流恢复和再开一局均通过。该次开发遥测仍为 allocated 约 241–255 MB、reserved 约 599–607 MB，第二局进入战斗约 247 MB allocated；原始日志：[idempotent-guided-2](../logs/idempotent-guided-2.log)，报告：[guided-validation.json](../artifacts/idempotent-guided-2/guided-validation.json)。
