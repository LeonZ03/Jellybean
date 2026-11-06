# 糖豆行走 · 后腿衔接与轻晃 · 2026-11-06

[播放、慢放与逐帧](index.html) · [实际向前走](walk-refined.mp4) · [十六个关键姿势](key-poses.png)

- 补画 8 个完整猫身的中间姿势，再统一整张母稿，共 16 个关键姿势，补足后腿蹬地、收腿、前摆的过程。近侧腿保持明亮，远侧腿保持较暗。
- 整套共用一个等比缩放值和水平基准，每行共用脚底基准。参考格高 169 像素，统一缩至 124 像素，不按每帧边界或金属环检测结果重新缩放、左右挪动。
- 对交错后腿比较后采用 `rife-anime`，在两倍桌面分辨率下进行两轮 2× 整帧补间，再缩回预览尺寸。
- 修正循环末尾多余停顿：17 个输入（含重复首姿势）生成 34、再生成 68 帧，只播放前 64 帧。每 4 帧准确保留一个原关键姿势，舍弃末尾 4 张重复帧。原因见 [RIFE 源码的时间映射](https://github.com/nihui/rife-ncnn-vulkan/blob/master/src/main.cpp#L676)：它使用输入数 / 输出数。
- 加入平滑、连续的上身位移：水平方向最多 0.7 像素、竖直方向最多 0.85 像素，脚底三行保持不动。处理的是完整画面，没有拆分身体部件，也没有周期性调整猫的大小。
- 网页和 MP4 为 60 fps，每步约 1.07 秒；GIF 为 50 fps。
- 向前走采用每周期 128 个原始像素的位移，地面刻度保持不动，方便检查滑步。该值是当前图稿的预览校准值。
- 本版用于确认行走。检查了完整循环中的头部与背线位置以及首尾接头；正式桌宠继续使用原动作。

导出核对：17 个输入关键位置（含循环接头）在每 4 帧处逐像素保留；实际循环 64 帧均不重复，去掉末尾停帧。向前走视频为 384 帧、60 fps。

## 动作依据

主要依据 [Bishop、Pai、Schmitt（2008）：Whole Body Mechanics of Stealthy Walking in Cats](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0003808)，尤其是 [S2 较直立的步行实拍](https://doi.org/10.1371/journal.pone.0003808.s002)。PLOS 论文与附件采用 CC BY；原视频署名归原作者。取其中约 3.8–6.2 秒的慢放片段研究一个周期，结合四脚接地关系重画，不是逐像素描摹或原视频换色。

角色外形沿用糖豆定稿：橘色虎斑、细长耳朵、细尾巴、粉色 H 背带。使用内置图像生成工具补画完整猫身的中间姿势，并统一整张图集。

原始[统一图集](../../assets/animations/walk-v4/whole-cat.png)与[完整提示词](../../assets/animations/walk-v4/prompts.json)保存在 `assets/animations/walk-v4/`。透明预览关键图在 [keys/](keys/)。网页内嵌图集，离线打开不依赖相邻图片加载。

## 制作工具

[RIFE ncnn Vulkan](https://github.com/nihui/rife-ncnn-vulkan)，MIT，Copyright (c) 2020 nihui。使用 [20221029 发布包](https://github.com/nihui/rife-ncnn-vulkan/releases/tag/20221029)的 `rife-anime` 模型和空间补间（`-x`），许可证见 [RIFE-MIT.txt](../../licenses/RIFE-MIT.txt)。下载包 SHA256：`D8E4D772D26CD8006EF0AD0BC82EB191B53C68677D1AE2F42506D74CBBBEA606`。

RIFE 和模型仅在本机制作预览时使用，不随桌宠分发。补间预览使用淡色底，透明原图另外保留；该预览图集不能直接替换桌宠的透明素材。

复现：解压工具至 `.tools/rife/`，安装 FFmpeg 后运行 `./scripts/walk-review.ps1`。`-Gpu` 指定本机显卡编号，`-Gpu -1` 使用 CPU。
