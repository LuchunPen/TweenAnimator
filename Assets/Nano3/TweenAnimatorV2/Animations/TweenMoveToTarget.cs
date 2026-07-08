using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>Moves a RectTransform in world space between two target transforms.</summary>
    [Serializable]
    public class TweenMoveToTarget : TweenAnimation
    {
        [SerializeField] private RectTransform _trans;
        [SerializeField] private RectTransform _moveFrom;
        [SerializeField] private RectTransform _moveTo;

        protected override void Apply()
        {
            Vector2 pos = Vector2.Lerp(_moveFrom.position, _moveTo.position, _value);
            _trans.position = pos;
        }
    }
}
