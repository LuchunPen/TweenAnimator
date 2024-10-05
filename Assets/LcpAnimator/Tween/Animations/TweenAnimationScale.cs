using System;
using UnityEngine;

namespace LcpGames 
{
    public class TweenAnimationScale : TweenAnimation
    {
        [Space(10)]
        [SerializeField] private Transform _trans;
        [SerializeField] private float _startValue;
        [SerializeField] private float _endValue;
        protected override void UpdateAnimator()
        {
            float scale =  _startValue + (_tweenerValue * (_endValue - _startValue));
            _trans.localScale = new Vector3(scale, scale, scale);
        }
    }
}
