# 糖豆 Jellybean

纪念糖豆的 Windows 二维像素桌宠。离线运行，沿用确认的形象和粉色背带。

## 使用

Windows 11，x64。下载免安装压缩包，**完整解压，再双击 `Jellybean.exe`**。运行环境已包含在包内，保留旁边的文件和 `Assets` 文件夹。

| 操作 | 效果 |
| --- | --- |
| 单击糖豆 | 摸摸、眨眼和小爱心 |
| 双击 | 开始逗猫棒，跟着鼠标玩 20 秒；再双击或按 Esc 结束 |
| 按住拖动 | 换位置；在窗口顶边附近松开会停住，窗口移动时跟随 |
| 滚轮 | 调整大小 |
| 右键糖豆 / 托盘 | 动作、六个角度、显示隐藏、置顶、设置和退出 |

糖豆会自己走走、打盹、伸懒腰，也能跳到附近留有落脚空间的窗口。把糖豆放到窗口上后，右键选「从窗沿探头」。窗口关闭或最小化后会落下。

设置中可调整全屏收起、可选登录自启、定时休息提醒和每日提醒。默认安静、不自启、不开提醒。找不到糖豆时，再次双击 EXE，或从托盘选「把糖豆叫回来」。

设置保存在 `%LocalAppData%\Jellybean\settings.json`。免安装版需要保留原目录位置；搬动后若开启过自启，在设置里关掉再打开即可更新路径。

[首版动作预览](design/first-release/animations.png) · [设置面板](design/first-release/settings.png)

首版已检查窗口停驻、移动、消失后落地、跳窗、全屏识别和素材透明度，并验证了完整解压包独立启动。本机启动后 15 秒采样：工作集约 99 MiB，私有内存约 53 MiB，CPU 约单核 0.21%。鼠标手感与跨显示器体验仍待实际试用确认。

0.1 试用版尚未做代码签名。

## 开发

C# / WPF，.NET 10 SDK。复用 PixelPaws 的窗口识别、落地数学、显示器坐标和帧裁切，详见 [开源说明](THIRD-PARTY-NOTICES.md)。

```powershell
./scripts/build.ps1                  # 编译
./scripts/build.ps1 -Check           # 一次关键功能检查
./scripts/build.ps1 -Package         # 生成自带运行环境的免安装包
```

[功能表](REQUIREMENTS.md) · [形象与动画小样](demo/index.html) · [功能示意](design/features/index.html)
