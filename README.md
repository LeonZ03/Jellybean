# 糖豆 Jellybean

纪念糖豆的 Windows 二维像素桌宠。离线运行，沿用确认的形象和粉色背带。

当前试用版：**v0.2.0**。[本轮改动](CHANGELOG.md) · [连续动画预览](design/second-release/motion-preview.mp4)

## 使用

Windows 11，x64。下载免安装压缩包，**完整解压，再双击 `Jellybean.exe`**。运行环境已包含在包内，保留旁边的文件和 `Assets` 文件夹。

| 操作 | 效果 |
| --- | --- |
| 单击糖豆 | 摸摸、眨眼和小爱心 |
| 双击 | 开始逗猫棒，跟着鼠标玩 20 秒；再双击或按 Esc 结束 |
| 按住拖动 | 保持抓起时的姿势换位置；放开后落地缓冲，在窗沿松开可停住 |
| 滚轮 | 调整大小 |
| 右键糖豆 / 托盘 | 动作、六个角度、显示隐藏、置顶、设置和退出 |

糖豆持续有轻微呼吸和耳朵微动，会自己走走、打盹、伸懒腰，也会随机选择不同窗口跳过去。趴在窗沿时，细尾巴会垂进窗口。很久没有互动时，它会轻轻问「陪我玩一会儿，好不好？」。

大小滑块边拖边生效。设置可选登录自启、定时休息提醒和每日提醒；默认安静、不自启、不开提醒。全屏办公和视频继续显示，识别到全屏游戏时才收起。找不到糖豆时，再次双击 EXE，或从托盘选「把糖豆叫回来」。

设置保存在 `%LocalAppData%\Jellybean\settings.json`。已开启自启时，从新目录运行一次会更新启动位置。

[首版动作预览](design/first-release/animations.png) · [设置面板](design/second-release/settings.png)

v0.2 已检查持续呼吸、动作融合、步幅与位移同步、拖动保姿、落地与起身、多窗口跳转、大小即时回调和闲置求陪玩。跨显示器体验和主观动作手感继续根据试用反馈调整。

试用版尚未做代码签名。

## 开发

C# / WPF，.NET 10 SDK。复用 PixelPaws 的窗口识别、落地数学、显示器坐标和帧裁切，详见 [开源说明](THIRD-PARTY-NOTICES.md)。

```powershell
./scripts/build.ps1                  # 编译
./scripts/build.ps1 -Check           # 一次关键功能检查
./scripts/build.ps1 -Package         # 生成自带运行环境的免安装包
```

[功能表](REQUIREMENTS.md) · [形象与动画小样](demo/index.html) · [功能示意](design/features/index.html)
