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
