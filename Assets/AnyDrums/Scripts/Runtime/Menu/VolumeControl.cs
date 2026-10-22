using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Control de volumen general (RF4): slider de 0 a 100 %, botones − / + de 5 % para ajustes precisos
    /// con el rayo y un ícono que cambia según el nivel. Al soltar el slider o usar los botones suena
    /// un golpe de prueba con el volumen nuevo, para calibrarlo sin tener que tocar la batería.
    /// </summary>
    public class VolumeControl : MonoBehaviour
    {
        [SerializeField] Slider _slider;
        [SerializeField] TMP_Text _valueLabel;
        [SerializeField] Image _icon;
        [SerializeField] Sprite _muteSprite;
        [SerializeField] Sprite _lowSprite;
        [SerializeField] Sprite _highSprite;
        [SerializeField] Button _downButton;
        [SerializeField] Button _upButton;
        [SerializeField] PointerUpNotifier _release;
        [SerializeField] int _step = 5;
        [SerializeField] MenuAudio _audio;

        DrumSessionSettings _settings;

        public Slider Slider => _slider;
        public int Percent => Mathf.RoundToInt(_slider.value);

        void OnEnable()
        {
            _settings = DrumSessionSettings.Instance;
            _slider.SetValueWithoutNotify(Mathf.Round(_settings.MasterVolume * 100f));
            _slider.onValueChanged.AddListener(OnSliderChanged);
            _release.Released.AddListener(PlayPreview);
            _downButton.onClick.AddListener(StepDown);
            _upButton.onClick.AddListener(StepUp);
            _settings.Changed += OnSettingsChanged;
            Refresh();
        }

        void OnDisable()
        {
            _slider.onValueChanged.RemoveListener(OnSliderChanged);
            _release.Released.RemoveListener(PlayPreview);
            _downButton.onClick.RemoveListener(StepDown);
            _upButton.onClick.RemoveListener(StepUp);
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
        }

        public void SetPercent(float percent) => _slider.value = Mathf.Clamp(Mathf.Round(percent), 0f, 100f);

        public void StepDown() => Step(-_step);
        public void StepUp() => Step(_step);

        void Step(int delta)
        {
            float snapped = Mathf.Round((_slider.value + delta) / _step) * _step;
            SetPercent(snapped);
            PlayPreview();
        }

        void OnSliderChanged(float value)
        {
            _settings.MasterVolume = value / 100f;
            Refresh();
        }

        // Si el volumen cambia desde otro panel, este se actualiza sin volver a notificar.
        void OnSettingsChanged(DrumSessionSettings settings)
        {
            float percent = Mathf.Round(settings.MasterVolume * 100f);
            if (!Mathf.Approximately(_slider.value, percent)) _slider.SetValueWithoutNotify(percent);
            Refresh();
        }

        void PlayPreview()
        {
            if (_audio != null) _audio.PlayKitPreview(_settings.Kit);
        }

        void Refresh()
        {
            int percent = Percent;
            _valueLabel.text = $"{percent} %";
            _icon.sprite = percent == 0 ? _muteSprite : percent < 50 ? _lowSprite : _highSprite;
            _downButton.interactable = percent > 0;
            _upButton.interactable = percent < 100;
        }
    }
}
