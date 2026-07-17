using System;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Plays child nodes one after another. Each child starts only after the previous
    /// one completes. Orchestrated via completion callbacks (no coroutines).
    /// </summary>
    [Serializable]
    public class TweenSequence : TweenGroup
    {
        public override float GetDuration()
        {
            float total = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) { total += _nodes[i].GetDuration(); }
            }
            return total;
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
    }
}
