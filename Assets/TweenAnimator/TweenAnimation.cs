using System;
using UnityEngine;
using DG.Tweening;

namespace Nano3
{
    public abstract class TweenAnimation : AnimationBase
    {
        [SerializeField] private TweenData _tween;

        protected float _tweenerValue = 0;
        private Tweener _tw;

        public override void PlayAnimation(Action onComplete = null)
        {
            if (_state == AnimationState.Play) { return; }

            _tw = DOTween.To(() => _tweenerValue, x => _tweenerValue = x, 1, _tween.Duration)
                .SetEase(_tween.TweenEase, _tween.Amplitude, _tween.Period)
                .SetDelay(_tween.Delay)
                .OnUpdate(UpdateAnimator)
                .OnComplete(()=> { CompleteInvocation(onComplete); });

            _state = AnimationState.Play;
        }

        protected abstract void UpdateAnimator();

        protected void CompleteInvocation(Action onComplete)
        {
            _state = AnimationState.Complete;

            if (_tw != null) { _tw.Kill(); }

            onComplete?.Invoke();
        }

        public override void StopAnimation()
        {
            if (_tw != null) { _tw.Kill(); }
            _tweenerValue = 0;

            _state = AnimationState.Stop;
        }


        public override void ResetAnimation()
        {
            StopAnimation();
            UpdateAnimator();
        }
    }
}
