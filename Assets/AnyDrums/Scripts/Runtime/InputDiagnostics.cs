using System.Linq;
using System.Text;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AnyDrums
{
    /// <summary>
    /// Registro de diagnóstico de la interacción (se lee en el visor con: adb logcat -s Unity | findstr AnyDrums).
    /// Escribe una línea solo cuando cambia algo: cuántos rayos hay activos, en qué estado están, qué apuntan,
    /// y si el sistema de eventos de la UI está listo. Sirve para saber por qué un panel no responde.
    /// </summary>
    public class InputDiagnostics : MonoBehaviour
    {
        const float Interval = 0.25f;

        string _last;
        float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("[InputDiagnostics]");
            DontDestroyOnLoad(go);
            go.AddComponent<InputDiagnostics>();
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _last = null;

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Interval;

            var summary = new StringBuilder();
            var eventSystem = EventSystem.current;
            var module = eventSystem != null ? eventSystem.GetComponent<PointableCanvasModule>() : null;
            summary.Append($"escena={SceneManager.GetActiveScene().name} eventSystem={(eventSystem != null)} ");
            summary.Append($"pointableModule={(module != null ? (module.isActiveAndEnabled ? "activo" : "inactivo") : "no")} ");
            summary.Append($"canvasConRayo={FindObjectsByType<PointableCanvas>(FindObjectsSortMode.None).Length} ");

            var rays = FindObjectsByType<RayInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            summary.Append($"rayos={rays.Length}");
            foreach (var ray in rays.OrderBy(r => r.transform.position.x))
            {
                string target = ray.HasInteractable ? ray.Interactable.name : "-";
                summary.Append($" | {Side(ray)}:{ray.State}→{target}");
            }

            string text = summary.ToString();
            if (text == _last) return;
            _last = text;
            Debug.Log($"[AnyDrums][Input] {text}");
        }

        static string Side(Component c)
        {
            for (var t = c.transform; t != null; t = t.parent)
            {
                if (t.name.Contains("Left")) return "izq";
                if (t.name.Contains("Right")) return "der";
            }
            return c.name;
        }

        /// <summary>Lo llaman los controles del menú cuando reciben un evento de la UI.</summary>
        public static void LogUiEvent(string what, Object target) =>
            Debug.Log($"[AnyDrums][Input] UI {what}: {(target != null ? target.name : "?")}");
    }
}
