using System;
using UnityEngine;

namespace LcpGames
{
    public abstract class AnimationBase : MonoBehaviour
    {
        public enum AnimationState
        {
            Stop,
            Play,
            Complete
        }

        [SerializeField] protected AnimationState _state;
        public AnimationState State { get { return _state; } }

        public abstract void PlayAnimation(Action onComplete = null);
        public abstract void StopAnimation();
        public abstract void ResetAnimation();
    }
}
