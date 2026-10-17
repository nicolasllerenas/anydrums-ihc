using System.Collections;
using System.Linq;
using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// En el menú el joystick no tiene uso: apaga los interactores de teletransporte y giro del rig de Meta.
    /// Se hace después del primer frame para que el Interaction SDK los inicialice y luego los deshabilite limpiamente.
    /// </summary>
    public class DisableLocomotionInMenu : MonoBehaviour
    {
        [SerializeField] string[] _objectNames = { "LocomotionControllerInteractorGroup", "LocomotionHandInteractorGroup" };

        IEnumerator Start()
        {
            yield return null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (_objectNames.Contains(t.name)) t.gameObject.SetActive(false);
            }
        }
    }
}
