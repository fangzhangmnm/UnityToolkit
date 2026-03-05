# LowRezRendering

`LowRezRendering` renders a camera into a low-resolution render texture and displays it via `RawImage`.

It is designed for pixel-look prototyping with minimal setup, and you can preview the low-res result directly in the editor (Scene view) before entering play mode.

## Quick Start

1. Add `LowRezRendering` component to a scene object.
2. Set or auto-assign:
- `targetCamera`
- `targetCanvas` (`RawImage`)
3. Pick a preset or set custom width/height.
4. Enable `syncCanvasScalerToResolution` if you want UI reference resolution to follow target size.

## Runtime API

```csharp
renderer.SetPreset(ResolutionPreset.GameBoyAdvance);
renderer.SetCustomResolution(320, 180);
renderer.ApplyCurrentSettings();
```

## Notes

- Uses point filtering by default for crisp low-res output.
- Supports low-res preview in editor (`previewInEditMode`) and scene view preview helper.
- If `targetTexture` is not provided, it creates and manages a runtime texture internally.
