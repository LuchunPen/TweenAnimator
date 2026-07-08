using System;
using UnityEngine;
using DG.Tweening;

namespace Nano3.TweenAnimator
{
    /// <summary>
    /// Timing/easing parameters shared by every leaf tween.
    /// </summary>
    [Serializable]
    public class TweenData
    {
        [SerializeField] private float _delay;
        [SerializeField] private float _duration = 1f;
        [SerializeField] private Ease _ease = Ease.Linear;
        [Tooltip("Overshoot for Back eases / strength for Elastic eases.")]
        [SerializeField] private float _amplitude = 1.7f;
        [Tooltip("Period for Elastic eases (0 = auto).")]
        [SerializeField] private float _period = 0f;

        public float Delay { get { return _delay; } }
        public float Duration { get { return _duration; } }
        public Ease Ease { get { return _ease; } }
        public float Amplitude { get { return _amplitude; } }
        public float Period { get { return _period; } }
    }
}
