# Changelog

All notable changes to this project are documented in this file.

## [1.0.1] - 2026-02-26

### Added
- Centralized .NET project version metadata in `Directory.Build.props`.
- Public iteration tracking via `CHANGELOG.md`.
- Release notes document for `v1.0.1` at `artifacts/release-notes-v1.0.1.md`.

### Changed
- Release documentation to use versioned package naming for GitHub releases.

## [1.0.0] - 2026-02-25

### Added
- Initial public release of ImageAiEnhancerApp.
- Offline-first WPF app on .NET 8 with single image and batch enhancement workflows.
- Basic enhancement pipeline plus ONNX Runtime AI upscaling with optional DirectML.
- Model manager support using `Models/models.json`.
