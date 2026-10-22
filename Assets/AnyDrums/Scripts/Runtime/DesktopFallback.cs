using System.Collections;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace AnyDrums
{
    /// <summary>
    /// Modo escritorio para probar sin visor (por ejemplo, en el Editor sin Quest Link). Si al iniciar no hay un
    /// visor activo: sube la cámara a la altura de los ojos, permite mirar alrededor manteniendo el clic derecho y
    /// usar los paneles con el mouse. Con el visor conectado no hace nada.
    /// </summary>
    public class DesktopFallback : MonoBehaviour
    {
        const float EyeHeight = 1.6f;
        const float LookSpeed = 3f;

        static DesktopFallback _instance;

        Transform _trackingSpace;
        float _yaw, _pitch;
        GUIStyle _style;

        public static bool IsActive => _instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var probe = new GameObject("[DesktopFallbackProbe]");
            DontDestroyOnLoad(probe);
            probe.AddComponent<Probe>();
        }

        // Espera un momento a que XR termine de iniciar antes de decidir si hay visor.
        class Probe : MonoBehaviour
        {
            IEnumerator Start()
            {
                yield return new WaitForSecondsRealtime(0.5f);
                if (!HeadsetActive())
                {
                    Debug.Log("[AnyDrums] No hay visor activo: modo escritorio (mouse para los paneles, clic derecho para mirar, M abre el panel de sesión).");
                    var go = new GameObject("[DesktopFallback]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<DesktopFallback>();
                }
                Destroy(gameObject);
            }
        }

        static bool HeadsetActive() => XRSettings.isDeviceActive || OVRManager.isHmdPresent;

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void Start() => Setup();

        // Se espera un frame: el PointableCanvasModule de la escena nueva (en modo exclusivo) desactiva en su Start
        // los demás módulos de entrada, incluido el del mouse si se agregara antes.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(SetupNextFrame());

        IEnumerator SetupNextFrame()
        {
            yield return null;
            Setup();
        }

        void Setup()
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            _trackingSpace = rig != null ? rig.trackingSpace != null ? rig.trackingSpace : rig.transform.Find("TrackingSpace") : null;
            if (_trackingSpace != null)
            {
                var position = _trackingSpace.localPosition;
                position.y = EyeHeight;
                _trackingSpace.localPosition = position;
                _trackingSpace.localRotation = Quaternion.identity;
            }
            _yaw = _pitch = 0f;

            // El mouse reemplaza al rayo del mando: módulo estándar de uGUI en lugar del de Interaction SDK.
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                if (eventSystem.TryGetComponent(out PointableCanvasModule pointable))
                {
                    pointable.ExclusiveMode = false;
                    pointable.enabled = false;
                }
                if (!eventSystem.TryGetComponent(out StandaloneInputModule standalone))
                    standalone = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                standalone.enabled = true;
            }
        }

        void Update()
        {
            if (_trackingSpace == null) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * LookSpeed;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * LookSpeed, -70f, 70f);
                _trackingSpace.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
#endif
        }

        void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(1f, 1f, 1f, 0.75f) } };
            GUI.Label(new Rect(12, Screen.height - 28, Screen.width - 24, 22),
                "Modo escritorio (sin visor): clic para seleccionar · clic derecho + mover para mirar · M abre el panel de sesión",
                _style);
        }
    }
}
