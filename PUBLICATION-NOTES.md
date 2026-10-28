# Publication notes — 3.3.2

This release publishes the minimap, global shortcut handling and automatic map selection introduced locally in 3.3.0–3.3.2, including the confirmed Interchange scene-alias fix. README and release notes are English first, followed by Polish information.

The source/runtime archives retain required licenses, bundled dependencies and map attribution. Documentation images include sample-data renders and the user-supplied gameplay capture with its private-path notification redacted. The original unredacted capture is not distributed.

Package-Release.ps1 includes docs and only reports/<VERSION>. It excludes runtime caches and preferences, and removes compiled executables from the source archive. The Windows archive includes BUILD-INFO.json; SHA256SUMS.txt identifies the two ZIPs.

The existing main-branch VERSION-triggered workflow builds, checks and publishes a new prerelease with the scoped GitHub Actions token. Existing releases and assets are not overwritten. The 3.3 checks add minimap and automatic-map coverage to the previous CI checks. Workflow edits also trigger validation. The hosted runner uses noninteractive mode for overlay desktop-presentation checks; local desktop assertions remain available. Environment-dependent shortcut conflicts are reported by the tests without taking shortcuts away from other running software.

The installed local application is not replaced by this publication. CI creates its own executable from the same application source.
