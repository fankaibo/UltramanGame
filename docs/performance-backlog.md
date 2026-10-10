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

### 2026-10-03：启动预热阻塞修复与引导回归

启动探针在加载梦比优斯/哥尔赞骨骼后进入 Metal 首帧渲染与 shader variant 等待，导致开发引导脚本误判为“没有进入游戏”。`PresentationWarmup` 现在默认不创建离屏 RenderTexture，也不在标题页前调用 `Camera.Render`；实时战斗仍使用完整渲染路径。完整开发版引导回归通过：158.1 秒、键鼠事件 0、两次自动合照、重拍、预览断流恢复和再开局通过；姿态噪声下没有误出拳，防御 4 次、大招 2 次。运行时遥测仍约 242–261 MB allocated、588–607 MB reserved，未观察到本修复造成的资源增长。

本次证据：[guided-validation.json](../artifacts/bilingual-finisher-guided/guided-validation.json)、[bilingual-finisher-guided.log](../logs/bilingual-finisher-guided.log)。这只证明启动阻塞和流程回归，不替代 P1 要求的发行版三局连续 footprint 采样。

### 2026-10-03：ARCADE-90 运行时资源审计与可复现采样器

本轮只审计运行时资源生命周期，没有修改 `ArenaController`、战斗规则、HUD 或角色素材。静态核对确认：`VictoryPhoto.Close` 释放 `PhotoClient`、`PhotoComposition`、实时人像与成片纹理；`PhotoComposition.Dispose` 解绑相机目标、释放 1920×1080/2K `RenderTexture`、材质和合照角色根节点；`PhotoClient` 只保留一个最新帧并在关闭时清空；`LocalPhotoEnhancement.TakeResultPng` 在 `Texture2D.LoadImage` 后清空编码结果；`ContactShadows`、`VolcanoEnvironment` 和 `RuntimeResources` 均有成对的 RenderTexture、材质、网格或 GameObject 清理路径。没有发现可以安全解释 GB 级持续增长的新泄漏，因此没有加入强制 `Resources.UnloadUnusedAssets` 或降低纹理质量的改动。

新增 [scripts/runtime_memory_profile.py](../scripts/runtime_memory_profile.py)，只使用子进程 PID 和 macOS `ps` 读取 RSS/VSZ，按局输出 TSV，并在每局结束后记录首个、峰值和最后一个有效 RSS；终止行的 `ps` 零值会被视为缺失，避免把回收误报成零内存。可用同一命令连续启动三次，命令参数中的 `{cycle}`、`{output}` 会替换为局号和独立输出目录，例如：

```sh
python3 scripts/runtime_memory_profile.py --cycles 3 --interval 5 \
  --output logs/memory-cycles-new.tsv --command \
  unity/Builds/TigaTraining.app/Contents/MacOS/迪迦体感训练场 \
  -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 \
  -logFile logs/memory-cycle-{cycle}.log
```

脚本本身已用三次短命令循环和现有 Unity 可执行文件做有界启动探针验证；Unity 探针在 8 秒上限后按预期终止，首个有效启动样本约 149 MiB，加载阶段约 234–761 MiB RSS，VSZ 约 421 GiB，说明脚本能采到实际玩家进程且不会把 VSZ 当作内存。该探针没有进入战斗或合照，不能替代完整流程。完整三局证据仍采用同一源码的 Development Build：第一/二/三局 RSS 峰值约 950.6/948.7/774.2 MiB，结束约 442.4/388.0/410.0 MiB；96 个有效样本没有按局单调增长，见 [memory-cycles-20261002.tsv](../logs/memory-cycles-20261002.tsv)。

验证：`python3 -m py_compile scripts/runtime_memory_profile.py`、`git diff --check` 通过；在允许本机回环和进程读取的环境执行 `bash scripts/check.sh`，39 个 Python 单元测试及完整规则检查共 **680 项**通过。受限沙箱中的同一命令曾因端口绑定和 `ps` 进程枚举 EPERM 产生环境错误，未计入代码失败。当前仍没有复现“单独游戏进程稳定占用 5 GB”；系统总内存读数继续按游戏、Unity、姿态服务、合照服务和审查缓存分项取证，P1 保留为待发行包三局/角色切换同进程复核。

### 2026-10-04：ARCADE-90 发行包同进程完整回放采样

为补上“采样器能运行但没有覆盖完整战斗”的证据边界，`scripts/guided_player_check.py` 增加可选的 `--memory-output` 和 `--memory-interval`。它在现有无键鼠回放的同一个 Unity 发行进程内每秒读取 macOS `ps` 的 RSS/VSZ，并写出带阶段、战斗相位、合照次数和命令行的 TSV；回放报告同时保存首个、峰值、末值和各阶段峰值。进程结束或 `ps` 暂时不可读时记为缺失，不把零值伪装成回收。

当前发行包回放命令：

```sh
python3 scripts/guided_player_check.py \
  --output artifacts/arcade90-release-memory \
  --log logs/arcade90-release-memory.log \
  --memory-output logs/arcade90-release-memory.tsv \
  --memory-interval 1.0
```

结果：144.7 秒，键鼠事件 0，自动合照 2 次，重拍、合照/预览断流恢复和再开局通过，照片预览 p99 差异为 0。游戏进程 141 个有效样本的 RSS 峰值约 **663.3 MiB**、末值约 **320.6 MiB**；阶段峰值为战斗 **663.3 MiB**、合照倒数 **301.4 MiB**、合照预览 **314.6 MiB**、再开局 **367.0 MiB**。VSZ 峰值约 **421.43 GiB**，只表示虚拟地址空间，不能当作实际内存。该样本没有显示回合间 RSS 单调增长，也没有复现独立游戏进程稳定占用 5 GB，因此本轮不降低角色/背景纹理、不关闭特效、不加入全局强制卸载。

验证：`python3 -m py_compile scripts/guided_player_check.py`、`git diff --check` 和允许本机回环环境的 `bash scripts/check.sh`（**680 项**）通过。原始采样见 [arcade90-release-memory.tsv](../logs/arcade90-release-memory.tsv)，摘要见 [arcade90-release-memory-summary.json](../logs/arcade90-release-memory-summary.json)，完整流程见 [guided-validation.json](../artifacts/arcade90-release-memory/guided-validation.json)。P1 仍保持 Review，下一步是同一进程三局连续、角色切换、Unity reserved/帧率和真实摄像头/小米电视现场采样。

### 2026-10-04：背景音乐导入的低风险内存优化

用户提供的《奇迹再现》MP3 原先按 `DecompressOnLoad` 导入，长达 236.095 秒的双声道文件会在 Unity 启动时扩展为 PCM；这不是已复现的 5 GB 泄漏，但会制造没有必要的常驻峰值。本轮增加 `UserMusicImportSettings`，每次重新导入同名文件时强制使用 `CompressedInMemory`、Vorbis、后台加载和 44.1 kHz；`GameAudio` 的音乐 `AudioSource` 本来就是 2D，运行时诊断同时记录 `musicProject=True` 和来源字符串。

Unity `AudioMixReview.ProjectMusic` 报告 `result=passed`，实际资源为 236.095 秒、双声道、44.1 kHz；15 秒混音录制和静音/暂停清理通过。Release 完整回放 `artifacts/arcade60-music-compressed-release/guided-validation.json` 通过，154.6 秒、键鼠事件 0、自动合照 2 次、重拍/再开局通过；日志记录 `musicProject=True musicSource=项目内导入《奇迹再现》`。这项改动只降低背景音乐的导入驻留方式，没有降低角色或场景画质；P1 仍需要同一进程角色切换、Unity reserved 和现场采样后才能关闭。

### 2026-10-04：ARCADE-90 发行回放采样竞态与持久英雄选角修正

上一轮发行版内存采样的首行曾读到进程创建竞态的 0.2 MiB；本轮 `summarize_memory()` 保留原始 TSV，但在摘要基线、末值、阶段峰值和差值中排除 RSS 小于 16 MiB 的样本，并输出 `summary_samples`，避免把进程尚未完成 Unity 映射的瞬间当作真实低水位。采样对象仍是游戏 PID；VSZ 只记录为虚拟地址空间，不计作实际 RAM。

同时，发行包会记住上一次可用英雄。回放验证器以前固定向右挥手，当前英雄为格力乔时会落到尚未导入的泽塔占位卡，游戏正确地不发选角事件，但验证器误报失败。现在先读取运行日志中的当前英雄：格力乔向左切到已有的捷德，其余已知可用起点向右切换；合照后选角姿态窗口从 7.4 秒扩展到 8.8 秒，以覆盖采样/Metal 首帧长帧，不改变游戏自身手势门禁。

发行版完整回放命令仍为：

```sh
python3 scripts/guided_player_check.py --gesture-wobble --gesture-entry-noise \
  --output artifacts/arcade76-memory-summary-release \
  --log logs/arcade76-memory-summary-release.log \
  --memory-output logs/arcade76-memory-summary.tsv --memory-interval 1.0
```

结果：157.4 秒，键鼠事件 0，自动合照 2 次，重拍、合照/预览断流恢复、再开局和选角重置通过；防御/大招姿势噪声下误出拳 0，格挡 3，大招 2，照片预览 p99 差异 0。155 个原始 RSS 样本中 154 个用于摘要，游戏进程 RSS 首个有效值约 95.6 MiB、峰值 **682.2 MiB**、末值 271.0 MiB；阶段峰值为战斗 **682.2 MiB**、合照 **376.9 MiB**、预览 294.2 MiB、再开局 290.7 MiB。VSZ 峰值约 **422.02 GiB**，是虚拟地址空间，不能当作物理内存。

验证：`python3 -m py_compile scripts/guided_player_check.py`、`git diff --check`、允许本机回环环境执行的 `bash scripts/check.sh`（**698 项**）通过；代码提交为 `0714d18`。当前仍没有复现独立游戏进程稳定占用 5 GB；ARCADE-90 继续保留角色切换、Unity reserved、稳定 60 FPS 和真实摄像头/小米电视现场专项。

### 2026-10-09：烟团表现的新增开销（A-20261009-64）

新增逐团上升烟雾后，首版实际回放的两个渲染窗口降至 29.7 / 31.5 FPS。`1a14fa5` 将烟团位置、半径及存活权重移到舞台每帧计算，两组数组复用上传，避免每个像素的多个射线采样重复三角函数和幂运算；三张代表画面优化前后最大单通道差仅 1 / 255。

最终完整发行回放通过，窗口为 44.9 / 52.5 FPS；此前旧版记录为 51.3 / 57.2。不同运行时刻、截图和机器负载会影响这些数字，不能直接推算成本比例，也不能认为已完成 60 FPS。后续性能窗口需对同镜头的开启／关闭烟团做 GPU 剖析，关注三次密度查询、动态数组索引与大招近景覆盖率，优先评估阴影采样复用及降采样体积合成；保留 1080P/2K 输出和烟团质量。详见 [本轮过程](火山烟团上升与翻卷.md)。


### 2026-10-10：A-78 回放计时异常待分离

`1a3e072` 发行包按键回放外部总耗时 96.89 秒，玩法及所需图像验证通过；但 Unity `[FrameTiming]` 日志在十余秒处连续跳至 1079、2126、3147 秒，后续出现 0.1 / 0.3 FPS 聚合。两种时钟明显不一致，本轮不把该数值解释为真实游戏帧率或性能优化成功。保留 `artifacts/cinematic-combat/player/20261009T235006177380Z/player.log` 和 `validation.json`，后续性能专项需要隔离宿主时钟跳变、截图成本、正常前台运行和真实长帧，再确定是否存在渲染回退。未因此降低素材或增加性能改动。


### 2026-10-10：A-80 验证宿主长停顿记录

侧身跟随源码 `a54d1e1` 的五英雄离线审查与核心检查通过，但三次完整体感发行回放未完成。第三轮发布样本中位间隔 41 ms、p95 240 ms，43 次超过 1 秒、最长 6436 ms；系统同期 `vm.swapusage` 为 11972.75 MiB，不能当成游戏进程占用。第二轮还定位到长间隙导致合照重新放手门禁复位，测试玩家没有遵循页面提示，已独立修复夹具。

保存 `artifacts/live-body-20261010/guided-blockers.json` 和三个失败目录，不宣称新包稳定 60 FPS 或完整续局已验收。后续先在负载较稳时复跑同包、记录实际投递及渲染时间，再区分宿主内存压力、截图开销和游戏自身长帧；本轮不关闭用户应用、不降低人物／舞台质量，性能专题仍按用户要求后置。


### 2026-10-10 / A-81 同包复验补充

- 失败 420 秒与成功 319.1 秒两轮均有长发布间隙（最大 14.701／10.906 秒），同时存在 Python 预览／照片发布耗时和 Unity 长帧。不能将全部异常归为游戏显示层，也不能仅凭系统交换量下结论。
- 同进程 RSS 峰值分别 775.6／1014.9 MiB；成功后的新局进入 Battle 又出现追踪暂停。本次只补流程证据，不验收稳定 60 FPS、不宣布内存任务解决。游戏原始日志、姿态包和内存采样位于 `artifacts/live-body-20261010/guided-recheck/` 与 `guided-cue-recovery/`。

## 2026-10-11 A-91 发行回放观察

最终烟团改动发行回放 `artifacts/cinematic-combat/player/20261010T203636101418Z/validation.json` 的两个渲染窗口为 45.4／47.3 FPS；A-90 为 53.7／57.7。两者含同步 PNG 截图，设备连续运行状态未控制，因此只是下降信号，尚不能归因于烟团参数或宣称实际游玩下降相同比例。后续做无截图、同包/同场景、交替旧新参数的受控对比并记录 GPU/CPU 帧时间；不把本轮视觉改动算作 60 FPS 达标。用户要求帧率专项后置仍适用，若出现影响主线的持续卡顿则提前处理。

## 2026-10-11 A-92 同进程三局 RSS

同一 PID 57306 连续运行 369.8 秒，三次完整胜利、四张合成合照、三次续局及第四局开场；184 个 RSS 样本峰值 746.0 MiB、结束 330.0 MiB，逐局战斗后 10 秒中位数 499.0／355.3／313.9 MiB，未观察到逐局增长。首局截图更多，不能将下降称为优化成果。证据 `artifacts/tiga-three-rounds-20261011/memory.tsv` 与 `round-audit.json`。此次补齐同进程自动流程，未覆盖真人摄像头、AI 美化或换角色，也不等于 Unity reserved／图形资源总量或系统监控的 5 GB；完整性能专项继续保留。
