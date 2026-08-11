SOACS Arsenal v1.5.4 Alpha

Changes in this build:
- Restored explicit foreground colors so tab, button, package-content, grid, and list text remains visible.
- Added deployment build progress bar and percentage.
- Added current phase, current file, copied bytes, and file counts.
- Added a live deployment build log.
- Package Contents now uses explicit high-contrast text.
- Deployment builder automatically creates and verifies the ZIP.
- Added a unique Deployment ID to the manifest and completion summary.

Open SOACS.Arsenal.sln in Visual Studio 2019 and build Debug or Release | Any CPU.

Deployment builder now removes its temporary staging folder after ZIP verification, leaving only the final ZIP.
