using System;
using UnityEngine;
using DG.Tweening;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Abstract leaf node. Drives a single 0..1 DOTween tweener and pushes the eased value
    /// into <see cref="Apply"/>. Concrete tweens (scale, move, alpha, ...) only implement Apply.
    /// </summary>
    [Serializable]
    public abstract class TweenAnimation : TweenNode
    {
        [SerializeField] private TweenData _tween = new TweenData();

        /// <summary>Eased progress in [0..1], written by the tweener each frame.</summary>
        protected float _value;

        private Tweener _tw;

        public override void Play(Action onComplete = null)
        {
            if (_state == TweenState.Play) { return; }

            OnStarted();

            _tw = DOTween.To(() => _value, x => _value = x, 1f, _tween.Duration)
                .SetEase(_tween.Ease, _tween.Amplitude, _tween.Period)
                .SetDelay(_tween.Delay)
                .OnUpdate(Apply)
                .OnComplete(() => Complete(onComplete));

            _state = TweenState.Play;
        }

        /// <summary>Apply the current <see cref="_value"/> to the target. Called every frame.</summary>
        protected abstract void Apply();

        /// <summary>Optional hook fired once when playback starts.</summary>
        protected virtual void OnStarted() { }

        /// <summary>Optional hook fired once when playback finishes (value has reached 1).</summary>
        protected virtual void OnCompleted() { }

        /// <summary>Optional hook fired when the node is reset.</summary>
        protected virtual void OnReset() { }

        private void Complete(Action onComplete)
        {
            KillTween();
            _value = 1f;
            _state = TweenState.Complete;

            OnCompleted();
            onComplete?.Invoke();
        }

        public override void Stop()
        {
            KillTween();
            _value = 0f;
            _state = TweenState.Stop;
        }

        public override void Reset()
        {
            KillTween();
            _value = 0f;
            _state = TweenState.Stop;

            OnReset();
            Apply();
        }

        public override void SetFinishState(Action onComplete = null)
        {
            KillTween();
            _value = 1f;
            _state = TweenState.Complete;

            Apply();
            OnCompleted();
            onComplete?.Invoke();
        }

        private void KillTween()
        {
            if (_tw != null && _tw.IsActive()) { _tw.Kill(); }
            _tw = null;
        }
    }
}
