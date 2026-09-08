# UltramanGame · 迪迦体感训练场

给 4 岁孩子玩的家庭体感游戏。Mac 前置摄像头识别身体动作，Unity 显示英雄与怪兽，小米电视作为外接屏幕。孩子挥拳、双手防御、摆姿势发射光线；怪兽会提前预警，受击后恢复，首版没有失败。

## 当前进度

这是 **M0 开发原型**，已经编写本地姿态服务、Unity 自制可动人形与战场、动作识别和战斗状态机。英雄目前是为验证动作制作的红紫银配色简模，迪迦正式造型、完整变身演出和美术资源仍需继续完成。

Python、C# 规则和回环通信有自动检查。Unity 编辑器内的编译、画面与相机实测进度见 [验证记录](docs/验证记录.md)。核心代码测试通过不代表 Unity 画面已经验证。

- [已确认需求与首版方案](docs/首版游戏方案.md)
- [关键点通信协议与动作规则](docs/实现说明.md)

## 本机启动

需要 Python 3.12（使用 uv 管理）、Unity 6 编辑器。工程当前目标版本为 `6000.3.23f1`，实际使用其他 Unity 6 版本时应在编辑器导入后检查并提交版本记录。自动测试另外使用 .NET 8 SDK。

### 1. 准备本地识别环境

```bash
./scripts/setup.sh
```

脚本在项目内创建 `.venv`，安装固定依赖，从 Google 官方下载姿态模型并校验 SHA-256。首次准备需要联网；后续相机识别在本机执行。uv 安装方式见 [uv 官方文档](https://docs.astral.sh/uv/getting-started/installation/)。

### 2. 启动相机

```bash
./scripts/camera.sh
```

macOS 首次使用时需要为实际启动 Python 的应用允许相机访问。预览窗口显示镜像画面和手臂关键点，按 `Q` 或 `Esc` 结束。确保肩膀、手肘、双手进入画面，先让一个人在镜头前试玩。

相机预览目前是独立调试窗口，尚未合成进 Unity 的变身画面。图像不通过网络发送、不自动录制或保存；姿态服务仅监听 `127.0.0.1:8765`。

### 3. 打开 Unity 工程

在 Unity Hub 中添加本仓库的 `unity` 文件夹。打开工程后，选择 **UltramanGame → Open Arena**，再按 Play。场景在运行时创建，不需要手动绑定对象。

默认使用摄像头输入：先保持肩膀和手臂可见，再双手举高触发变身。挥拳前收手；防御时双手靠近胸前；能量满后，一只前臂竖起、另一只前臂横在胸前，保持片刻释放光线。

摄像头暂时不可用时，可以点击游戏底部的 **键盘练习**，明确切换到键盘模式：

| 按键 | 操作 |
| --- | --- |
| 空格 | 变身 |
| A / D | 左拳 / 右拳 |
| 按住 S | 防御 |
| J | 能量充足时释放光线 |
| Esc | 暂停 / 继续 |
| R | 重新开始 |
| F2 | 切换摄像头与键盘模式，并重新开始 |

### 4. 生成固定中文引导语音（macOS）

```bash
.venv/bin/python scripts/generate_voice.py
```

使用本机安装的 `Tingting` 系统声音生成 8 条固定台词，没有模仿角色演员的声音。生成音频保存在 `unity/Assets/Resources/Voice`，不纳入 Git；重新克隆后运行脚本即可生成。没有音频时仍有字幕和短提示音。

### 5. 连接电视

先在电脑上完成动作测试，再用适配的 USB-C 转 HDMI 线或转接器连接电视。笔记本保持开盖，放在电视附近，摄像头朝向孩子。电视模式与端到端延迟需要在实际设备上调试。

## 验证与调试

```bash
# 规则、协议和 Python → C# 本机通信；不会打开相机
./scripts/check.sh

# 真实模型加载与推理；输入是生成的黑色画面，不打开相机
MPLCONFIGDIR="$PWD/.cache/matplotlib" .venv/bin/python scripts/smoke_model.py

# 向 Unity 发送明确标记的合成动作，不打开相机
./scripts/camera.sh --demo

# 短时相机验证，不显示预览、不保存画面
./scripts/camera.sh --seconds 5 --no-preview
```

GitHub Actions 执行无需摄像头的自动检查。Unity 编辑器安装完成后，还应在编辑器内编译、进入 Play 模式和运行真实摄像头联调。

## 项目结构

```text
vision/                  Python 摄像头、MediaPipe、合成姿态与本机服务
unity/Assets/Scripts/Core 不依赖 Unity 的姿态校验、动作判定、战斗和 TCP 客户端
unity/Assets/Scripts/Runtime 场景、角色、画面、声音与输入适配
unity/Assets/Editor/      工程配置与 macOS 构建入口
tests/                   协议、规则和通信验证
scripts/                 环境准备、启动、语音生成与检查
models/manifest.json     官方模型来源与固定校验值
docs/                    需求、实现和验证记录
```

仓库沿用初始的 Apache-2.0 代码许可证。第三方模型按其发布方说明使用；本仓库不包含街机游戏文件、商业角色模型或从宣传视频提取的素材。
