using System;
using UnityEngine.Events;

namespace Nano3
{
    public class TweenAnimationTrigger : TweenAnimation
    {
        private bool _activated = false;
        public UnityEvent OnActivate;

        protected override void UpdateAnimator()
        {
            if (_tweenerValue == 1 && !_activated)
            {
                _activated = true;
                OnActivate?.Invoke();
            }
        }

        public override void ResetAnimation()
        {
            _activated = false;
            base.ResetAnimation();
        }
    }
}
