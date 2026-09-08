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

## 项目生成的美术与配乐

城市背景通过内置 imagegen 生成；2 首循环音乐与 7 种音效由本项目脚本编排合成。文件位置、完整生成提示词、音频重建方式及造型边界见 [美术与音频说明](docs/美术与音频说明.md)。未使用商业角色模型或影视原声。

## 角色参考与怪兽皮肤（2026-09-08）

通过 Bing 图片搜索查看的迪迦、哥尔赞商品图仅作本地建模参考，不包含在源码发布或游戏资源中。具体原图链接、用途、网格重建方式及生成提示词见 [角色造型参考](docs/角色造型参考.md)。`GolzaSkin.png` 为内置 image_gen 根据怪兽材质参考生成的通用岩石皮肤颜色贴图；角色网格由本项目代码构建，并非官方模型。
