using System;
using UnityEngine;

namespace AnyDrums
{
    /// <summary>
    /// Configuración de la sesión elegida en el menú (tipo de batería y volumen general).
    /// Vive en DontDestroyOnLoad para que el estudio se cargue con lo que eligió el usuario
    /// y para que, al volver al menú, la configuración actual aparezca ya marcada.
    /// </summary>
    public class DrumSessionSettings : MonoBehaviour
    {
        public const DrumKitType DefaultKit = DrumKitType.Acoustic;
        public const float DefaultVolume = 0.75f;

        static DrumSessionSettings _instance;

        [SerializeField] DrumKitType _kit = DefaultKit;
        [SerializeField, Range(0f, 1f)] float _masterVolume = DefaultVolume;

        /// <summary>Se dispara cuando cambia el kit o el volumen.</summary>
        public event Action<DrumSessionSettings> Changed;

        public static DrumSessionSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<DrumSessionSettings>();
                    if (_instance == null)
                        _instance = new GameObject("[DrumSessionSettings]").AddComponent<DrumSessionSettings>();
                }
                return _instance;
            }
        }

        public DrumKitType Kit
        {
            get => _kit;
            set
            {
                if (_kit == value) return;
                _kit = value;
                Changed?.Invoke(this);
            }
        }

        /// <summary>Volumen general de 0 a 1. Se aplica a todo el audio mediante AudioListener.volume (RF4).</summary>
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_masterVolume, value)) return;
                _masterVolume = value;
                AudioListener.volume = value;
                Changed?.Invoke(this);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            AudioListener.volume = _masterVolume;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
