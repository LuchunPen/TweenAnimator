using System;
using UnityEngine;
using DG.Tweening;

namespace LcpGames
{
    [Serializable]
    public class TweenData
    {
        [SerializeField] public float _delay;
        [SerializeField] public float _duration;
        [SerializeField] public Ease _tweenEase;
        [SerializeField] public float _amplitude = 1.7f;
        [SerializeField] public float _period = 0;

        public float Delay { get { return _delay; } }
        public float Duration { get { return _duration; } }
        public Ease TweenEase { get { return _tweenEase; } }
        public float Amplitude { get { return _amplitude; } }
        public float Period { get { return _period; } }
    }
}
