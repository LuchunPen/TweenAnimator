using System;
using System.Collections;
using UnityEngine;

namespace Nano3
{
    public class AnimationGroupParallel : AnimationBase
    {
        [SerializeField] private AnimationBase[] _animations;

        private Coroutine _animRoutine;

        public override void PlayAnimation(Action onComplete = null)
        {
            if (_state == AnimationState.Play) { return; }

            _state = AnimationState.Play;
            _animRoutine = StartCoroutine(PlayGroupAnimations(onComplete));
        }

        private IEnumerator PlayGroupAnimations(Action onComplete)
        {
            for (int i = 0; i < _animations.Length; i++)
            {
                _animations[i].PlayAnimation();
            }

            while(!AllAnimationsComplete())
            {
                yield return null;
            }

            CompleteInvokation(onComplete);
        }

        private bool AllAnimationsComplete()
        {
            bool complete = true;
            for (int i = 0; i < _animations.Length; i++)
            {
                if (_animations[i].State != AnimationState.Complete)
                {
                    complete = false;
                    break;
                }
            }
            return complete;
        }

        protected void CompleteInvokation(Action onComplete)
        {
            _state = AnimationState.Complete;
            _animRoutine = null;

            onComplete?.Invoke();
        }

        public override void StopAnimation()
        {
            _state = AnimationState.Stop;

            if (_animRoutine != null) 
            { 
                StopCoroutine(_animRoutine); 
                _animRoutine = null;
            }

            for (int i = 0; i < _animations.Length; i++)
            {
                _animations[i].StopAnimation();
            }
        }

        public override void ResetAnimation()
        {
            _state = AnimationState.Stop;

            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }

            for (int i = 0; i < _animations.Length; i++)
            {
                _animations[i].ResetAnimation();
            }
        }
    }
}
