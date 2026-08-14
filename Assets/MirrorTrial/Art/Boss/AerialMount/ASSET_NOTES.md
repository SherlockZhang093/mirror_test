# Mirror Bird Aerial Mount — Visual Asset Notes

This package contains visual-only resources for phase one of the aerial archer boss. The bird is a passive carrier: it has no attack animation and no weaponized body action. Combat behavior, health, movement, impact timing, rider detachment, and transition to the ground boss remain owned by gameplay code outside this package.

## Production specification

- Mount frame canvas: `192 × 128 px`
- Rider reference canvas: `96 × 84 px`
- Pixels per unit: `32`
- Mount visual root: pixel `(96, 66)` from the top-left
- Stable rider seat: pixel `(108, 72)` from the top-left; prefab local position `(0.375, -0.1875, 0)`
- Ground contact baseline: pixel `118`
- Sprite import: Multiple, Point filter, mipmaps disabled, uncompressed, Full Rect
- Background: transparent RGBA
- Scaling: one fixed nearest-neighbour scale per sequence; frames are never normalized independently

## Mount clips

| Clip | Frames | FPS | Loop |
| --- | ---: | ---: | --- |
| AirIdle | 6 | 8 | Yes |
| HorizontalFlight | 6 | 10 | Yes |
| Dive | 6 | 10 | No |
| Hit | 4 | 12 | No |
| FatalImbalance | 6 | 10 | No |
| UncontrolledFall | 6 | 10 | Yes |
| ImpactShatter | 8 | 12 | No |

The mount controller has no automatic state transitions. Boss logic may play states by name without this art package deciding combat sequencing.

## Mirror Bird animation construction standard

- Assemble every pose from five authored parts: far wing, near wing, one complete body, upper tail, and lower tail.
- Keep the connected pelvis-chest-neck-head-beak body core fixed unless a later action explicitly authors motion for the complete chain.
- Render the far wing behind the body and the near wing in front of the body.
- Use only small per-frame feather clusters to close the shoulder seam; do not add animated fur or spikes around the torso.
- Animate the two tails independently with subtle delayed motion while preserving the shared tail root.
- Keep the `192 x 128` canvas, PPU `32`, integer placement, fixed slicing, and Point filtering for every frame.

## Layered prefab

`MirrorBirdMountVisual.prefab` contains separate `MountVisual` and `RiderVisual` SpriteRenderer/Animator layers. `RiderVisual` reuses the existing pixel bow Aim, Draw, and Fire sprites at PPU 32. Empty `RiderSeatAnchor`, `VisualRootAnchor`, and `GroundContactAnchor` transforms expose stable integration points.

`MirrorArcherMountedBoss.prefab` is the directly editable, unpacked mounted-boss visual assembly requested for integration. It intentionally contains the same visual layers and anchors without health, physics, collision, behaviour-tree, or final combat components.

## Mounted attack telegraphs

The runtime mounted boss now adds shape-coded bow charge and landing warnings without changing damage or attack timing:

- Locked shot: yellow diamond, single red trajectory, single red impact ring.
- Fan shot: orange fan rays with multiple red impact rings.
- Ground arrow rain: magenta down-arrow glyph with a row of red ground markers.
- Mounted dive: red downward glyph, curved dive path, and impact-radius ring.
- Air reposition: cyan movement arrow and destination ring; it does not use a red damage marker.

Direct and fan arrows raycast against the `Ground` layer to preview their likely impact point. Arrow-rain markers use the same count and horizontal range as the runtime spawn calculation.

## Rebuild and previews

Run **MirrorTrial > Boss > Build Mirror Bird Visual Assets** to reapply deterministic slicing and rebuild clips, controllers, and the visual prefab. Preview GIFs and scale/action comparison PNGs are in `Previews/`; the original ImageGen chroma and alpha boards remain in `Source/`.
