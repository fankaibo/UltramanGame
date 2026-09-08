# 第三方资源

## Noto Sans SC Regular

- 来源：[Noto CJK 官方仓库](https://github.com/notofonts/noto-cjk/blob/main/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf)
- 上游原文件，未修改字体；本地路径：`unity/Assets/Resources/Fonts/NotoSansSC-Regular.otf`
- SHA-256：`faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9`
- 许可证：SIL Open Font License 1.1，全文随字体保存在 `unity/Assets/Resources/Fonts/OFL.txt`；字体内部保留上游版权信息。
- 字体单独沿用其许可证，仓库的 Apache-2.0 代码许可证不替换字体许可证。

## MediaPipe Pose Landmarker Lite

模型来源与固定 SHA-256 见 `models/manifest.json`。模型通过准备脚本下载到本机，不纳入 Git。MediaPipe 及 Python 依赖版本固定在 requirements 文件中，各依赖沿用各自许可证。

## 本机生成语音

固定台词由 `scripts/generate_voice.py` 调用用户本机的 macOS Tingting 声音生成。生成的音频不纳入 Git，不包含商业角色配音录音。
