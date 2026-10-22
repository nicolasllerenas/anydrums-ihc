using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Reproduce la retroalimentación sonora de los paneles desde su posición.
    /// El golpe de prueba usa la caja del banco de samples del kit (RF1); todos los sonidos pasan por
    /// AudioListener.volume, así el golpe ya suena con el volumen elegido (RF4).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float _clickVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float _hoverVolume = 0.18f;
        [SerializeField, Range(0f, 1f)] float _previewVolume = 1f;

        AudioSource _source;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        public void PlayHover() => _source.PlayOneShot(ProceduralDrumSounds.Hover, _hoverVolume);

        public void PlayClick() => _source.PlayOneShot(ProceduralDrumSounds.Click, _clickVolume);

        /// <summary>Golpe corto de caja con el banco de samples del kit indicado.</summary>
        public void PlayKitPreview(DrumKitType kit)
        {
            var catalog = DrumKitCatalog.Load();
            var bank = catalog != null ? catalog.GetBank(kit) : null;
            AudioClip clip = bank != null ? bank.Get(DrumPiece.Snare) : null;
            if (clip == null)
                clip = kit == DrumKitType.Acoustic ? ProceduralDrumSounds.AcousticSnare : ProceduralDrumSounds.ElectronicSnare;
            _source.PlayOneShot(clip, _previewVolume);
        }
    }
}
