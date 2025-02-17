using System;
using UnityEngine;

namespace Nano3
{
    public class TweenAnimationSize : TweenAnimation
    {
        [SerializeField] private RectTransform _trans;
        [SerializeField] private Vector2 _targetSize;

        private Vector2 _startSize;

        private void Start()
        {
            _startSize = _trans.rect.size;
        }

        protected override void UpdateAnimator()
        {
            Vector2 newSize = Vector2.Lerp(_startSize, _targetSize, _tweenerValue);
            _trans.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newSize.x);
            _trans.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newSize.y);
        }
    }
}