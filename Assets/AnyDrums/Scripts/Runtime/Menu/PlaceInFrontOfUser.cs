using System.Collections;
using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Ubica el menú a una distancia de lectura cómoda (1.5 m) frente a la mirada del usuario
    /// y con el panel centrado a la altura de sus ojos. Se vuelve a ubicar si el usuario recentra la vista.
    /// El objeto queda apoyado en el piso; solo el panel sube a la altura de los ojos.
    /// </summary>
    public class PlaceInFrontOfUser : MonoBehaviour
    {
        [SerializeField] Transform _panel;
        [SerializeField] float _distance = 1.5f;
        [Tooltip("Desplazamiento vertical del centro del panel respecto a los ojos.")]
        [SerializeField] float _heightOffset = -0.05f;
        [Tooltip("Tiempo máximo que se espera a que el visor reporte la posición de la cabeza.")]
        [SerializeField] float _trackingTimeout = 1f;

        float _floorY;

        IEnumerator Start()
        {
            _floorY = transform.position.y;
            // Sin visor (por ejemplo en el Editor sin Link) se respeta la posición de la escena.
            if (!OVRManager.isHmdPresent) yield break;

            float waited = 0f;
            while (waited < _trackingTimeout && !HeadIsTracked())
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Place();

            if (OVRManager.display != null) OVRManager.display.RecenteredPose += Place;
        }

        void OnDestroy()
        {
            if (OVRManager.display != null) OVRManager.display.RecenteredPose -= Place;
        }

        static bool HeadIsTracked()
        {
            var cam = Camera.main;
            return cam != null && cam.transform.localPosition.sqrMagnitude > 0.01f;
        }

        public void Place()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Transform head = cam.transform;

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            forward.Normalize();

            Vector3 position = head.position + forward * _distance;
            position.y = _floorY;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));

            if (_panel != null)
            {
                Vector3 local = _panel.localPosition;
                local.y = head.position.y - _floorY + _heightOffset;
                _panel.localPosition = local;
            }
        }
    }
}
