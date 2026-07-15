using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>Resizes a RectTransform from its current size to a target size.</summary>
    [Serializable]
    public class TweenSize : TweenAnimation
    {
        [SerializeField] private RectTransform _trans;
        [SerializeField] private Vector2 _targetSize;

        private Vector2 _startSize;

        public override void Init()
        {
            if (_trans != null) { _startSize = _trans.rect.size; }
        }

        protected override void Apply()
        {
            Vector2 size = Vector2.Lerp(_startSize, _targetSize, _value);
            _trans.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _trans.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }
    }
}
