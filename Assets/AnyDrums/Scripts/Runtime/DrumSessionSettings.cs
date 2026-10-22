using System;
using UnityEngine;

namespace AnyDrums
{
    /// <summary>
    /// Configuración de la sesión elegida en el menú (tipo de batería y volumen general).
    /// Vive en DontDestroyOnLoad para que el estudio se cargue con el banco de samples y el volumen
    /// que eligió el usuario y para que, al volver al menú, la configuración actual aparezca ya marcada.
    /// Se crea sola al iniciar el juego, así también funciona si se da Play directamente en el estudio.
    /// </summary>
    public class DrumSessionSettings : MonoBehaviour
    {
        public const DrumKitType DefaultKit = DrumKitType.Acoustic;
        public const float DefaultVolume = 0.75f;

        static DrumSessionSettings _instance;

        [SerializeField] DrumKitType _kit = DefaultKit;
        [SerializeField, Range(0f, 1f)] float _masterVolume = DefaultVolume;

        DrumSampleBank _currentBank;

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
                LoadBank();
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

        /// <summary>Banco de samples del kit elegido (puede ser null si no hay catálogo).</summary>
        public DrumSampleBank CurrentBank => _currentBank;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateOnStartup() => _ = Instance;

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
            LoadBank();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void LoadBank()
        {
            var catalog = DrumKitCatalog.Load();
            _currentBank = catalog != null ? catalog.GetBank(_kit) : null;
            if (_currentBank != null) _currentBank.Preload();
        }
    }
}
