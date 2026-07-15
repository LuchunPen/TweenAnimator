using System;
using UnityEngine.Events;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Fires a UnityEvent once when the tween completes.
    /// Has no per-frame visual, so <see cref="Apply"/> is a no-op.
    /// </summary>
    [Serializable]
    public class TweenTrigger : TweenAnimation
    {
        public UnityEvent OnActivate;

        protected override void Apply() { }

        protected override void OnCompleted()
        {
            OnActivate?.Invoke();
        }
    }
}
