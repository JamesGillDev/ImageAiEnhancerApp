# ImageAiEnhancerApp

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-512BD4)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![ONNX Runtime](https://img.shields.io/badge/ONNX-Runtime-0E77B7)](https://onnxruntime.ai/)
[![Version](https://img.shields.io/badge/version-1.0.2-success)](./CHANGELOG.md)
[![License](https://img.shields.io/badge/License-BLS%201.1-blue.svg)](./LICENSE.md)

A local-first, fully offline Windows desktop app for image enhancement.

It supports:
- Basic enhancement (upscale + optional denoise + optional sharpen)
- AI upscaling via ONNX Runtime (CPU by default, optional DirectML GPU)
- Single image workflow and batch workflow with per-job status

No cloud APIs are required.

## Tech stack

- .NET 8 (LTS)
- WPF desktop app
- MVVM: `CommunityToolkit.Mvvm`
- Image processing: `SkiaSharp`
- AI inference: `Microsoft.ML.OnnxRuntime`
- Optional GPU on Windows: `Microsoft.ML.OnnxRuntime.DirectML`

## Solution layout

- `ImageAiEnhancerApp.sln`
- `src/ImageAiEnhancerApp.App` (WPF UI)
- `src/ImageAiEnhancerApp.Domain` (models/enums)
- `src/ImageAiEnhancerApp.Core` (pipeline/services)
- `Models/models.json` (model registry)

## Build and run

From repository root:

```powershell
dotnet restore ImageAiEnhancerApp.sln
dotnet build ImageAiEnhancerApp.sln
dotnet run --project src/ImageAiEnhancerApp.App/ImageAiEnhancerApp.App.csproj
```

## Local Windows publish (.exe)

Validated on Windows with a self-contained `win-x64` publish.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-local.ps1
```

Default local outputs for `v1.0.2`:

- `./artifacts/publish/win-x64/v1.0.2/ImageAiEnhancerApp.exe`
- `./artifacts/ImageAiEnhancerApp-win-x64-v1.0.2.zip`

## ONNX model setup

This app loads model entries from:

- `./Models/models.json`

Default entry included:

```json
[
  {
    "Name": "SuperResolution10",
    "Path": "Models/super-resolution-10.onnx",
    "Scale": 3,
    "Notes": "YCbCr luma-only; expects Y input; output is Y upscaled."
  }
]
```

For `onnxmodelzoo/super-resolution-10`:

1. Place the model file at `./Models/super-resolution-10.onnx`.
2. Ensure the entry above exists in `./Models/models.json`.
3. In the app, open **Models / Settings** and click **Reload Models**.
4. In Single or Batch tabs, select **Mode = AiUpscale** and **Model = SuperResolution10**.

The published app includes `Models/models.json`; only the `.onnx` weight file needs to be added separately.

## DirectML and fallback behavior

- CPU is always supported.
- If `Use GPU if available` is enabled and DirectML is available, app uses:
  - `SessionOptions.AppendExecutionProvider_DML(0)`
  - `GraphOptimizationLevel.ORT_ENABLE_ALL`
- If DirectML is unavailable, app automatically uses CPU.

## Model-specific preprocessing/postprocessing (SuperResolution10)

Implemented pipeline:

1. Resize input image to `224x224`.
2. Convert RGB to YCbCr.
3. Extract Y (luma), normalize to float32 `[0..1]`.
4. Create tensor shape `[1,1,224,224]`.
5. Run ONNX inference.
6. Read output Y tensor (expected `[1,1,672,672]`).
7. Upscale Cb/Cr to `672x672` with high-quality Skia resize.
8. Merge Y + Cb + Cr and convert back to RGB.

Detected model input/output names and metadata shapes are written to:

- `./App_Data/Logs/app.log`

## Batch logging

Each batch run writes a log file:

- `./App_Data/Logs/batch_yyyyMMdd_HHmmss.log`

## Safety behavior

- Originals are never overwritten.
- Output naming applies suffixes (default `_enhanced`) and collision-safe numbering.
- If output folder is same as input folder, suffixing is enforced.

## Release build (desktop)

Use the tracked publish script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-local.ps1
```

## Release versioning

- Current public release: `v1.0.2`
- Previous public release: `v1.0.1`
- Full history: [CHANGELOG.md](./CHANGELOG.md)
- Release notes: [v1.0.0](./artifacts/release-notes-v1.0.0.md)
- Release notes: [v1.0.1](./artifacts/release-notes-v1.0.1.md)
- Release notes: [v1.0.2](./artifacts/release-notes-v1.0.2.md)

For GitHub releases, create and push a tag per version:

```powershell
git tag -a v1.0.2 -m "Public release v1.0.2"
git push origin v1.0.2
```

## Roadmap

- Better AI tiling for very large images
- Better denoise/sharpen algorithms
- More ONNX model templates in `models.json`
- Saved presets
- Enhancement history panel

## License

This project is licensed under the **Business Source License 1.1 (BLS)**.
See [LICENSE.md](./LICENSE.md) for full terms.

- Additional Use Grant: None
- Change Date: 2029-01-01
- Change License: Apache License 2.0
