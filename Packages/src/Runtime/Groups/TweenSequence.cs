using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Plays child nodes one after another. Each child starts only after the previous
    /// one completes. Orchestrated via completion callbacks (no coroutines).
    /// </summary>
    [Serializable]
    public class TweenSequence : TweenNode
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

        public override float GetDuration()
        {
            float total = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) { total += _nodes[i].GetDuration(); }
            }
            return total;
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
            PlayFrom(0, onComplete);
        }

        private void PlayFrom(int index, Action onComplete)
        {
            // Aborted by Stop/Reset while a child was running.
            if (_state != TweenState.Play) { return; }

            if (index >= _nodes.Count)
            {
                Complete(onComplete);
                return;
            }

            if (_nodes[index] == null)
            {
                PlayFrom(index + 1, onComplete);
                return;
            }

            _nodes[index].Play(() => PlayFrom(index + 1, onComplete));
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
    }
}
