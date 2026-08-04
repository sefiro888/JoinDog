using UnityEngine;

namespace JoinDog.App
{
    public sealed class BootController : MonoBehaviour
    {
        private void Start()
        {
            AppServices.Instance.GoToMainMenu();
        }
    }
}
