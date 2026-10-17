using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnyDrums
{
    /// <summary>
    /// Fundido a negro entre escenas (0.5 s por defecto) para que el cambio de entorno no sea brusco
    /// ni provoque mareo. Vive en DontDestroyOnLoad y dibuja un Canvas pegado a la cámara principal,
    /// así la escena de destino también aparece con fundido sin tener que configurarla.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        public const float DefaultFadeDuration = 0.5f;

        static readonly Color FadeColor = new Color(0.01f, 0.01f, 0.012f, 1f);
        static SceneTransition _instance;

        Camera _attachedCamera;
        Canvas _overlay;
        CanvasGroup _overlayGroup;
        float _alpha;
        bool _busy;

        public static bool IsBusy => _instance != null && _instance._busy;

        static SceneTransition Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SceneTransition]");
                    _instance = go.AddComponent<SceneTransition>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        /// <summary>Empieza en negro y aclara la vista. Útil al iniciar la aplicación.</summary>
        public static void FadeInFromBlack(float duration = DefaultFadeDuration)
        {
            var t = Instance;
            t.StopAllCoroutines();
            t.StartCoroutine(t.Fade(1f, 0f, duration));
        }

        /// <summary>
        /// Funde a negro, carga la escena y vuelve a aclarar. Devuelve false si la escena no está en Build Settings.
        /// </summary>
        public static bool LoadScene(string sceneName, float duration = DefaultFadeDuration)
        {
            if (IsBusy) return false;
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[AnyDrums] La escena '{sceneName}' no está en File > Build Settings.");
                return false;
            }
            var t = Instance;
            t.StopAllCoroutines();
            t.StartCoroutine(t.LoadRoutine(sceneName, duration));
            return true;
        }

        /// <summary>Funde a negro y cierra la aplicación (en el Editor detiene el Play Mode).</summary>
        public static void Quit(float duration = DefaultFadeDuration)
        {
            if (IsBusy) return;
            var t = Instance;
            t.StopAllCoroutines();
            t.StartCoroutine(t.QuitRoutine(duration));
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        // Se llama antes del primer frame de la escena nueva: la cubrimos de negro para que no se vea un salto.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_busy) SetAlpha(1f);
        }

        IEnumerator LoadRoutine(string sceneName, float duration)
        {
            _busy = true;
            yield return Fade(_alpha, 1f, duration);
            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;
            // Un frame extra para que el rig de la escena nueva ubique la cámara antes de aclarar.
            yield return null;
            yield return Fade(1f, 0f, duration);
            _busy = false;
        }

        IEnumerator QuitRoutine(float duration)
        {
            _busy = true;
            yield return Fade(_alpha, 1f, duration);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            SetAlpha(from);
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            SetAlpha(to);
        }

        void SetAlpha(float alpha)
        {
            _alpha = alpha;
            if (!EnsureOverlay()) return;
            _overlayGroup.alpha = alpha;
            _overlay.enabled = alpha > 0.001f;
        }

        bool EnsureOverlay()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            if (_overlay != null && _attachedCamera == cam) return true;

            if (_overlay != null) Destroy(_overlay.gameObject);
            _attachedCamera = cam;

            var go = new GameObject("FadeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.layer = cam.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(cam.transform, false);

            // Justo delante del plano cercano y lo bastante grande para cubrir todo el campo de visión del visor.
            float distance = cam.nearClipPlane + 0.02f;
            rt.localPosition = new Vector3(0f, 0f, distance);
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(100f, 100f);
            rt.localScale = Vector3.one * (distance * 10f / 100f);

            _overlay = go.GetComponent<Canvas>();
            _overlay.renderMode = RenderMode.WorldSpace;
            _overlay.sortingOrder = short.MaxValue;
            _overlayGroup = go.GetComponent<CanvasGroup>();
            _overlayGroup.interactable = false;
            _overlayGroup.blocksRaycasts = false;

            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            black.layer = go.layer;
            var blackRt = (RectTransform)black.transform;
            blackRt.SetParent(rt, false);
            blackRt.anchorMin = Vector2.zero;
            blackRt.anchorMax = Vector2.one;
            blackRt.offsetMin = Vector2.zero;
            blackRt.offsetMax = Vector2.zero;
            var image = black.GetComponent<Image>();
            image.color = FadeColor;
            image.raycastTarget = false;
            return true;
        }
    }
}
