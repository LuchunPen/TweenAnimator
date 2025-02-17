using System;
using UnityEngine;

namespace Nano3
{
    public class TweenAnimationCanvasGroupAlpha : TweenAnimation
    {
        [Space(10)]
        [SerializeField] private CanvasGroup _cg;
        [SerializeField][Range(0, 1)] private float _startValue;
        [SerializeField][Range(0, 1)] private float _endValue;

        protected override void UpdateAnimator()
        {
            float value = _startValue + (_tweenerValue * (_endValue - _startValue));
            _cg.alpha = value;
        }
    }
}