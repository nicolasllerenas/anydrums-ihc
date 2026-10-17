using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Tarjeta para elegir un tipo de batería. Muestra con claridad cuál está seleccionada:
    /// borde y brillo del color del kit, ícono coloreado y una insignia con check.
    /// </summary>
    public class KitOptionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly Color BackgroundIdle = new Color32(0x1A, 0x1D, 0x25, 0xFF);
        static readonly Color BorderIdle = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color BorderHover = new Color(1f, 1f, 1f, 0.32f);
        static readonly Color IconIdle = new Color32(0x8A, 0x91, 0xA1, 0xFF);
        static readonly Color TitleIdle = new Color32(0xC9, 0xCE, 0xD8, 0xFF);

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

        bool _selected, _hovered;
        float _selection, _hover;

        public DrumKitType Kit => _kit;
        public Color Accent => _accent;
        public Button Button => _button;

        public void SetSelected(bool selected, bool instant = false)
        {
            _selected = selected;
            if (instant)
            {
                _selection = selected ? 1f : 0f;
                Apply();
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        void OnDisable() => _hovered = false;

        void Update()
        {
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            _selection = Mathf.Lerp(_selection, _selected ? 1f : 0f, k);
            _hover = Mathf.Lerp(_hover, _hovered && _button.IsInteractable() ? 1f : 0f, k);
            Apply();
        }

        void Apply()
        {
            Color selectedBackground = Color.Lerp(BackgroundIdle, _accent, 0.13f);
            _background.color = Color.Lerp(Color.Lerp(BackgroundIdle, new Color32(0x22, 0x26, 0x30, 0xFF), _hover), selectedBackground, _selection);
            _border.color = Color.Lerp(Color.Lerp(BorderIdle, BorderHover, _hover), _accent, _selection);
            _icon.color = Color.Lerp(Color.Lerp(IconIdle, Color.white, _hover * 0.5f), _accent, _selection);
            _title.color = Color.Lerp(TitleIdle, Color.white, Mathf.Max(_selection, _hover));

            var glow = _accent;
            glow.a = 0.45f * _selection;
            _glow.color = glow;

            var genres = _accent;
            genres.a = Mathf.Lerp(0.55f, 1f, _selection);
            _genres.color = genres;

            _checkBadge.alpha = _selection;
            _checkBadge.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, _selection);
        }
    }
}
