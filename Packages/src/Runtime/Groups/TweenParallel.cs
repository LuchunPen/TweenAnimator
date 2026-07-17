using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Plays all child nodes at once and completes when the last one finishes.
    /// Orchestrated via a completion counter (no coroutines).
    /// </summary>
    [Serializable]
    public class TweenParallel : TweenGroup
    {
        private int _remaining;

        public override float GetDuration()
        {
            float max = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) { max = Mathf.Max(max, _nodes[i].GetDuration()); }
            }
            return max;
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
    }
}
