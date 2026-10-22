using UnityEngine;

namespace AnyDrums.Studio
{
    /// <summary>
    /// Muestra en el estudio el modelo del kit elegido (acústico o electrónico). Si el kit cambia desde el
    /// panel de sesión, el modelo se reemplaza al instante sin recargar la escena y el usuario sigue en su lugar.
    /// </summary>
    public class StudioKitSwitcher : MonoBehaviour
    {
        [SerializeField] GameObject _acousticKit;
        [SerializeField] GameObject _electronicKit;

        DrumSessionSettings _settings;

        void Awake()
        {
            _settings = DrumSessionSettings.Instance;
            Apply(_settings.Kit);
        }

        void OnEnable()
        {
            _settings ??= DrumSessionSettings.Instance;
            _settings.Changed += OnSettingsChanged;
            Apply(_settings.Kit);
        }

        void OnDisable()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
        }

        void OnSettingsChanged(DrumSessionSettings settings) => Apply(settings.Kit);

        void Apply(DrumKitType kit)
        {
            if (_acousticKit != null && _acousticKit.activeSelf != (kit == DrumKitType.Acoustic))
                _acousticKit.SetActive(kit == DrumKitType.Acoustic);
            if (_electronicKit != null && _electronicKit.activeSelf != (kit == DrumKitType.Electronic))
                _electronicKit.SetActive(kit == DrumKitType.Electronic);
        }
    }
}
