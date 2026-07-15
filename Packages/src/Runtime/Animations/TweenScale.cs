using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>Uniformly scales a Transform from a start to an end value.</summary>
    [Serializable]
    public class TweenScale : TweenAnimation
    {
        [SerializeField] private Transform _trans;
        [SerializeField] private float _startValue = 0f;
        [SerializeField] private float _endValue = 1f;

        protected override void Apply()
        {
            float scale = _startValue + (_value * (_endValue - _startValue));
            _trans.localScale = new Vector3(scale, scale, scale);
        }
    }
}
