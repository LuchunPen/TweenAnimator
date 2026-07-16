using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Plays all child nodes at once and completes when the last one finishes.
    /// Orchestrated via a completion counter (no coroutines).
    /// </summary>
    [Serializable]
    public class TweenParallel : TweenNode
    {
        [SerializeReference] protected List<TweenNode> _nodes = new List<TweenNode>();

        private int _remaining;

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

        public override float GetDuration()
        {
            float max = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) { max = Mathf.Max(max, _nodes[i].GetDuration()); }
            }
            return max;
        }

        public override void Init()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i]?.Init();
            }
        }

        public override void Play(Action onComplete = null)
        {
            if (_state == TweenState.Play) { return; }

            _state = TweenState.Play;

            int alive = 0;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) { alive++; }
            }

            if (alive == 0)
            {
                Complete(onComplete);
                return;
            }

            _remaining = alive;
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i]?.Play(() => OnChildComplete(onComplete));
            }
        }

        private void OnChildComplete(Action onComplete)
        {
            // Aborted by Stop/Reset while children were running.
            if (_state != TweenState.Play) { return; }

            _remaining--;
            if (_remaining <= 0)
            {
                Complete(onComplete);
            }
        }

        private void Complete(Action onComplete)
        {
            _state = TweenState.Complete;
            onComplete?.Invoke();
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
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i]?.Stop();
            }
        }

        public override void Reset()
        {
            _state = TweenState.Stop;
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i]?.Reset();
            }
        }
    }
}
