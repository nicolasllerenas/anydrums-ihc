using System.Collections;
using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Mantiene el menú a una distancia de lectura cómoda (1.5 m) frente al usuario y con el panel a la altura de sus ojos.
    /// Se ubica al iniciar, al ponerse el visor y al recentrar la vista. Si el panel queda fuera de la vista por más de
    /// un segundo (por ejemplo, porque la app arrancó con el visor apoyado en la mesa) vuelve suavemente al frente.
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

        [Header("Volver al frente")]
        [Tooltip("Ángulo horizontal a partir del cual se considera que el panel quedó fuera de la vista.")]
        [SerializeField] float _maxYawAngle = 55f;
        [Tooltip("Diferencia de altura (m) entre los ojos y el panel a partir de la cual se reubica.")]
        [SerializeField] float _maxHeightDifference = 0.45f;
        [SerializeField] float _outOfViewDelay = 1f;
        [SerializeField] float _moveDuration = 0.45f;

        float _floorY;
        float _outOfViewTime;
        bool _active;
        Coroutine _move;

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
            _active = true;

            if (OVRManager.display != null) OVRManager.display.RecenteredPose += Place;
            OVRManager.HMDMounted += Place;
        }

        void OnDestroy()
        {
            if (OVRManager.display != null) OVRManager.display.RecenteredPose -= Place;
            OVRManager.HMDMounted -= Place;
        }

        void Update()
        {
            if (!_active || _move != null) return;
            var cam = Camera.main;
            if (cam == null || _panel == null) return;

            Transform head = cam.transform;
            Vector3 toPanel = Vector3.ProjectOnPlane(_panel.position - head.position, Vector3.up);
            Vector3 forward = FlatForward(head);
            bool outOfView = Vector3.Angle(forward, toPanel) > _maxYawAngle
                             || Mathf.Abs(_panel.position.y - (head.position.y + _heightOffset)) > _maxHeightDifference;

            _outOfViewTime = outOfView ? _outOfViewTime + Time.unscaledDeltaTime : 0f;
            if (_outOfViewTime > _outOfViewDelay)
            {
                _outOfViewTime = 0f;
                _move = StartCoroutine(MoveTo(TargetPose(head)));
            }
        }

        static bool HeadIsTracked()
        {
            var cam = Camera.main;
            return cam != null && cam.transform.localPosition.sqrMagnitude > 0.01f;
        }

        static Vector3 FlatForward(Transform head)
        {
            // Si se mira casi recto hacia abajo o arriba, la dirección "arriba" de la cabeza indica hacia dónde se mira.
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.04f) forward = Vector3.ProjectOnPlane(head.forward.y < 0f ? head.up : -head.up, Vector3.up);
            return forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
        }

        (Vector3 rootPosition, Quaternion rootRotation, float panelHeight) TargetPose(Transform head)
        {
            Vector3 forward = FlatForward(head);
            Vector3 position = head.position + forward * _distance;
            position.y = _floorY;
            return (position, Quaternion.LookRotation(forward, Vector3.up), head.position.y - _floorY + _heightOffset);
        }

        public void Place()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (_move != null)
            {
                StopCoroutine(_move);
                _move = null;
            }
            var (position, rotation, height) = TargetPose(cam.transform);
            transform.SetPositionAndRotation(position, rotation);
            SetPanelHeight(height);
        }

        IEnumerator MoveTo((Vector3 rootPosition, Quaternion rootRotation, float panelHeight) target)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float startHeight = _panel != null ? _panel.localPosition.y : target.panelHeight;
            float t = 0f;
            while (t < _moveDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / _moveDuration);
                transform.SetPositionAndRotation(Vector3.Lerp(startPosition, target.rootPosition, k),
                    Quaternion.Slerp(startRotation, target.rootRotation, k));
                SetPanelHeight(Mathf.Lerp(startHeight, target.panelHeight, k));
                yield return null;
            }
            _move = null;
        }

        void SetPanelHeight(float height)
        {
            if (_panel == null) return;
            Vector3 local = _panel.localPosition;
            local.y = height;
            _panel.localPosition = local;
        }
    }
}
