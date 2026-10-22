using UnityEngine;

namespace AnyDrums.Studio
{
    /// <summary>
    /// Reproduce los samples del banco del kit elegido. Usa un grupo de AudioSources reutilizables para que
    /// varios golpes seguidos se puedan superponer. Los golpes de las baquetas (Entregables 3 a 5) llaman a
    /// <see cref="Play"/> con la pieza, la posición del impacto y el volumen o tono calculados.
    /// </summary>
    public class DrumKitAudio : MonoBehaviour
    {
        public static DrumKitAudio Instance { get; private set; }

        [SerializeField, Range(4, 32)] int _voices = 16;
        [SerializeField] float _minDistance = 0.6f;
        [SerializeField] float _maxDistance = 12f;

        AudioSource[] _pool;
        int _next;

        void Awake()
        {
            Instance = this;
            _pool = new AudioSource[_voices];
            for (int i = 0; i < _voices; i++)
            {
                var source = new GameObject($"Voice{i}").AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.minDistance = _minDistance;
                source.maxDistance = _maxDistance;
                source.dopplerLevel = 0f;
                _pool[i] = source;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Reproduce el sample de la pieza con el banco actual. Devuelve la fuente usada o null si no hay sample.</summary>
        public AudioSource Play(DrumPiece piece, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var bank = DrumSessionSettings.Instance.CurrentBank;
            var clip = bank != null ? bank.Get(piece) : null;
            if (clip == null) return null;

            var source = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            source.transform.position = position;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.Play();
            return source;
        }
    }
}
