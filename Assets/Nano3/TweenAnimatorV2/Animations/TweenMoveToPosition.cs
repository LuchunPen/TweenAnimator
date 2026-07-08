using System;
using UnityEngine;

namespace Nano3.TweenAnimator
{
    /// <summary>Moves a RectTransform between two anchored positions.</summary>
    [Serializable]
    public class TweenMoveToPosition : TweenAnimation
    {
        [SerializeField] private RectTransform _trans;
        [SerializeField] private Vector2 _startPosition;
        [SerializeField] private Vector2 _endPosition;

        protected override void Apply()
        {
            Vector2 pos = Vector2.Lerp(_startPosition, _endPosition, _value);
            _trans.anchoredPosition = pos;
        }
    }
}
