using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Reproduce la retroalimentación sonora del menú desde la posición del panel.
    /// Todos los sonidos pasan por AudioListener.volume, así el golpe de prueba ya suena con el volumen elegido.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuAudio : MonoBehaviour
    {
        [Header("Samples reales (opcional, se conectan en el Entregable 2)")]
        [SerializeField] AudioClip _acousticPreview;
        [SerializeField] AudioClip _electronicPreview;

        [Header("Niveles")]
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

        /// <summary>Golpe corto de caja con el banco del kit elegido.</summary>
        public void PlayKitPreview(DrumKitType kit)
        {
            AudioClip clip = kit == DrumKitType.Acoustic
                ? (_acousticPreview != null ? _acousticPreview : ProceduralDrumSounds.AcousticSnare)
                : (_electronicPreview != null ? _electronicPreview : ProceduralDrumSounds.ElectronicSnare);
            _source.PlayOneShot(clip, _previewVolume);
        }
    }
}
