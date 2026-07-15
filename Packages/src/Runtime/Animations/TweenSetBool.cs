using System;
using UnityEngine;
using UnityEngine.Events;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Fires a boolean UnityEvent when the tween completes; emits the inverse value on reset.
    /// Has no per-frame visual, so <see cref="Apply"/> is a no-op.
    /// </summary>
    [Serializable]
    public class TweenSetBool : TweenAnimation
    {
        [SerializeField] private bool _boolValue;
        public UnityEvent<bool> OnSetBoolean;

        protected override void Apply() { }

        protected override void OnCompleted()
        {
            OnSetBoolean?.Invoke(_boolValue);
        }

        protected override void OnReset()
        {
            OnSetBoolean?.Invoke(!_boolValue);
        }
    }
}
