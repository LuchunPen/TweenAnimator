# Tween Animator

A DOTween-based animation tool for Unity, designed mainly for UI. Build complex,
nested animations from a single component and a visual tree editor — no more piling
up one MonoBehaviour per animation. One `TweenPlayer` holds several **named clips**
and plays them by name.

> Requires [DOTween](http://dotween.demigiant.com/) in the project.
> Tested on Unity 2022.3 (LTS).

![Tween Tree editor](Documentation~/images/tree-editor-overview.png)

![Sample animation](Documentation~/images/anim-example.gif)

## Features

- **One component, many animations.** A single `TweenPlayer` holds a list of named
  `TweenClip`s; each clip is an independent tween tree stored via `[SerializeReference]`.
  Play them by name (`player.Play("Show")`), and run more than one at a time.
- **Sequence & Parallel groups** in any nesting combination.
- **Built-in leaf animations:** Scale, Move To Position, Move To Target, Canvas
  Group Alpha, Size, Set Bool, Trigger.
- **Instant nodes** — flip a leaf to *Instant* to apply its end value immediately
  (zero duration), for setting up an initial state.
- **Tween Tree editor window:** outliner with add/remove, **drag-and-drop reorder
  and reparent**, **rename**, **duplicate**, **cut / copy / paste** of branches, and
  a live **duration** readout per node. Copy/paste a **whole clip** from the clip bar.
- **Play from the editor:** Play / Pause / Stop with a live timeline bar and per-row
  highlight (green = playing, yellow = waiting out a delay).
- **Per-clip options:** Play On Start, Unscaled Time, Loop.
- **Looping** (Restart, N times or infinite) — e.g. a blinking button.
- **Unscaled time** — keep playing while `Time.timeScale = 0` (gameplay pause).
- **Extensible:** add a new animation by subclassing `TweenAnimation`; it appears
  in the editor's type picker automatically.

## Install via UPM (Package Manager)

1. Open **Window → Package Manager**.
2. Click **+ → Add package from git URL…**
3. Enter:

   ```
   https://github.com/LuchunPen/NanoTweenAnimator.git?path=Packages/src
   ```

DOTween must be present in the project separately (Asset Store or DOTween's own
package), as it is a required dependency and is not bundled.

## Usage — from the Editor

1. Add a **Tween Player** component to a GameObject.
2. Click **Open Tween Tree Editor** (or **Window → Nano3 → Tween Tree Editor**).
3. In the clip bar, click **+ Clip** and give the clip a name (e.g. `Show`). A new
   clip starts with a `Parallel` **Root** node — a good place to set initial states.
4. Add child groups and leaf animations under the root, then assign the scene targets
   (Transform / RectTransform / CanvasGroup) in the parameters panel on the right.
5. Per-clip options: **Play On Start**, **Unscaled Time**, **Loop**.

Clip bar: **+ Clip / - Clip** add or remove a clip, **Copy Clip / Paste Clip**
duplicate a whole animation (with its options and tree) into a new, uniquely named
clip. **Play / Pause / Stop** preview the selected clip live.

Editor shortcuts:

- **Double-click** a node — rename it.
- **Right-click** a node — Add Child / Duplicate / Copy / Cut / Paste / Delete.
- **Ctrl+C / Ctrl+X / Ctrl+V** — copy / cut / paste the selected branch;
  **Delete** removes it. (The root is protected.)
- **Drag** a node — reorder within a group, move between groups, or drop onto a
  group (middle of the row) to nest it as a child.
- The right column shows each node's **duration** for one pass (leaf = Delay +
  Duration, Sequence = sum, Parallel = longest child, root = total).

## Usage — from code

Drive clips through the `TweenPlayer` component, by name:

- `Play(string name, Action onComplete = null, Action onStepComplete = null)` —
  play the named clip from the start. `onComplete` fires once when the clip fully
  finishes (never on an infinite loop); `onStepComplete` fires at the end of every
  pass, including each loop cycle.
- `Stop(string name)` / `Stop()` — kill one clip, or every clip on the player.
- `ResetAnimation(string name)` — return the clip to its start state.
- `Pause(string name)` / `Resume(string name)` (and `IsPaused(string name)`) —
  pause keeping progress.
- `IsPlaying(string name)` — whether the clip is currently running.
- `SetFinishState(string name, Action onComplete = null)` — jump to the finished state.
- `GetClip(string name)` / `Clips` — access the clip objects directly.

The sample scene uses this controller (see `Assets/Nano3/Example`), binding one clip
to the keyboard — **Q** play, **E** stop, **Space** pause/resume:

```csharp
using UnityEngine;
using Nano3.TweenAnimator;

public class TweenPlayerExample : MonoBehaviour
{
    [SerializeField] private TweenPlayer _player;
    [SerializeField] private string _clipName = "Open";

    private void Update()
    {
        if (_player == null) { return; }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            _player.ResetAnimation(_clipName);
            _player.Play(_clipName, OnComplete, OnStepComplete);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            _player.Stop(_clipName);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_player.IsPaused(_clipName)) { _player.Resume(_clipName); }
            else { _player.Pause(_clipName); }
        }
    }

    private void OnComplete()
    {
        Debug.Log($"Clip '{_clipName}' complete");
    }

    private void OnStepComplete()
    {
        Debug.Log($"Clip '{_clipName}' step complete (each pass / loop cycle)");
    }
}
```

### Behaviour notes

- **Re-`Play` while playing is ignored.** If a clip is already running, `Play`
  returns immediately and the passed callbacks are discarded. Call `Stop` or
  `ResetAnimation` first (as the sample does) to restart from the beginning.
- **`Set Bool` reapplies on every play.** `Play` resets the tree first, so a
  `Set Bool` leaf fires its *reset* (inverse) value at the start of each play, then
  its set value on completion.
- **`DOTween.KillAll()` is external.** Tweens are global DOTween tweens; if other
  code calls `DOTween.KillAll()`, a clip's tweens die without reporting completion,
  so its `IsPlaying` state can stay stuck. Prefer `player.Stop()` to end animations.

## Add a custom animation

Subclass `TweenAnimation` and implement `Apply()`. The `_value` field is the eased
progress in `[0..1]`, driven by the tween each frame. New types show up in the
editor's type picker automatically.

```csharp
using System;
using UnityEngine;
using Nano3.TweenAnimator;

[Serializable]
public class TweenScale : TweenAnimation
{
    [SerializeField] private Transform _trans;
    [SerializeField] private float _startValue = 0f;
    [SerializeField] private float _endValue = 1f;

    protected override void Apply()
    {
        float scale = _startValue + (_value * (_endValue - _startValue));
        _trans.localScale = new Vector3(scale, scale, scale);
    }
}
```

Optional hooks: `OnStarted()`, `OnCompleted()`, `OnReset()`, and `Init()` (for
caching initial state, e.g. the current size). Give a type a friendlier menu entry
with `[TweenNodeMenu("Category/Nice Name")]`.

## License

MIT
