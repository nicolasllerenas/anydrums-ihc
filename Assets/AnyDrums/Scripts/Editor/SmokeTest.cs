using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AnyDrums.Menu;
using AnyDrums.Studio;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AnyDrums.EditorTools
{
    /// <summary>
    /// Prueba automática del Entregable 2 (RF1 y RF4) en Play Mode, sin visor: recorre el menú, entra al estudio,
    /// usa el panel de sesión, vuelve al menú y entra de nuevo. Revisa el estado en cada paso, guarda capturas
    /// en Logs/ y el resultado en Logs/SmokeTest.txt. Cualquier excepción en la consola cuenta como falla.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeTest
    {
        const string RunningKey = "AnyDrums.SmokeTest.Running";
        const string ExitKey = "AnyDrums.SmokeTest.Exit";
        const string ReportPath = "Logs/SmokeTest.txt";
        const double StepTimeout = 20.0;

        static readonly List<string> Lines = new List<string>();
        static readonly List<string> ConsoleErrors = new List<string>();
        static int _failures;
        static IEnumerator _routine;
        static double _waitUntil;
        static Func<bool> _condition;
        static string _conditionName;
        static double _conditionDeadline;

        static SmokeTest()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("AnyDrums/Prueba automática del Entregable 2 (Play Mode)", priority = 40)]
        static void RunFromMenu() => Begin(exitWhenDone: false);

        /// <summary>Unity -projectPath . -executeMethod AnyDrums.EditorTools.SmokeTest.RunFromCommandLine</summary>
        public static void RunFromCommandLine() => Begin(exitWhenDone: true);

        static void Begin(bool exitWhenDone)
        {
            EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(ExitKey, exitWhenDone);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Lines.Clear();
                ConsoleErrors.Clear();
                _failures = 0;
                _deprecatedWarnings = 0;
                Application.logMessageReceived += OnLog;
                _routine = Scenario();
                _waitUntil = 0;
                _condition = null;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(RunningKey, false);
                if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(_failures > 0 ? 1 : 0);
            }
        }

        static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error)
                ConsoleErrors.Add($"{type}: {message.Split('\n')[0]}");
            else if (type == LogType.Warning && message.Contains("has been deprecated"))
                _deprecatedWarnings++;
        }

        static int _deprecatedWarnings;

        // Simula dónde caería un clic del mouse sobre el centro de un control y revisa que lo reciba ese control.
        static bool MouseReaches(Component target)
        {
            var cam = Camera.main;
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (cam == null || eventSystem == null) return false;
            Vector3 screen = cam.WorldToScreenPoint(target.transform.position);
            var data = new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = screen };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(data, hits);
            return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(target.transform);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || _routine == null) return;
            double now = EditorApplication.timeSinceStartup;
            if (_condition != null)
            {
                bool done = _condition();
                if (!done && now < _conditionDeadline) return;
                if (!done) Check(false, $"Esperar: {_conditionName}");
                _condition = null;
            }
            if (now < _waitUntil) return;

            try
            {
                if (!_routine.MoveNext())
                {
                    Finish();
                    return;
                }
            }
            catch (Exception e)
            {
                Check(false, $"Excepción en la prueba: {e.Message}");
                Finish();
                return;
            }

            switch (_routine.Current)
            {
                case float seconds:
                    _waitUntil = now + seconds;
                    break;
                case ValueTuple<string, Func<bool>> wait:
                    _conditionName = wait.Item1;
                    _condition = wait.Item2;
                    _conditionDeadline = now + StepTimeout;
                    break;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            _routine = null;

            var exceptions = ConsoleErrors.Where(e => e.StartsWith("Exception")).ToList();
            Check(exceptions.Count == 0, $"Sin excepciones en la consola ({exceptions.Count})");
            Check(_deprecatedWarnings == 0, $"Sin advertencias de prefabs obsoletos del rig ({_deprecatedWarnings})");

            var report = new StringBuilder();
            report.AppendLine($"Prueba automática AnyDrums – {DateTime.Now:yyyy-MM-dd HH:mm}");
            report.AppendLine(_failures == 0 ? "RESULTADO: OK" : $"RESULTADO: {_failures} FALLA(S)");
            report.AppendLine();
            foreach (var line in Lines) report.AppendLine(line);
            report.AppendLine();
            report.AppendLine($"Errores y excepciones en la consola ({ConsoleErrors.Count}):");
            foreach (var error in ConsoleErrors.Distinct()) report.AppendLine("  " + error);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log($"[AnyDrums] Prueba terminada: {(_failures == 0 ? "OK" : _failures + " falla(s)")}. Detalle en {ReportPath}");
            EditorApplication.ExitPlaymode();
        }

        static void Check(bool condition, string description)
        {
            Lines.Add($"{(condition ? "[OK]   " : "[FALLA]")} {description}");
            if (!condition) _failures++;
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.006f;

        static (string, Func<bool>) Until(string name, Func<bool> condition) => (name, condition);

        static IEnumerable<GameObject> AllInScene() =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject);

        static GameObject FindInScene(string name) => AllInScene().FirstOrDefault(g => g.name == name);

        static int ActiveRayCount() =>
            Object.FindObjectsByType<RayInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

        static KitOptionCard Card(Component root, DrumKitType kit) =>
            root.GetComponentsInChildren<KitOptionCard>(true).FirstOrDefault(c => c.Kit == kit);

        static IEnumerator Scenario()
        {
            yield return 1.5f;
            var settings = DrumSessionSettings.Instance;

            // ---------- Escena 1: menú inicial ----------
            Lines.Add("— Menú inicial —");
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Check(menu != null, "El menú inicial carga");
            if (menu == null) yield break;
            Check(settings.Kit == DrumKitType.Acoustic, "La batería acústica viene seleccionada por defecto");
            Check(Near(settings.MasterVolume, 0.75f) && Near(AudioListener.volume, 0.75f), "El volumen inicia en 75 % y se aplica al audio");
            Check(settings.CurrentBank != null && settings.CurrentBank.Kit == DrumKitType.Acoustic, "Se carga el banco de samples acústico");
            Check(menu.VolumeControl.Percent == 75, "El slider muestra 75 %");
            var acousticCard = Card(menu, DrumKitType.Acoustic);
            var electronicCard = Card(menu, DrumKitType.Electronic);
            Check(acousticCard.IsSelected && !electronicCard.IsSelected, "La tarjeta acústica se ve seleccionada");
            MainMenuSceneBuilder.CapturePreview("Logs/Test_1_Menu_Acustica.png");

            // Sin visor (como al dar Play sin Quest Link) el menú debe verse y usarse con el mouse.
            Check(DesktopFallback.IsActive, "Sin visor se activa el modo escritorio");
            Check(Camera.main != null && Mathf.Abs(Camera.main.transform.position.y - 1.6f) < 0.05f,
                "En modo escritorio la cámara queda a la altura de los ojos (1.6 m)");
            Check(MouseReaches(menu.EnterButton), "Un clic del mouse sobre \"Ingresar al estudio\" llega al botón");
            Check(MouseReaches(electronicCard), "Un clic del mouse sobre la tarjeta electrónica llega a la tarjeta");

            electronicCard.Button.onClick.Invoke();
            yield return 0.5f;
            Check(settings.Kit == DrumKitType.Electronic, "Elegir \"Batería electrónica\" cambia el kit");
            Check(settings.CurrentBank != null && settings.CurrentBank.Kit == DrumKitType.Electronic, "Se carga el banco de samples electrónico");
            Check(electronicCard.IsSelected && !acousticCard.IsSelected, "Solo la tarjeta electrónica queda seleccionada");
            Check(menu.EnterSummary.text.Contains("electrónica"), "El botón de ingreso resume el kit elegido");

            menu.VolumeControl.SetPercent(40f);
            Check(Near(settings.MasterVolume, 0.40f) && Near(AudioListener.volume, 0.40f), "Mover el slider a 40 % cambia el volumen general");
            menu.VolumeControl.StepDown();
            Check(menu.VolumeControl.Percent == 35 && Near(AudioListener.volume, 0.35f), "El botón − baja 5 % (35 %)");
            Check(menu.EnterSummary.text.Contains("35 %"), "El resumen muestra el volumen actual");
            yield return 0.6f;
            MainMenuSceneBuilder.CapturePreview("Logs/Test_2_Menu_Electronica.png");

            // ---------- Paso de la configuración a la escena 2 ----------
            Lines.Add("— Ingreso al estudio —");
            menu.EnterButton.onClick.Invoke();
            Check(SceneTransition.IsBusy, "\"Ingresar al estudio\" inicia la transición con fundido");
            yield return Until("cargar Studio", () => SceneManager.GetActiveScene().name == "Studio" && !SceneTransition.IsBusy);
            Check(SceneManager.GetActiveScene().name == "Studio", "Se carga la escena del estudio");
            yield return 0.5f;
            var kitAcoustic = FindInScene("Kit_Acoustic");
            var kitElectronic = FindInScene("Kit_Electronic");
            Check(kitElectronic != null && kitElectronic.activeInHierarchy && kitAcoustic != null && !kitAcoustic.activeInHierarchy,
                "El estudio muestra la batería electrónica elegida");
            Check(Near(AudioListener.volume, 0.35f), "El estudio conserva el volumen elegido (35 %)");
            var head = Camera.main;
            Check(head != null && Mathf.Abs(head.transform.position.y - 1.6f) < 0.05f,
                "En el estudio (modo escritorio) la cámara queda a la altura de los ojos");
            if (head != null)
                MainMenuSceneBuilder.CaptureView("Logs/Test_Studio_Escritorio.png", head.transform.position, head.transform.rotation, head.fieldOfView);
            Check(settings.CurrentBank.Kit == DrumKitType.Electronic, "El estudio usa el banco de samples electrónico");

            var panel = Object.FindFirstObjectByType<SessionPanelController>();
            Check(panel != null, "El estudio tiene panel de sesión");
            if (panel == null) yield break;
            Check(!SessionPanelController.IsOpen && !panel.Panel.gameObject.activeSelf, "El panel empieza cerrado");
            Check(ActiveRayCount() == 0, "Con el panel cerrado no se ven los rayos de los mandos");

            var source = DrumKitAudio.Instance != null ? DrumKitAudio.Instance.Play(DrumPiece.Kick, kitElectronic.transform.position) : null;
            Check(source != null && source.clip == settings.CurrentBank.Get(DrumPiece.Kick), "DrumKitAudio reproduce el bombo del banco actual");

            // ---------- Panel de sesión ----------
            Lines.Add("— Panel de sesión —");
            int sceneHandle = SceneManager.GetActiveScene().handle;
            panel.Toggle();
            yield return 0.4f;
            Check(SessionPanelController.IsOpen && panel.Panel.gameObject.activeSelf, "El botón de menú abre el panel");
            Check(ActiveRayCount() > 0, "Con el panel abierto aparecen los rayos");
            var panelVolume = panel.Panel.GetComponentInChildren<VolumeControl>(true);
            Check(panelVolume.Percent == 35, "El panel muestra el volumen actual (35 %)");
            var panelElectronic = Card(panel.Panel, DrumKitType.Electronic);
            Check(panelElectronic.IsSelected, "El panel muestra el kit actual");
            Check(MouseReaches(panel.BackToMenuButton), "En el panel, un clic del mouse llega a \"Volver al menú\"");
            var panelGroup = panel.Panel.GetComponent<CanvasGroup>();
            yield return Until("terminar la animación de apertura", () => panelGroup.alpha >= 0.999f);
            Check(panelGroup.alpha >= 0.999f, "El panel termina de aparecer (opaco)");
            if (head != null)
            {
                MainMenuSceneBuilder.CaptureView("Logs/Test_Studio_Panel_Escritorio.png", head.transform.position, head.transform.rotation, head.fieldOfView);
                // Solo el panel sobre fondo blanco: sirve para ver si el fondo del panel deja pasar el cuarto.
                MainMenuSceneBuilder.CaptureView("Logs/Test_Panel_SobreBlanco.png", head.transform.position, head.transform.rotation, head.fieldOfView,
                    1 << MenuUIKit.UILayer, Color.white);
            }

            Card(panel.Panel, DrumKitType.Acoustic).Button.onClick.Invoke();
            yield return 0.4f;
            Check(settings.Kit == DrumKitType.Acoustic && kitAcoustic.activeInHierarchy && !kitElectronic.activeInHierarchy,
                "Cambiar a acústica desde el panel cambia la batería del estudio");
            Check(SceneManager.GetActiveScene().handle == sceneHandle, "El cambio de kit no recarga la escena");
            Check(settings.CurrentBank.Kit == DrumKitType.Acoustic, "Se reemplaza el banco de samples por el acústico");
            panelVolume.StepUp();
            Check(Near(AudioListener.volume, 0.40f), "El botón + del panel sube el volumen (40 %)");
            yield return 0.5f;
            var p = panel.Panel;
            MainMenuSceneBuilder.CaptureView("Logs/Test_3_Panel_Sesion.png", p.position - p.forward * 0.95f + Vector3.up * 0.05f,
                Quaternion.LookRotation(p.forward), 60f);

            panel.Toggle();
            yield return 0.4f;
            Check(!SessionPanelController.IsOpen && !panel.Panel.gameObject.activeSelf, "El botón de menú cierra el panel");
            Check(ActiveRayCount() == 0, "Al cerrar el panel se ocultan los rayos");

            // ---------- Volver al menú ----------
            Lines.Add("— Volver al menú —");
            panel.Toggle();
            yield return 0.3f;
            panel.BackToMenuButton.onClick.Invoke();
            yield return Until("volver al menú", () => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransition.IsBusy);
            Check(SceneManager.GetActiveScene().name == "MainMenu", "\"Volver al menú\" carga el menú inicial");
            Check(!SessionPanelController.IsOpen, "Al salir del estudio el panel queda cerrado");
            yield return 0.5f;
            menu = Object.FindFirstObjectByType<MainMenuController>();
            Check(Card(menu, DrumKitType.Acoustic).IsSelected && !Card(menu, DrumKitType.Electronic).IsSelected,
                "El menú muestra marcado el kit actual (acústica)");
            Check(menu.VolumeControl.Percent == 40, "El menú muestra el volumen actual (40 %)");
            Check(!SceneTransition.IsBusy, "La transición quedó libre al volver al menú");

            menu.EnterButton.onClick.Invoke();
            yield return Until("volver a entrar al estudio", () => SceneManager.GetActiveScene().name == "Studio" && !SceneTransition.IsBusy);
            yield return 0.3f;
            Check(SceneManager.GetActiveScene().name == "Studio" && FindInScene("Kit_Acoustic").activeInHierarchy,
                "Se puede volver a entrar al estudio, ahora con la batería acústica");
        }
    }
}
