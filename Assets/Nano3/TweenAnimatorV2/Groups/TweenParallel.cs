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

        public override void Init()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].Init();
            }
        }

        public override void Play(Action onComplete = null)
        {
            if (_state == TweenState.Play) { return; }

            _state = TweenState.Play;

            if (_nodes.Count == 0)
            {
                Complete(onComplete);
                return;
            }

            _remaining = _nodes.Count;
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].Play(() => OnChildComplete(onComplete));
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
                _nodes[i].SetFinishState();
            }

            _state = TweenState.Complete;
            onComplete?.Invoke();
        }

        public override void Stop()
        {
            _state = TweenState.Stop;
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].Stop();
            }
        }

        public override void Reset()
        {
            _state = TweenState.Stop;
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].Reset();
            }
        }
    }
}
