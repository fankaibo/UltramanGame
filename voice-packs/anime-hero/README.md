# 热血青年英雄

用户在两段实声预览中选择的 A 音色，用于游戏的 12 条中文引导和战斗提示。它是新设计的合成角色声音，不标称任何动漫人物或演员的影视原声。迪迦大招短战吼单独保存在本机 Unity 资源中，不属于这个语音包。

`reference.wav` 是用户试听选中的原始合成样音，由 [Qwen3-TTS VoiceDesign](https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign) 在官方公开演示页生成。后续台词以该合成样音为参考，使用 [Qwen3-TTS Base 的 MLX 4-bit 模型](https://huggingface.co/mlx-community/Qwen3-TTS-12Hz-1.7B-Base-4bit) 在本机生成。没有使用孩子、家长或演员的录音进行音色复制。

`manifest.json` 记录设计描述、参考文本、模型、参数、12 条台词、时长与 SHA-256。成品均为 24 kHz、单声道、16-bit PCM WAV，总时长约 29 秒。修整首尾空白、统一音量并保留峰值余量，防御预警为 2.24 秒，短于怪兽 2.4 秒蓄力提示。

在项目根目录运行 `python3 scripts/generate_voice.py` 即可离线校验、转换和安装。常规 macOS 构建会自动执行；不依赖生成模型、Python 语音工具或在线服务。`reference.wav` 仅记录音色来源，不会打包进 Unity。
