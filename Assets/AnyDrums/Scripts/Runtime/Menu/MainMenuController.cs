using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Escena 1 (menú inicial). La selección de batería (RF1) y el volumen (RF4) los manejan
    /// <see cref="KitSelector"/> y <see cref="VolumeControl"/>; este componente se encarga de entrar al
    /// estudio con fundido a negro, de salir y de los mensajes al usuario. Por defecto la batería acústica
    /// está seleccionada y el volumen en 75 %, así "Ingresar al estudio" funciona aunque no se cambie nada.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        const string HintText = "Apunta con el mando y presiona el gatillo para seleccionar";

        [SerializeField] KitSelector _kitSelector;
        [SerializeField] VolumeControl _volumeControl;
        [SerializeField] Button _enterButton;
        [Tooltip("Resumen de lo elegido debajo de \"Ingresar al estudio\".")]
        [SerializeField] TMP_Text _enterSummary;
        [SerializeField] Button _exitButton;
        [SerializeField] TMP_Text _hintLabel;
        [SerializeField] CanvasGroup _panelGroup;

        DrumSessionSettings _settings;
        Coroutine _hintRoutine;
        Color _hintColor;

        public KitSelector KitSelector => _kitSelector;
        public VolumeControl VolumeControl => _volumeControl;
        public Button EnterButton => _enterButton;
        public TMP_Text EnterSummary => _enterSummary;

        void Awake()
        {
            _settings = DrumSessionSettings.Instance;
            _hintColor = _hintLabel.color;
        }

        void OnEnable()
        {
            _enterButton.onClick.AddListener(EnterStudio);
            _exitButton.onClick.AddListener(QuitApplication);
            _settings.Changed += OnSettingsChanged;
        }

        void OnDisable()
        {
            _enterButton.onClick.RemoveListener(EnterStudio);
            _exitButton.onClick.RemoveListener(QuitApplication);
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
        }

        void Start()
        {
            _hintLabel.text = HintText;
            _panelGroup.interactable = true;
            RefreshSummary();
            SceneTransition.FadeInFromBlack();
        }

        public void EnterStudio()
        {
            if (SceneTransition.IsBusy) return;
            var catalog = DrumKitCatalog.Load();
            string scene = catalog != null ? catalog.StudioScene : "Studio";
            _panelGroup.interactable = false;
            if (!SceneTransition.LoadScene(scene))
            {
                _panelGroup.interactable = true;
                ShowHint($"No se encontró la escena \"{scene}\". Agrégala en File > Build Settings.", warning: true);
            }
        }

        public void QuitApplication()
        {
            if (SceneTransition.IsBusy) return;
            _panelGroup.interactable = false;
            SceneTransition.Quit();
        }

        void OnSettingsChanged(DrumSessionSettings settings) => RefreshSummary();

        void RefreshSummary()
        {
            if (_enterSummary != null)
                _enterSummary.text = $"{KitPalette.DisplayName(_settings.Kit)}  ·  Volumen {Mathf.RoundToInt(_settings.MasterVolume * 100f)} %";
        }

        void ShowHint(string message, bool warning)
        {
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            _hintRoutine = StartCoroutine(HintRoutine(message, warning));
        }

        IEnumerator HintRoutine(string message, bool warning)
        {
            _hintLabel.text = message;
            _hintLabel.color = warning ? new Color32(0xFF, 0xB4, 0x54, 0xFF) : _hintColor;
            yield return new WaitForSecondsRealtime(5f);
            _hintLabel.text = HintText;
            _hintLabel.color = _hintColor;
            _hintRoutine = null;
        }
    }
}
