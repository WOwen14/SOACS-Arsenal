# SOACS Arsenal Changelog

## v1.5.4 Alpha

### Changed
- Restored explicit foreground colors so tabs, buttons, package-content views, grids, and lists remain readable.
- Deployment Package Builder now reports current phase, current file, copied bytes, file counts, and overall percentage.
- Package Contents uses explicit high-contrast text.

### Added
- Deployment build progress bar.
- Live deployment build log.
- Automatic deployment ZIP creation and verification.
- Unique Deployment ID in the manifest and completion summary.

### Improved
- Temporary deployment staging folders are removed after successful ZIP verification so the final ZIP is the retained package artifact.

### Status
- Working Prototype / Alpha
- Assembly version: `1.5.4.0`
- .NET Framework 4.8
- WPF / Windows
