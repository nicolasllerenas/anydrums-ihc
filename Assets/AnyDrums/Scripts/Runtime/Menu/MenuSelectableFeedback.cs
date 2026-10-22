using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Retroalimentación de un control del menú: se resalta y crece un poco cuando el rayo pasa encima,
    /// y al presionarlo suena un clic y vibra el mando.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class MenuSelectableFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] MenuAudio _audio;
        [SerializeField] RectTransform _scaleTarget;
        [SerializeField] float _hoverScale = 1.04f;
        [SerializeField] float _pressedScale = 0.97f;
        [SerializeField] bool _hoverSound = true;
        [SerializeField] bool _clickSound = true;
        [SerializeField, Range(0f, 1f)] float _hapticAmplitude = 0.45f;

        [Header("Color (opcional)")]
        [SerializeField] Graphic _tintTarget;
        [SerializeField] Color _normalColor = Color.white;
        [SerializeField] Color _hoverColor = Color.white;
        [SerializeField] Color _pressedColor = Color.white;

        Selectable _selectable;
        bool _hovered, _pressed;

        public bool IsHovered => _hovered;

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (_scaleTarget == null) _scaleTarget = (RectTransform)transform;
        }

        void OnDisable()
        {
            _hovered = _pressed = false;
            if (_scaleTarget != null) _scaleTarget.localScale = Vector3.one;
            if (_tintTarget != null) _tintTarget.color = _normalColor;
        }

        /// <summary>Cambia el color base; el resaltado y el presionado se derivan de él.</summary>
        public void SetBaseColor(Color normal)
        {
            _normalColor = normal;
            _hoverColor = Color.Lerp(normal, Color.white, 0.18f);
            _pressedColor = Color.Lerp(normal, Color.black, 0.15f);
            _hoverColor.a = _pressedColor.a = normal.a;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            InputDiagnostics.LogUiEvent("apuntado", this);
            if (!_selectable.IsInteractable()) return;
            _hovered = true;
            if (_hoverSound && _audio != null) _audio.PlayHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            InputDiagnostics.LogUiEvent("presionado", this);
            if (!_selectable.IsInteractable()) return;
            _pressed = true;
            if (_clickSound && _audio != null) _audio.PlayClick();
            ControllerHaptics.PulsePressingController(_hapticAmplitude);
        }

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;

        void Update()
        {
            bool interactable = _selectable.IsInteractable();
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);

            // Solo se escribe cuando falta llegar al valor final, para no reconstruir el Canvas en cada frame.
            float targetScale = !interactable ? 1f : _pressed ? _pressedScale : _hovered ? _hoverScale : 1f;
            Vector3 scale = _scaleTarget.localScale;
            if (Mathf.Abs(scale.x - targetScale) > 0.0005f)
            {
                float next = Mathf.Lerp(scale.x, targetScale, k);
                if (Mathf.Abs(next - targetScale) < 0.001f) next = targetScale;
                _scaleTarget.localScale = new Vector3(next, next, 1f);
            }

            if (_tintTarget != null)
            {
                Color target = !interactable ? _normalColor
                    : _pressed ? _pressedColor : _hovered ? _hoverColor : _normalColor;
                Color current = _tintTarget.color;
                if (current != target)
                {
                    Color next = Color.Lerp(current, target, k);
                    _tintTarget.color = Vector4.Distance(next, target) < 0.004f ? target : next;
                }
            }
        }
    }
}
