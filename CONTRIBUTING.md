# Contributing to SOACS Arsenal

SOACS Arsenal uses a simple controlled branch workflow intended to keep `main` stable while allowing active development and testing.

## Branches

- `main` — stable repository baseline
- `develop` — integrated development branch
- `feature/<short-description>` — isolated feature or fix branches

## Normal workflow

1. Create a feature branch from `develop`.
2. Make and test the change on the feature branch.
3. Open a pull request from the feature branch into `develop`.
4. Test the integrated `develop` build.
5. Promote tested changes from `develop` to `main` through a pull request.
6. Create a tag/release only after the version is verified against the source metadata.

## Development expectations

Changes should preserve Arsenal's offline/disconnected operating model and avoid introducing unnecessary online dependencies.

For deployment-engine changes, verify at minimum:

- package discovery and classification
- SHA-256 validation
- install ordering
- continue-on-error behavior
- reboot-required handling
- standalone deployment package creation
- deployment ZIP verification
- manual-review behavior for unsupported executables
- firmware/BIOS safeguards

## Public repository safety

Do **not** commit:

- credentials, passwords, API keys, tokens, or private certificates
- customer or site-specific configuration
- operational IP addresses or network diagrams
- real patch repositories or licensed software payloads
- generated deployment ZIPs
- deployment logs or reports containing machine/user information
- firmware packages
- local application rule files containing environment-specific data

If sensitive information is accidentally committed, removing the current file is not sufficient if it remains in Git history. Rotate any exposed credential and clean the repository history before continuing.

## Versioning

Keep these locations aligned before a release:

- `Properties/AssemblyInfo.cs`
- visible application version text
- README/release documentation
- Git tag and GitHub release title

The current source baseline is **v1.5.4 Alpha**.
