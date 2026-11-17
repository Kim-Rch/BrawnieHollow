# Verification — Brawnie Hollow 0.1.0

Verified on November 17, 2026, against the locally installed Stardew Valley and SMAPI assemblies.

## Automated checks

- Release build: zero warnings and zero errors.
- 36 simulation and policy assertions, including crop progression, season boundaries, idempotent daily work, loaded-data validation, durable recovery, warp ownership, and evening routes sampled against the packaged map's Buildings layer.
- 13 installed-assembly smoke checks for entry resolution, runtime dependencies, Newtonsoft serialization, recovery hooks, and installed warp/input/Lost-and-Found API compatibility.
- Asset checks: 36×28 map, complete layers, six valid vanilla texture references, three resident sprite sheets, and distinct farming frames.
- Exact ZIP-content and byte-freshness validation. No vanilla texture sheets, game assemblies, settings, saves, or build intermediates are included.

## Actual isolated game run

21 runtime assertions passed with no SMAPI errors. The test used a separate copied runtime, isolated mod directory and isolated game-data directory. No existing player save or installed mod was used or modified.

Verified in the running game:

- Village assets resolve and render using vanilla Stardew terrain, cabins, craftables and well textures.
- Three resident NPC sprite objects are created, without registering the transient location in the vanilla save graph.
- Bram's live loop reaches hoeing, sowing and watering; actual vanilla HoeDirt/Crop instances appear.
- Resident action interaction opens a real dialogue box; ordinary rival-field harvesting is suppressed.
- Menus freeze live work. Advancing the test world's dates produces a real harvest tally.
- Residents reach the evening hearth.
- A seven-item dropped stack with custom modData is recovered with a full inventory.
- The save-boundary handler returns the player synchronously and cancels a queued inbound village warp.
- SMAPI save-data write/read and runtime state reconstruction retain production.
- Normal visit/return still works after reconstruction.
- A real save file written only to isolated test storage can be deserialized, with settlement JSON and the recovered stack intact; no transient village is serialized.

The harness invokes lifecycle handlers directly for targeted save-boundary regressions and advances dates programmatically. This is not a claim of multi-season manual playtesting, full save-anywhere compatibility, or compatibility testing with the author's entire mod collection.

`village-working-ingame.png` and `village-evening-ingame.png` are actual game captures. `village-preview.png` is a static full-map art preview, not an in-game screenshot.

## Scope

Single-player prototype. Houses have no interiors; Mira and Pip do not generate goods; the long-term rival competition/economy is not implemented. Broad third-party mod compatibility and extended playtesting remain future work.
