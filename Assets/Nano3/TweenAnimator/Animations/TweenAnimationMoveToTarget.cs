using System;
using UnityEngine;

namespace Nano3
{
    public class TweenAnimationMoveToTarget : TweenAnimation
    {
        [SerializeField] private RectTransform _trans;
        [SerializeField] private RectTransform _moveFrom;
        [SerializeField] private RectTransform _moveTo;

        protected override void UpdateAnimator()
        {
            Vector2 newPos = Vector2.Lerp(_moveFrom.position, _moveTo.position, _tweenerValue);
            _trans.position = newPos;
        }
    }
}
