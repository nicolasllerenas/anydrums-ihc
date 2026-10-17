using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace AnyDrums.Menu
{
    /// <summary>Avisa cuando el usuario suelta el gatillo sobre un control (por ejemplo, al terminar de arrastrar el slider).</summary>
    public class PointerUpNotifier : MonoBehaviour, IPointerUpHandler
    {
        public UnityEvent Released = new UnityEvent();

        public void OnPointerUp(PointerEventData eventData) => Released.Invoke();
    }
}
