using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Escena 1 (menú inicial): selección de batería acústica o electrónica (RF1), volumen general (RF4)
    /// y entrada al estudio con fundido a negro. La elección se guarda en <see cref="DrumSessionSettings"/>.
    /// Por defecto la batería acústica está seleccionada y el volumen en 75 %, así "Ingresar al estudio"
    /// funciona aunque el usuario no cambie nada.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Tipo de batería")]
        [SerializeField] KitOptionCard _acousticCard;
        [SerializeField] KitOptionCard _electronicCard;

        [Header("Volumen")]
        [SerializeField] Slider _volumeSlider;
        [SerializeField] TMP_Text _volumeValueLabel;
        [SerializeField] Image _volumeIcon;
        [SerializeField] Sprite _volumeMuteSprite;
        [SerializeField] Sprite _volumeLowSprite;
        [SerializeField] Sprite _volumeHighSprite;
        [SerializeField] Button _volumeDownButton;
        [SerializeField] Button _volumeUpButton;
        [SerializeField] int _volumeStep = 5;
        [SerializeField] PointerUpNotifier _volumeRelease;

        [Header("Acciones")]
        [SerializeField] Button _enterButton;
        [SerializeField] MenuSelectableFeedback _enterButtonFeedback;
        [SerializeField] Button _exitButton;

        [Header("Escenas del estudio (deben estar en Build Settings)")]
        [SerializeField] string _acousticScene = "drumsAcustic";
        [SerializeField] string _electronicScene = "drumsElectric";

        [Header("Color del kit seleccionado")]
        [Tooltip("Elementos que toman el color del kit; se conserva su transparencia.")]
        [SerializeField] Graphic[] _accentGraphics;
        [SerializeField] Renderer _floorGlow;
        [SerializeField] TMP_Text _title;

        [Header("Otros")]
        [SerializeField] TMP_Text _hintLabel;
        [SerializeField] CanvasGroup _panelGroup;
        [SerializeField] MenuAudio _audio;

        const string TitleText = "AnyDrums <color=#{0}>VR</color>";
        const string HintText = "Apunta con el mando y presiona el gatillo para seleccionar";
        static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        DrumSessionSettings _settings;
        Color _accent;
        Color _targetAccent;
        MaterialPropertyBlock _floorBlock;
        Coroutine _hintRoutine;
        Color _hintColor;

        public DrumKitType SelectedKit => _settings.Kit;
        public float VolumePercent => _volumeSlider.value;

        void Awake()
        {
            _settings = DrumSessionSettings.Instance;
            _floorBlock = new MaterialPropertyBlock();
            _hintColor = _hintLabel.color;
        }

        void OnEnable()
        {
            _acousticCard.Button.onClick.AddListener(OnAcousticClicked);
            _electronicCard.Button.onClick.AddListener(OnElectronicClicked);
            _volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
            _volumeRelease.Released.AddListener(OnVolumeReleased);
            _volumeDownButton.onClick.AddListener(OnVolumeDown);
            _volumeUpButton.onClick.AddListener(OnVolumeUp);
            _enterButton.onClick.AddListener(EnterStudio);
            _exitButton.onClick.AddListener(QuitApplication);
        }

        void OnDisable()
        {
            _acousticCard.Button.onClick.RemoveListener(OnAcousticClicked);
            _electronicCard.Button.onClick.RemoveListener(OnElectronicClicked);
            _volumeSlider.onValueChanged.RemoveListener(OnVolumeSliderChanged);
            _volumeRelease.Released.RemoveListener(OnVolumeReleased);
            _volumeDownButton.onClick.RemoveListener(OnVolumeDown);
            _volumeUpButton.onClick.RemoveListener(OnVolumeUp);
            _enterButton.onClick.RemoveListener(EnterStudio);
            _exitButton.onClick.RemoveListener(QuitApplication);
        }

        void Start()
        {
            // Al volver desde el estudio, el menú muestra la configuración que ya estaba elegida.
            _volumeSlider.SetValueWithoutNotify(Mathf.Round(_settings.MasterVolume * 100f));
            ApplyKit(_settings.Kit, instant: true);
            RefreshVolume();
            _hintLabel.text = HintText;
            SceneTransition.FadeInFromBlack();
        }

        void Update()
        {
            if (_accent == _targetAccent) return;
            _accent = Color.Lerp(_accent, _targetAccent, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if (Vector4.Distance(_accent, _targetAccent) < 0.002f) _accent = _targetAccent;
            ApplyAccent();
        }

        // ---------- API pública (también sirve para pruebas automáticas) ----------

        public void SelectKit(DrumKitType kit)
        {
            _settings.Kit = kit;
            ApplyKit(kit, instant: false);
            if (_audio != null) _audio.PlayKitPreview(kit);
        }

        public void SetVolumePercent(float percent) => _volumeSlider.value = Mathf.Clamp(percent, 0f, 100f);

        public void EnterStudio()
        {
            if (SceneTransition.IsBusy) return;
            string scene = _settings.Kit == DrumKitType.Acoustic ? _acousticScene : _electronicScene;
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

        // ---------- Eventos de la UI ----------

        void OnAcousticClicked() => SelectKit(DrumKitType.Acoustic);
        void OnElectronicClicked() => SelectKit(DrumKitType.Electronic);

        void OnVolumeSliderChanged(float value)
        {
            _settings.MasterVolume = value / 100f;
            RefreshVolume();
        }

        // Al soltar el slider se repite el golpe con el volumen nuevo para calibrarlo sin entrar al estudio.
        void OnVolumeReleased()
        {
            if (_audio != null) _audio.PlayKitPreview(_settings.Kit);
        }

        void OnVolumeDown() => StepVolume(-_volumeStep);
        void OnVolumeUp() => StepVolume(_volumeStep);

        void StepVolume(int delta)
        {
            float snapped = Mathf.Round((_volumeSlider.value + delta) / _volumeStep) * _volumeStep;
            _volumeSlider.value = Mathf.Clamp(snapped, 0f, 100f);
            if (_audio != null) _audio.PlayKitPreview(_settings.Kit);
        }

        // ---------- Presentación ----------

        void ApplyKit(DrumKitType kit, bool instant)
        {
            _acousticCard.SetSelected(kit == DrumKitType.Acoustic, instant);
            _electronicCard.SetSelected(kit == DrumKitType.Electronic, instant);
            _targetAccent = kit == DrumKitType.Acoustic ? _acousticCard.Accent : _electronicCard.Accent;
            if (instant)
            {
                _accent = _targetAccent;
                ApplyAccent();
            }
        }

        void ApplyAccent()
        {
            foreach (var graphic in _accentGraphics)
            {
                if (graphic == null) continue;
                var c = _accent;
                c.a = graphic.color.a;
                graphic.color = c;
            }

            if (_enterButtonFeedback != null) _enterButtonFeedback.SetBaseColor(_accent);

            var handleColors = _volumeSlider.colors;
            handleColors.highlightedColor = Color.Lerp(Color.white, _accent, 0.35f);
            handleColors.pressedColor = _accent;
            handleColors.selectedColor = Color.white;
            _volumeSlider.colors = handleColors;

            if (_title != null) _title.text = string.Format(TitleText, ColorUtility.ToHtmlStringRGB(_accent));

            if (_floorGlow != null)
            {
                _floorGlow.GetPropertyBlock(_floorBlock);
                _floorBlock.SetColor(TintColorId, new Color(_accent.r, _accent.g, _accent.b, 0.22f));
                _floorGlow.SetPropertyBlock(_floorBlock);
            }
        }

        void RefreshVolume()
        {
            int percent = Mathf.RoundToInt(_volumeSlider.value);
            _volumeValueLabel.text = $"{percent} %";
            _volumeIcon.sprite = percent == 0 ? _volumeMuteSprite : percent < 50 ? _volumeLowSprite : _volumeHighSprite;
            _volumeDownButton.interactable = percent > 0;
            _volumeUpButton.interactable = percent < 100;
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
