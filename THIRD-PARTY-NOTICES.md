# Open-source acknowledgements

Jellybean reuses parts of [PixelPaws](https://github.com/jordansbc/pixelpaws), Copyright (c) 2026 PixelPaws contributors, under the MIT License.

- `Engine/Platforms.cs`: original standing and swept landing calculations.
- `Engine/SurfaceProvider.cs`: adapted window eligibility and platform enumeration; extended with window identities, visible edge segments and moving-window attachment.
- `Engine/DesktopGeometry.cs`, `Native/Win32.cs`, `UI/PetWindow.cs`: adapted monitor geometry and physical placement across different DPI settings.
- `Engine/SpriteCatalog.cs`: adapted frozen sprite atlas slicing.

The complete license is included in [licenses/PixelPaws-MIT.txt](licenses/PixelPaws-MIT.txt). PixelPaws character artwork is not distributed. Jellybean uses its own approved character and animation assets.

The standalone walk-review asset workflow uses [RIFE ncnn Vulkan](https://github.com/nihui/rife-ncnn-vulkan), Copyright (c) 2020 nihui, under the [MIT License](licenses/RIFE-MIT.txt). Its binary and models are local authoring tools and are not bundled with the desktop pet. See [walk-review provenance](design/walk-v4/SOURCES.md) for the real-motion reference and generation method.
