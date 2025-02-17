using System;
using UnityEngine;
using UnityEngine.Events;

namespace Nano3
{
    public class TweenAnimationSetBool : TweenAnimation
    {
        [SerializeField] private bool _value;
        public UnityEvent<bool> OnSetBoolean;

        private bool _isActivated;

        protected override void UpdateAnimator()
        {
            if (_tweenerValue == 1 && !_isActivated)
            {
                OnSetBoolean?.Invoke(_value);
            }
        }

        public override void ResetAnimation()
        {
            _isActivated = false;
            OnSetBoolean?.Invoke(!_value);

            base.ResetAnimation();
        }
    }
}