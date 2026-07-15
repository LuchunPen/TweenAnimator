using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>Fades a CanvasGroup between two alpha values.</summary>
    [Serializable]
    public class TweenAlpha : TweenAnimation
    {
        [SerializeField] private CanvasGroup _cg;
        [SerializeField][Range(0, 1)] private float _startValue = 0f;
        [SerializeField][Range(0, 1)] private float _endValue = 1f;

        protected override void Apply()
        {
            float alpha = _startValue + (_value * (_endValue - _startValue));
            _cg.alpha = alpha;
        }
    }
}
