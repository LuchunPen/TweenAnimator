using UnityEngine;
using Nano3;

public class Test_TweenAnimationController : MonoBehaviour
{
    [SerializeField] private AnimationBase _anim;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            _anim.ResetAnimation();
            _anim.PlayAnimation(OnComplete);
        }

        if (Input.GetKey(KeyCode.E))
        {
            _anim.StopAnimation();
        }
    }

    private void OnComplete()
    {
        UnityEngine.Debug.Log("Complete animation");
    }
}
