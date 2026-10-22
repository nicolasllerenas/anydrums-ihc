using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Tiñe un panel con el color del kit elegido (ámbar o cian) con una transición suave,
    /// para que toda la interfaz confirme la elección y no solo la tarjeta.
    /// </summary>
    public class AccentTheme : MonoBehaviour
    {
        static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        [Tooltip("Elementos que toman el color del kit; se conserva su transparencia.")]
        [SerializeField] Graphic[] _graphics;
        [Tooltip("Botones cuyo fondo toma el color del kit.")]
        [SerializeField] MenuSelectableFeedback[] _accentButtons;
        [SerializeField] Slider[] _sliders;
        [SerializeField] Renderer _floorGlow;
        [SerializeField] TMP_Text _title;
        [SerializeField] string _titleFormat = "AnyDrums <color=#{0}>VR</color>";

        DrumSessionSettings _settings;
        MaterialPropertyBlock _block;
        Color _accent, _target;

        void OnEnable()
        {
            _settings = DrumSessionSettings.Instance;
            _settings.Changed += OnSettingsChanged;
            _accent = _target = KitPalette.AccentFor(_settings.Kit);
            Apply();
        }

        void OnDisable()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
        }

        void OnSettingsChanged(DrumSessionSettings settings) => _target = KitPalette.AccentFor(settings.Kit);

        void Update()
        {
            if (_accent == _target) return;
            _accent = Color.Lerp(_accent, _target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if (Vector4.Distance(_accent, _target) < 0.002f) _accent = _target;
            Apply();
        }

        void Apply()
        {
            foreach (var graphic in _graphics)
            {
                if (graphic == null) continue;
                var c = _accent;
                c.a = graphic.color.a;
                graphic.color = c;
            }

            foreach (var button in _accentButtons)
                if (button != null) button.SetBaseColor(_accent);

            foreach (var slider in _sliders)
            {
                if (slider == null) continue;
                var colors = slider.colors;
                colors.highlightedColor = Color.Lerp(Color.white, _accent, 0.35f);
                colors.pressedColor = _accent;
                colors.selectedColor = Color.white;
                slider.colors = colors;
            }

            if (_title != null && !string.IsNullOrEmpty(_titleFormat))
                _title.text = string.Format(_titleFormat, ColorUtility.ToHtmlStringRGB(_accent));

            if (_floorGlow != null)
            {
                _block ??= new MaterialPropertyBlock();
                _floorGlow.GetPropertyBlock(_block);
                _block.SetColor(TintColorId, new Color(_accent.r, _accent.g, _accent.b, 0.22f));
                _floorGlow.SetPropertyBlock(_block);
            }
        }
    }
}
