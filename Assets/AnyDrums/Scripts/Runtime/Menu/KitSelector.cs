using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Selección de batería acústica o electrónica (RF1). Se usa en el menú inicial y en el panel de sesión:
    /// ambos leen y escriben la misma <see cref="DrumSessionSettings"/>, así siempre muestran lo mismo.
    /// </summary>
    public class KitSelector : MonoBehaviour
    {
        [SerializeField] KitOptionCard _acousticCard;
        [SerializeField] KitOptionCard _electronicCard;
        [SerializeField] MenuAudio _audio;

        DrumSessionSettings _settings;

        void OnEnable()
        {
            _settings = DrumSessionSettings.Instance;
            _acousticCard.Button.onClick.AddListener(SelectAcoustic);
            _electronicCard.Button.onClick.AddListener(SelectElectronic);
            _settings.Changed += OnSettingsChanged;
            Refresh(instant: true);
        }

        void OnDisable()
        {
            _acousticCard.Button.onClick.RemoveListener(SelectAcoustic);
            _electronicCard.Button.onClick.RemoveListener(SelectElectronic);
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
        }

        void SelectAcoustic() => Select(DrumKitType.Acoustic);
        void SelectElectronic() => Select(DrumKitType.Electronic);

        /// <summary>Elige el kit, carga su banco de samples y reproduce un golpe de prueba.</summary>
        public void Select(DrumKitType kit)
        {
            _settings.Kit = kit;
            Refresh(instant: false);
            if (_audio != null) _audio.PlayKitPreview(kit);
            (kit == DrumKitType.Acoustic ? _acousticCard : _electronicCard).PlayPulse();
        }

        void OnSettingsChanged(DrumSessionSettings settings) => Refresh(instant: false);

        void Refresh(bool instant)
        {
            _acousticCard.SetSelected(_settings.Kit == DrumKitType.Acoustic, instant);
            _electronicCard.SetSelected(_settings.Kit == DrumKitType.Electronic, instant);
        }
    }
}
