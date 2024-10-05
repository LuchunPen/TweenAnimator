using LcpGames;
using UnityEngine;

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
    }

    private void OnComplete()
    {
        UnityEngine.Debug.Log("Complete animation");
    }
}
