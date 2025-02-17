using System;
using UnityEngine;

namespace Nano3
{
    public class TweenAnimationMoveToPosition : TweenAnimation
    {
        [SerializeField] private RectTransform _trans;
        [SerializeField] private Vector2 _startPosition;
        [SerializeField] private Vector2 _endPosition;
        protected override void UpdateAnimator()
        {
            Vector2 pos = Vector2.Lerp(_startPosition, _endPosition, _tweenerValue);
            _trans.anchoredPosition = pos;
        }
    }
}
