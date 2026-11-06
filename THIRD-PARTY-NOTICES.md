# Open-source acknowledgements

Jellybean reuses parts of [PixelPaws](https://github.com/jordansbc/pixelpaws), Copyright (c) 2026 PixelPaws contributors, under the MIT License.

- `Engine/Platforms.cs`: original standing and swept landing calculations.
- `Engine/SurfaceProvider.cs`: adapted window eligibility and platform enumeration; extended with window identities, visible edge segments and moving-window attachment.
- `Engine/DesktopGeometry.cs`, `Native/Win32.cs`, `UI/PetWindow.cs`: adapted monitor geometry and physical placement across different DPI settings.
- `Engine/SpriteCatalog.cs`: adapted frozen sprite atlas slicing.

The complete license is included in [licenses/PixelPaws-MIT.txt](licenses/PixelPaws-MIT.txt). PixelPaws character artwork is not distributed. Jellybean uses its own approved character and animation assets.

The standalone walk-review asset workflow uses [RIFE ncnn Vulkan](https://github.com/nihui/rife-ncnn-vulkan), Copyright (c) 2020 nihui, under the [MIT License](licenses/RIFE-MIT.txt). Its binary and models are local authoring tools and are not bundled with the desktop pet. See [walk-review provenance](design/walk-v4/SOURCES.md) for the real-motion reference and generation method.

The separate [motion-reference preview](design/motion-reference/index.html) includes the following CC0 public-domain assets and derived reference renders. They are authoring references, not the approved Jellybean character or application runtime assets:

- **Quaternius**, [Animated Animales Low Poly / Animal Pack Vol.2](https://opengameart.org/content/animated-animales-low-poly), [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). The original `Cat.blend` is retained in `assets/motion-reference/quaternius/`; derived renders and foot-control traces are in `design/motion-reference/`.
- **Shepardskin**, [Cat sprites](https://lpc.opengameart.org/content/cat-sprites), [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). Original walk/run GIFs and sprite sheet are retained in `assets/motion-reference/shepardskin/`. Preview strips are extracted from those GIFs.

The candidate Frakassets pack is only linked for evaluation and is not distributed here. Its CC BY 4.0 attribution requirements apply if it is imported later. See [source records](design/motion-reference/SOURCES.md).
