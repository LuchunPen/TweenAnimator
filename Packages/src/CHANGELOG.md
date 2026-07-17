# Changelog

All notable changes to this package are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-07-18

### Added
- **Named clips.** A `TweenPlayer` now holds a list of named `TweenClip`s and plays
  them by name: `Play("Show", onComplete, onStepComplete)`. Each clip is an
  independent tree with its own Play On Start / Unscaled Time / Loop options, and
  more than one clip can run at once.
- `onStepComplete` callback, fired at the end of every pass (including each loop cycle).
- **Instant nodes** — a leaf flag that applies the end value immediately (zero
  duration), for setting up initial state.
- **Cut / copy / paste** of node branches (`Ctrl+X` / `Ctrl+C` / `Ctrl+V` and the
  context menu), and **Copy Clip / Paste Clip** for a whole clip from the clip bar.
- Editor **Play / Pause / Stop** preview with a live timeline bar and per-row play
  highlight (green = playing, yellow = waiting out a delay).

### Changed
- Callbacks are passed as parameters to `Play` (atomic), not attached afterwards.
- Re-`Play` on an already-playing clip is ignored (call `Stop`/`ResetAnimation` to
  restart); the tree is auto-reset at the start of each play.
- New clips start with a `Parallel` root named "Root" (protected from deletion).
- Internal cleanup: shared `TweenGroup` base for Sequence/Parallel, duration read
  from the live node, runtime-only `_state`, isolated `TweenTreeClipboard`.

### Fixed
- Loop-recursion crash guard for zero-duration clips.
- `OnDestroy` now stops tweens so a destroyed player can't write to destroyed targets.
- `MoveToTarget` preserves the target's Z.
- Missing-target icons and durations no longer flicker after structural edits or undo
  (the SerializedObject is rebuilt); null children (from deleted node scripts) are
  handled safely across all traversals.
- Removed the glitchy in-place "Change Type" that could drop children.

## [2.0.0] - 2026

Initial UPM release of Tween Animator V2: a single `TweenPlayer` component holds a
whole nested tween tree (Sequence / Parallel groups + leaf animations) via
`[SerializeReference]`, edited through the visual **Tween Tree** editor window
(outliner with add/remove, drag-and-drop reorder & reparent, rename, duplicate, and
a live per-node duration readout). Requires DOTween in the project.
