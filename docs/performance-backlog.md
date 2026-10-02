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
5. 验收目标：完整一局（含一次大招和一次合照）结束后 RSS 回落到启动峰值附近；连续三局不得持续增长；画面、动作识别、合照预览和当前 675 项回归检查保持通过。

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

### 2026-10-02：合照缓存回收与遗留构建进程记录

合照流程现在会在 PNG 成功写入 Downloads 后立即清空编码字节缓存，保存的网络帧也会在定格后释放；AI 返回的 PNG 由 `Texture2D.LoadImage` 消费一次后立刻移交并清空，避免原图、AI 结果和 Unity 纹理同时长期驻留。保存失败仍保留一次重试所需的缓冲。`scripts/build_macos.sh` 也增加了退出清理：批处理 Unity 被中断或卡在 Licensing 时，脚本退出会结束自己的子进程，避免下次运行叠加多个遗留 Unity 实例。

本轮排查发现系统里曾有多个运行 7–9 小时的遗留 Unity 批处理/审查进程，以及旧 Licensing client；它们会被 macOS 的系统总内存统计计入，并阻塞后续构建。已结束这些本项目遗留进程，Unity Hub 图形界面未关闭。它们不是游戏本身的稳定 RSS，因此“系统总内存约 5 GB”不能直接等同于游戏内存。发行包构建需在 Unity Licensing 通道恢复后再做一次最终启动验证；当前代码回归检查已通过 674 项。

### 2026-10-02：用户再次观察到约 5 GB（待复现）

用户再次报告游戏运行时系统监视器约 5 GB，并要求记录为后续优化任务。当前不能把这个数字直接判定为游戏进程占用：已有独立构建采样显示游戏进程 RSS 启动峰值约 635 MB，Unity 遥测的 allocated 约 242–255 MB、reserved 约 597–607 MB；`ps` 在当前受限会话中也无法读取系统进程列表，因此本次不虚构新的系统级采样。

任务状态保持 P1：下一次可运行 Unity 构建后，分别记录游戏进程、Unity Editor/Hub、姿态服务、合照服务和审查脚本的 RSS，并覆盖连续三局、角色切换、大招、合照倒数、预览、重拍和再开局。若单独游戏进程仍达到 GB 级，再按纹理、特效对象、角色生命周期和合照缓冲逐项优化；若只有系统总内存达到 5 GB，则先清理遗留进程和审查缓存，避免牺牲画质。

### 2026-10-02：授权恢复后的新一轮体感采样

Unity Licensing 通道已恢复。最新开发构建的完整无键鼠流程持续 165.8 秒，覆盖两局战斗、大招、防御噪声、自动合照、重拍、预览断流恢复和再开局；运行时遥测仍为 allocated 约 241–255 MB、reserved 约 599–607 MB，第二局战斗约 247 MB allocated。当前样本没有显示合照或重开导致单调增长；后续仍需在同一次会话内补做三局连续循环和多角色切换，才能关闭 P1。

证据：[guided-current-development.log](../logs/guided-current-development.log)、[guided-validation.json](../artifacts/guided-current-development/guided-validation.json)。

### 2026-10-02：独立发行进程的系统级 footprint 复核

为复核用户再次看到的约 5 GB，本次直接启动发行包 `TigaTraining.app`，并用 macOS `ps`、`vmmap -summary` 和 `footprint` 采样；没有把 Unity Editor 的内存混入游戏进程。键盘练习实例的 RSS 从启动加载期约 516 MiB，在约 50 秒后回落到 361 MiB；物理 footprint 当前约 1.72 GiB，峰值约 1.80 GiB。`vmmap` 中图形相关 footprint 约 680 MiB（IOAccelerator graphics）+ 391 MiB（owned graphics），是当前主要组成。独立 MediaPipe 相机服务约 210 MiB RSS。当前没有证据表明游戏进程稳定占用 5 GB；VSZ 约 431 GB 是 macOS/Unity 的保留虚拟地址空间，不能当作实际内存。

这次结果不触发降低角色纹理或关闭特效的仓促修改：当前街机画面仍需要高质量模型、灯光和合照输出。P1 任务继续保留，下一次性能窗口在同一进程内覆盖三局、角色切换、大招和合照预览，并同时记录游戏、相机服务、Unity Hub/Editor 的 RSS；只有在游戏自身 footprint 随流程持续增长，或单独进程达到 GB 级且可复现时，才按 GPU 纹理、临时 RenderTexture、特效对象和角色生命周期逐项优化。

本次原始采样（本地日志，未纳入版本库）：`logs/runtime-memory-20261002-baseline.tsv`、`logs/runtime-memory-20261002-footprint.txt`。

### 2026-10-02：三局连续基线复核

使用同一源码的 Development Build 连续运行三次完整无键鼠流程，覆盖两局战斗、大招、自动合照、重拍、预览断流恢复和再开一局。三局分别耗时 152.7、151.8、183.8 秒，均通过，且每局 `keyboard_mouse_events=0`、自动合照 2 次、重拍和再开局通过、照片预览 p99 差异为 0。

独立游戏进程每 5 秒采样一次，共 96 个有效样本：第一局 RSS 峰值约 950.6 MiB、结束约 442.4 MiB；第二局峰值约 948.7 MiB、结束约 388.0 MiB；第三局峰值约 774.2 MiB、结束约 410.0 MiB。三局没有按回合单调增长，采样最低约 355.3 MiB。Unity `[RuntimeMemory]` 在每局战斗稳定约 242–248 MB allocated、597–607 MB reserved，合照阶段约 252–261 MB allocated，回到新局战斗约 247.7 MB allocated；没有看到合照或重开后持续叠加。

本轮没有触发降低角色纹理、关闭特效或强制 `UnloadUnusedAssets`：当前证据更接近启动/图形资源峰值，而不是可复现的游戏内存泄漏。期间另有一次加入全部姿态噪声的压力回归在第二局出现一次护盾重新获取，日志同时出现 130–600 ms 长帧；它已与资源生命周期基线分开记录，不能据此宣称存在内存泄漏。三局原始采样：[memory-cycles-20261002.tsv](../logs/memory-cycles-20261002.tsv)；完整报告目录：[memory-cycles-20261002](../artifacts/memory-cycles-20261002)。P1 任务仍保留，后续若发行版单进程 footprint 达到 GB 级并随角色切换/合照持续增长，再针对 GPU 纹理、RenderTexture 和特效对象逐项优化。
