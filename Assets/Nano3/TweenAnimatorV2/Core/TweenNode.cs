using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Base unit of the tween tree. A node is plain serializable data (NOT a MonoBehaviour),
    /// so a whole animation tree lives inside a single component via [SerializeReference].
    /// A node is either a leaf <see cref="TweenAnimation"/> or a group (Sequence/Parallel).
    /// </summary>
    [Serializable]
    public abstract class TweenNode
    {
        [SerializeField] protected TweenState _state;
        public TweenState State { get { return _state; } }

        [Tooltip("Optional custom name shown in the tree editor. Falls back to the type name when empty.")]
        [SerializeField] private string _name;
        public string Name { get { return _name; } set { _name = value; } }

        /// <summary>
        /// When true the node's tween ignores Time.timeScale (plays during a timeScale=0 pause).
        /// Pushed down from the player; NOT serialized (runtime-only). Groups forward it to children.
        /// </summary>
        protected bool _useUnscaledTime;

        /// <summary>
        /// Propagate the unscaled-time mode into this node (and, for groups, all descendants).
        /// </summary>
        public virtual void SetUnscaledTime(bool value)
        {
            _useUnscaledTime = value;
        }

        /// <summary>
        /// Pause or resume this node (and, for groups, all descendants). Only nodes with an
        /// active tween are affected; a paused animation keeps its progress.
        /// </summary>
        public virtual void SetPaused(bool paused) { }

        /// <summary>
        /// Called once by the player before the first playback (equivalent of MonoBehaviour.Start).
        /// Groups forward this to their children. Override to cache initial values.
        /// </summary>
        public virtual void Init() { }

        /// <summary>Start playing; <paramref name="onComplete"/> fires when this node finishes.</summary>
        public abstract void Play(Action onComplete = null);

        /// <summary>Kill the tween and return to the "not played" state (value reset).</summary>
        public abstract void Stop();

        /// <summary>Stop and re-apply the initial state (value = 0).</summary>
        public abstract void Reset();

        /// <summary>Instantly jump to the finished state (value = 1) and report completion.</summary>
        public abstract void SetFinishState(Action onComplete = null);
    }
}
