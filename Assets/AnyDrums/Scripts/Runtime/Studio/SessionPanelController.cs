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
            PlaceFor(cam.transform.position, cam.transform.forward);
        }

        // Orden de preferencia: primero a la izquierda (para no tapar la batería), luego al frente o a la derecha.
        static readonly float[] HeadsetYaws = { 0f, -15f, 12f, -32f, 28f, 45f, -50f };
        static readonly float[] DesktopYaws = { -8f, -25f, 15f };

        /// <summary>
        /// Ubica el panel junto al usuario. Prueba varias posiciones y elige la primera que no choca con piezas de la
        /// batería ni con el cuarto y que se ve sin obstáculos desde los ojos; sentado en la banqueta, el hi-hat y el
        /// crash quedan a la izquierda y a menos de un metro, y podían tapar el panel.
        /// </summary>
        public void PlaceFor(Vector3 head, Vector3 lookDirection)
        {
            Vector3 forward = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();

            // En un monitor el campo de visión es más estrecho que en el visor: el panel va casi al frente.
            bool desktop = DesktopFallback.IsActive;
            float[] yaws = desktop ? DesktopYaws : HeadsetYaws;
            float[] distances = desktop ? new[] { 0.75f, 0.6f } : new[] { _distance, 0.72f, 0.58f };
            // Si alrededor está todo ocupado (sentado en la banqueta), se prueba por encima de los platos.
            float[] heights = desktop ? new[] { -0.06f } : new[] { _heightOffset, 0.05f, 0.2f, 0.32f };
            float baseYaw = desktop ? 0f : _yawOffset;

            Physics.SyncTransforms();
            var size = PanelSize();
            var obstacles = KitObstacles();
            int bestHits = int.MaxValue;
            Vector3 bestPosition = default;
            Quaternion bestRotation = Quaternion.identity;

            string chosen = "";
            foreach (float height in heights)
            foreach (float distance in distances)
            foreach (float yawStep in yaws)
            {
                Vector3 direction = Quaternion.Euler(0f, baseYaw + yawStep, 0f) * forward;
                Vector3 position = head + direction * distance;
                position.y = head.y + height;
                // El panel mira hacia los ojos (si queda más arriba, se inclina hacia abajo).
                var rotation = Quaternion.LookRotation(position - head, Vector3.up);
                int hits = CountObstacles(head, position, rotation, size, obstacles);
                if (hits < bestHits)
                {
                    bestHits = hits;
                    bestPosition = position;
                    bestRotation = rotation;
                    chosen = $"giro {baseYaw + yawStep:0}°, {distance:0.00} m, altura {height:+0.00;-0.00} m";
                }
                if (hits == 0) goto Found;
            }
            Found:
            _panel.SetPositionAndRotation(bestPosition, bestRotation);
            Debug.Log($"[AnyDrums] Panel de sesión: {chosen}, obstáculos {bestHits}");
        }

        Vector2 PanelSize()
        {
            var rect = ((RectTransform)_panel).rect;
            float scale = _panelScale > 0f ? _panelScale : _panel.localScale.x;
            return new Vector2(rect.width, rect.height) * scale;
        }

        // Cajas de las piezas del kit visible (sin las muy grandes, como el soporte completo, que lo bloquearían todo).
        static List<Bounds> KitObstacles()
        {
            var result = new List<Bounds>();
            var switcher = FindFirstObjectByType<StudioKitSwitcher>();
            var kit = switcher != null ? switcher.ActiveKit : null;
            if (kit == null) return result;
            foreach (var renderer in kit.GetComponentsInChildren<Renderer>())
                if (renderer.bounds.size.magnitude < 1.6f) result.Add(renderer.bounds);
            return result;
        }

        static int CountObstacles(Vector3 head, Vector3 center, Quaternion rotation, Vector2 size, List<Bounds> obstacles)
        {
            Vector3 right = rotation * Vector3.right * (size.x * 0.5f);
            Vector3 up = rotation * Vector3.up * (size.y * 0.5f);
            Vector3[] points =
            {
                center, center + right + up, center + right - up, center - right + up, center - right - up,
                center + right, center - right, center + up, center - up
            };
            var panelBox = new Bounds(center, Vector3.zero);
            foreach (var p in points) panelBox.Encapsulate(p);
            panelBox.Expand(0.04f);

            int hits = 0;
            foreach (var bounds in obstacles)
            {
                if (bounds.Intersects(panelBox))
                {
                    hits++;
                    continue;
                }
                foreach (var p in points)
                {
                    Vector3 toPoint = p - head;
                    if (bounds.IntersectRay(new Ray(head, toPoint), out float d) && d < toPoint.magnitude)
                    {
                        hits++;
                        break;
                    }
                }
            }

            // Paredes y muebles del cuarto (colisionadores), sin contar el propio rig del usuario.
            foreach (var p in points)
                if (Physics.Linecast(head, p, out var hit, ~0, QueryTriggerInteraction.Ignore) && !IsPlayer(hit.collider))
                    hits++;
            foreach (var collider in Physics.OverlapBox(center, new Vector3(size.x * 0.5f, size.y * 0.5f, 0.03f), rotation, ~0,
                         QueryTriggerInteraction.Ignore))
                if (!IsPlayer(collider)) hits++;
            return hits;
        }

        static bool IsPlayer(Collider collider) => collider.GetComponentInParent<OVRCameraRig>() != null;

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
