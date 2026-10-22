using System;
using System.Collections;
using System.Collections.Generic;
using AnyDrums.Menu;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace AnyDrums.Studio
{
    /// <summary>
    /// Panel de sesión del estudio (RF1 y RF4 dentro de la escena 2). Se abre y se cierra con el botón de menú
    /// del mando izquierdo y aparece a la izquierda del usuario para no tapar la batería. Tiene las mismas
    /// opciones del menú inicial (tipo de batería y volumen) y los botones "Volver al menú" y "Salir".
    /// Mientras está abierto los mandos muestran el rayo; <see cref="IsOpen"/> sirve para que los gatillos
    /// dejen de funcionar como pedales (Entregable 3).
    /// </summary>
    public class SessionPanelController : MonoBehaviour
    {
        /// <summary>True mientras el panel está abierto.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>Se dispara al abrir (true) o cerrar (false) el panel.</summary>
        public static event Action<bool> OpenChanged;

        [SerializeField] Transform _panel;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Button _closeButton;
        [SerializeField] Button _backToMenuButton;
        [SerializeField] Button _exitButton;
        [SerializeField] MenuAudio _audio;

        [Header("Ubicación respecto a la cabeza")]
        [SerializeField] float _distance = 0.9f;
        [Tooltip("Ángulo hacia la izquierda (negativo) para no tapar la batería.")]
        [SerializeField] float _yawOffset = -40f;
        [SerializeField] float _heightOffset = -0.15f;

        [Header("Rayos de los mandos")]
        [Tooltip("Los rayos solo se ven con el panel abierto, así no molestan al tocar.")]
        [SerializeField] bool _raysOnlyWhileOpen = true;

        [Header("Editor")]
        [Tooltip("Tecla para abrir el panel en el Editor sin visor.")]
        [SerializeField] KeyCode _editorKey = KeyCode.M;

        const float AnimationTime = 0.16f;

        readonly List<GameObject> _rays = new List<GameObject>();
        Coroutine _animation;
        float _panelScale;

        public Transform Panel => _panel;
        public Button BackToMenuButton => _backToMenuButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsOpen = false;
            OpenChanged = null;
        }

        void Awake()
        {
            _panelScale = _panel.localScale.x;
            _panel.gameObject.SetActive(false);
            _group.alpha = 0f;
        }

        void OnEnable()
        {
            _closeButton.onClick.AddListener(Close);
            _backToMenuButton.onClick.AddListener(BackToMenu);
            _exitButton.onClick.AddListener(QuitApplication);
        }

        void OnDisable()
        {
            _closeButton.onClick.RemoveListener(Close);
            _backToMenuButton.onClick.RemoveListener(BackToMenu);
            _exitButton.onClick.RemoveListener(QuitApplication);
        }

        IEnumerator Start()
        {
            // Un frame después, cuando el Interaction SDK ya inicializó los interactores del rig.
            yield return null;
            foreach (var ray in FindObjectsByType<RayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                _rays.Add(ray.gameObject);
            SetRays(IsOpen);
        }

        void OnDestroy()
        {
            if (IsOpen) SetOpen(false);
        }

        void Update()
        {
            if (SceneTransition.IsBusy) return;
            if (OVRInput.GetDown(OVRInput.Button.Start) || EditorKeyPressed()) Toggle();
        }

        bool EditorKeyPressed()
        {
#if UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(_editorKey);
#else
            return false;
#endif
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            PlaceNextToUser();
            _panel.gameObject.SetActive(true);
            _group.interactable = true;
            SetOpen(true);
            SetRays(true);
            if (_audio != null) _audio.PlayClick();
            Animate(1f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            _group.interactable = false;
            SetOpen(false);
            SetRays(false);
            Animate(0f);
        }

        void BackToMenu()
        {
            var catalog = DrumKitCatalog.Load();
            string menu = catalog != null ? catalog.MenuScene : "MainMenu";
            _group.interactable = false;
            if (!SceneTransition.LoadScene(menu)) _group.interactable = true;
        }

        void QuitApplication()
        {
            _group.interactable = false;
            SceneTransition.Quit();
        }

        void SetOpen(bool open)
        {
            IsOpen = open;
            OpenChanged?.Invoke(open);
        }

        void SetRays(bool visible)
        {
            if (!_raysOnlyWhileOpen) return;
            foreach (var ray in _rays)
                if (ray != null && ray.activeSelf != visible) ray.SetActive(visible);
        }

        void PlaceNextToUser()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Transform head = cam.transform;

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            forward.Normalize();

            Vector3 direction = Quaternion.Euler(0f, _yawOffset, 0f) * forward;
            Vector3 position = head.position + direction * _distance;
            position.y = head.position.y + _heightOffset;
            _panel.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
        }

        void Animate(float target)
        {
            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(AnimateRoutine(target));
        }

        IEnumerator AnimateRoutine(float target)
        {
            float start = _group.alpha;
            float t = 0f;
            while (t < AnimationTime)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / AnimationTime));
                _group.alpha = a;
                _panel.localScale = Vector3.one * _panelScale * Mathf.Lerp(0.92f, 1f, a);
                yield return null;
            }
            _group.alpha = target;
            _panel.localScale = Vector3.one * _panelScale * (target > 0f ? 1f : 0.92f);
            if (target <= 0f) _panel.gameObject.SetActive(false);
            _animation = null;
        }
    }
}
