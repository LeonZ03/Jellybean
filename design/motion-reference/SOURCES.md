# 现成猫动作参考 · 2026-11-07

[离线对照预览](index.html) · [连续动作视频](walk.mp4)

本轮改为复用作者已做好的完整行走循环。**这些是动作底稿，不是糖豆的新外观定稿。** 正式桌宠仍用 v0.3.1，糖豆新动作继续按已确认的二维外形制作，先确认再替换。

| 来源 | 许可 | 实际核对与用途 |
| --- | --- | --- |
| [Quaternius / Animated Animales Low Poly](https://opengameart.org/content/animated-animales-low-poly) | [CC0](https://creativecommons.org/publicdomain/zero/1.0/) | 已下载原始 `Cat.blend`，含 `Idle`、`Walking` 和四脚控制点。可复用连续姿势、四脚轨迹及多角度渲染方法。 |
| [Shepardskin / Cat sprites](https://lpc.opengameart.org/content/cat-sprites) | [CC0](https://creativecommons.org/publicdomain/zero/1.0/) | 已下载走路、跑步 GIF 与原始图集。各 6 帧，分别按原始 5 / 10 fps 播放；对照支撑与收腿节奏，分辨率不足以直接作为糖豆成品。 |
| [Frakassets / Free Pets Pack](https://frakassets.itch.io/free-pets-pack) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)，署名 Frakassets | 页面列有橘猫四方向、待机、行走、开心和睡觉及 Aseprite JSON。页面和许可已核对；原包下载遇到限流，尚未逐帧验收，未导入仓库。 |

## 本轮预览

- 保留 Quaternius 原始 `Walking` 动作曲线、关节与约束，只添加固定正交相机；没有改写原始工程。沿 40 个源时间单位、24 fps 的循环，采样 100 张完整猫身，按 60 fps 播放，时长 1.67 秒。
- 所有帧共用 384×288 画布与相机，没有逐帧改大小，没有分件拼接，没有光流补帧。模型仅用于制作二维动作参考，桌宠运行时不增加 3D 引擎。
- 轨迹中的四脚身份固定。实心点仅表示脚部控制点高度低于源模型的 0.10 单位，不是精确脚掌接触检测；不能凭这个指标判断动作已经自然。
- 原作者动作仍有风格化处理，背部和尾巴也与糖豆不同。后续要适配糖豆体型，重做背带与表情，不能把这只灰猫当成品。

下一步：选定动作循环 → 按糖豆定稿输出完整二维帧 → 对照脚底位移匹配前进速度 → 看新小样后接入桌宠。先解决连续动作，避免对不连贯关键图反复补帧。

## 原始文件与复现

原始素材存放在 [`assets/motion-reference/`](../../assets/motion-reference/)，不含糖豆私人照片。

- [Quaternius 原包](https://opengameart.org/sites/default/files/Animal%20Pack%20Vol.2%20by%20%40Quaternius.zip)：提取 `Animal Pack Vol.2 by @Quaternius/Blends/Cat.blend`，未修改。SHA256：`E9836027DC8591FD2DA03E6D8D6575972BC5C7AEBAA17A0762B82F7BC37264DE`。
- [Shepardskin 原包](https://lpc.opengameart.org/sites/default/files/cat%20sprite.zip)：保留 `catwalkx4.gif`、`catrunx4.gif`、`catspritesoriginal.gif` 原始字节，仅重命名。`walk.gif` SHA256：`CDF3AB76776A1FDF02926D2C3FCDB3FA60F2147B0F77468F554E9D34D15CB510`。
- Blender 5.2 LTS 打开原始 `Cat.blend`，执行 [`export_quaternius.py`](../../tools/MotionReference/export_quaternius.py) 的 `export()`，输出到 `artifacts/motion-library/quaternius/frames`；再运行 [`build_preview.py`](../../tools/MotionReference/build_preview.py)，需要 FFmpeg。网页内嵌图集和数据，可直接离线打开。

```powershell
blender --background assets/motion-reference/quaternius/Cat.blend --python tools/MotionReference/export_quaternius.py -- artifacts/motion-library/quaternius/frames
python tools/MotionReference/build_preview.py
```

CC0 素材无需强制署名，本项目保留作者与来源。Frakassets 尚未使用；若后续导入，应保留 CC BY 4.0 署名、许可链接和修改说明。
