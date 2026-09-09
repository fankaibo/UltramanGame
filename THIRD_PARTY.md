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

固定台词默认安装用户选定的热血青年英雄语音包，也可选择 macOS Tingting。用户确认的短迪迦战吼作为本机可选音频独立安装，不纳入 Git。详见 [美术与音频说明](docs/美术与音频说明.md)。

## 项目生成的美术与配乐

城市背景通过内置 imagegen 生成；循环音乐和战斗音效由本项目脚本编排合成。文件位置、生成提示词、音频重建方式及造型边界见 [美术与音频说明](docs/美术与音频说明.md)。这些生成文件与下述第三方模型、本机可选原声音频分别记录。

## 角色动作插画（2026-09-08）

通过 Bing 找到的迪迦、哥尔赞商品照片作为外形参考，由内置 image_gen 制作八姿势战斗图集。当前为背侧视角的 `TigaRear45Actions.png` 和正面 `GolzaActions.png`；背侧图集由本项目旧版迪迦正面图集编辑生成。原始商品照片不包含在源码或游戏包中；角色动作插画包含在游戏资源中。它们是本项目生成的角色插画，并非官方游戏素材或影视录像。参考链接、提示词和渲染方式见 [角色造型参考](docs/角色造型参考.md)。迪迦、哥尔赞及相关角色权利仍属于原权利人，代码许可证不授予这些角色的权利。

## 自选音乐

《奇迹再现》未随本仓库提供。游戏支持用户选择本机普通音频，只在本地播放、保存路径，不上传或纳入版本控制；音乐本身的权利不因导入而改变。

## 骨骼角色模型

- 迪迦由 Extrazhang 发布于 [BlendSwap](https://blendswap.com/blend/26877)，作者标注 CC-BY-NC。原作纹理、转换和改编动画的记录见 `unity/Assets/Resources/Characters/Tiga/ATTRIBUTION.txt`。
- 哥尔赞由 TengenGenesic 上传至 [SFMLab](https://sfmlab.com/project/5e6c0302-9b99-4e34-819f-cd0c5fbcc041/)，源文件路径另署名 ultimo。页面 CC0 标记由上传者选择，网站明确表示未核实；不代表官方商业授权。来源、改编与哈希见 `unity/Assets/Resources/Characters/Golza/ATTRIBUTION.txt`。
- 两项第三方模型均不适用本仓库的 Apache-2.0 代码许可证；角色权利仍属于原权利人。SourceIO/Blender 仅为本机转换工具，未打包进玩家应用。游戏 F5 页面及构建包保留模型署名。
