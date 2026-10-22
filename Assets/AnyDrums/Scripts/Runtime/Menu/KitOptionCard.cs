using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Tarjeta para elegir un tipo de batería. Muestra con claridad cuál está seleccionada:
    /// borde y brillo del color del kit, ícono coloreado y una insignia con check. Cuando suena el golpe
    /// de prueba, el brillo destella y unas barras de ecualizador se mueven al ritmo del sonido.
    /// </summary>
    public class KitOptionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly Color BackgroundIdle = new Color32(0x1A, 0x1D, 0x25, 0xFF);
        static readonly Color BackgroundHover = new Color32(0x22, 0x26, 0x30, 0xFF);
        // Bordes opacos (mezclados con el fondo de la tarjeta) para que se vean igual en espacio de color Gamma y Lineal.
        public static readonly Color BorderIdle = Opaque(Color.Lerp(BackgroundIdle, Color.white, 0.10f));
        static readonly Color BorderHover = Opaque(Color.Lerp(BackgroundIdle, Color.white, 0.32f));
        static readonly Color IconIdle = new Color32(0x8A, 0x91, 0xA1, 0xFF);
        static readonly Color TitleIdle = new Color32(0xC9, 0xCE, 0xD8, 0xFF);

        const float PulseDuration = 0.9f;

        [SerializeField] DrumKitType _kit;
        [SerializeField] Color _accent = Color.white;
        [SerializeField] Button _button;

        [Header("Partes visuales")]
        [SerializeField] Image _background;
        [SerializeField] Image _border;
        [SerializeField] Image _glow;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _genres;
        [SerializeField] CanvasGroup _checkBadge;
        [SerializeField] RectTransform[] _bars;

        bool _selected, _hovered;
        float _selection, _hover, _pulse;

        public DrumKitType Kit => _kit;
        public Color Accent => _accent;
        public Button Button => _button;
        public bool IsSelected => _selected;

        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        public void SetSelected(bool selected, bool instant = false)
        {
            _selected = selected;
            if (instant)
            {
                _selection = selected ? 1f : 0f;
                Apply();
            }
        }

        /// <summary>Destello y ecualizador mientras suena el golpe de prueba.</summary>
        public void PlayPulse() => _pulse = 1f;

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        void OnDisable() => _hovered = false;

        void Update()
        {
            float selectionTarget = _selected ? 1f : 0f;
            float hoverTarget = _hovered && _button.IsInteractable() ? 1f : 0f;
            // Sin cambios pendientes no se toca la UI, así el Canvas no se reconstruye en cada frame.
            if (_selection == selectionTarget && _hover == hoverTarget && _pulse <= 0f) return;

            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            _selection = Approach(_selection, selectionTarget, k);
            _hover = Approach(_hover, hoverTarget, k);
            _pulse = Mathf.Max(0f, _pulse - Time.unscaledDeltaTime / PulseDuration);
            Apply();
        }

        static float Approach(float current, float target, float k)
        {
            float next = Mathf.Lerp(current, target, k);
            return Mathf.Abs(next - target) < 0.002f ? target : next;
        }

        void Apply()
        {
            float pulse = _pulse * _pulse;
            Color selectedBackground = Color.Lerp(BackgroundIdle, _accent, 0.13f);
            _background.color = Color.Lerp(Color.Lerp(BackgroundIdle, BackgroundHover, _hover), selectedBackground, _selection);
            _border.color = Color.Lerp(Color.Lerp(BorderIdle, BorderHover, _hover), _accent, _selection);
            _icon.color = Color.Lerp(Color.Lerp(IconIdle, Color.white, _hover * 0.5f), _accent, _selection);
            _icon.rectTransform.localScale = Vector3.one * (1f + 0.12f * pulse * _selection);
            _title.color = Color.Lerp(TitleIdle, Color.white, Mathf.Max(_selection, _hover));

            var glow = _accent;
            glow.a = Mathf.Clamp01(0.3f * _selection + 0.35f * pulse);
            _glow.color = glow;

            var genres = _accent;
            genres.a = Mathf.Lerp(0.55f, 1f, _selection);
            _genres.color = genres;

            _checkBadge.alpha = _selection;
            _checkBadge.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, _selection);

            if (_bars == null) return;
            float time = Time.unscaledTime;
            for (int i = 0; i < _bars.Length; i++)
            {
                var bar = _bars[i];
                if (bar == null) continue;
                float wave = Mathf.Abs(Mathf.Sin(time * (9f + i * 2.3f) + i * 1.7f));
                float height = Mathf.Lerp(0.22f, 0.22f + 0.78f * wave, _pulse);
                bar.localScale = new Vector3(1f, height, 1f);
                if (bar.TryGetComponent(out Graphic graphic))
                {
                    // Solo se ven mientras suena el golpe de prueba.
                    var c = _accent;
                    c.a = _selection * Mathf.Clamp01(_pulse * 2.5f);
                    graphic.color = c;
                }
            }
        }
    }
}
