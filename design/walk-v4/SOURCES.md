# 糖豆行走重画 · 2026-11-06

[播放、慢放与逐帧](index.html) · [实际向前走](walk.mp4) · [八个关键姿势](key-poses.png)

- 8 个完整猫身关键姿势分别绘制，区分近侧与远侧四肢的支撑和前摆；不拆身体或四肢。
- 每张只做整图等比缩放与定位，以背带金属环对齐躯干，脚底对齐地面；参考站姿高度 124 像素。
- 用 RIFE 对相邻完整画面补间，含末帧到首帧，共 64 帧循环。网页和 MP4 为 60 fps，每步约 1.07 秒；GIF 为 50 fps。
- 向前走采用每周期 128 个原始像素的位移，地面刻度保持不动，方便检查滑步。该值是当前图稿的预览校准值。
- 本版用于确认行走。整帧补间可能使交叉的腿和花纹局部变软，仍需结合播放判断；正式桌宠继续使用原动作。

## 动作依据

主要依据 [Bishop、Pai、Schmitt（2008）：Whole Body Mechanics of Stealthy Walking in Cats](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0003808)，尤其是 [S2 较直立的步行实拍](https://doi.org/10.1371/journal.pone.0003808.s002)。PLOS 论文与附件采用 CC BY；原视频署名归原作者。取其中约 3.8–6.2 秒的慢放片段研究一个周期，结合四脚接地关系重画，不是逐像素描摹或原视频换色。

角色外形沿用糖豆定稿：橘色虎斑、细长耳朵、细尾巴、粉色 H 背带。图稿由图像生成工具按每张姿势分别生成，并修正后腿近远侧关系和透明背景。

原始关键图与[完整提示词](../../assets/animations/walk-v4/prompts.json)保存在 `assets/animations/walk-v4/`。透明预览关键图在 [keys/](keys/)。网页内嵌图集，离线打开不依赖相邻图片加载。

## 制作工具

[RIFE ncnn Vulkan](https://github.com/nihui/rife-ncnn-vulkan)，MIT，Copyright (c) 2020 nihui。使用 [20221029 发布包](https://github.com/nihui/rife-ncnn-vulkan/releases/tag/20221029)的 `rife-v4.6` 模型，许可证见 [RIFE-MIT.txt](../../licenses/RIFE-MIT.txt)。下载包 SHA256：`D8E4D772D26CD8006EF0AD0BC82EB191B53C68677D1AE2F42506D74CBBBEA606`。

RIFE 和模型仅在本机制作预览时使用，不随桌宠分发。补间预览使用淡色底，透明原图另外保留；该预览图集不能直接替换桌宠的透明素材。

复现：解压工具至 `.tools/rife/`，安装 FFmpeg 后运行 `./scripts/walk-review.ps1`。`-Gpu` 指定本机显卡编号，`-Gpu -1` 使用 CPU。
