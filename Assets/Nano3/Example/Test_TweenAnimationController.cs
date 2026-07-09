using UnityEngine;
using Nano3;
using Nano3.TweenAnimator;
public class Test_TweenAnimationController : MonoBehaviour
{
    [SerializeField] private AnimationBase _anim;
    [SerializeField] private TweenPlayer _anim2;

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

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            Time.timeScale = 0;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Time.timeScale = 1;
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            _anim2.ResetAnimation();
            _anim2.Play();
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            _anim2.Stop();
        }
    }

    private void OnComplete()
    {
        UnityEngine.Debug.Log("Complete animation");
    }
}
