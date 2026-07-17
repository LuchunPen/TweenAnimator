using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Base for nodes that own a list of child nodes (Sequence/Parallel). Holds the child
    /// list and every traversal that is the same regardless of play strategy
    /// (unscaled-time/pause/init/stop/reset/finish). Subclasses implement only how children
    /// are played (<see cref="Play"/>) and how their durations combine (<see cref="GetDuration"/>).
    /// </summary>
    [Serializable]
    public abstract class TweenGroup : TweenNode
    {
        [SerializeReference] protected List<TweenNode> _nodes = new List<TweenNode>();

        // Null children can appear when a custom node's script is deleted (Unity deserialises
        // the unknown [SerializeReference] type as null) — every traversal skips them so one
        // broken entry doesn't take the whole clip down.

        public override void SetUnscaledTime(bool value)
        {
            base.SetUnscaledTime(value);
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i]?.SetUnscaledTime(value);
            }
        }

        public override void SetPaused(bool paused)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i]?.SetPaused(paused);
            }
        }

        public override void Init()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i]?.Init();
            }
        }

        public override void SetFinishState(Action onComplete = null)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i]?.SetFinishState();
            }

            _state = TweenState.Complete;
            onComplete?.Invoke();
        }

        public override void Stop()
        {
            _state = TweenState.Stop;
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                _nodes[i]?.Stop();
            }
        }

        public override void Reset()
        {
            _state = TweenState.Stop;
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                _nodes[i]?.Reset();
            }
        }

        /// <summary>Mark the group complete and report it. Shared by every play strategy.</summary>
        protected void Complete(Action onComplete)
        {
            _state = TweenState.Complete;
            onComplete?.Invoke();
        }
    }
}
