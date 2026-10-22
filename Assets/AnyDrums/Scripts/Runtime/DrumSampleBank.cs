using UnityEngine;

namespace AnyDrums
{
    /// <summary>
    /// Banco de samples de un kit (RF1). Cada modalidad (acústica y electrónica) tiene el suyo;
    /// para cambiar un sonido basta con arrastrar otro AudioClip al campo correspondiente.
    /// </summary>
    [CreateAssetMenu(menuName = "AnyDrums/Banco de samples", fileName = "DrumSampleBank")]
    public class DrumSampleBank : ScriptableObject
    {
        [SerializeField] DrumKitType _kit;
        [SerializeField] string _displayName = "Batería";

        [Header("Samples")]
        [SerializeField] AudioClip _kick;
        [SerializeField] AudioClip _snare;
        [SerializeField] AudioClip _hiHatClosed;
        [SerializeField] AudioClip _hiHatOpen;
        [SerializeField] AudioClip _hiHatPedal;
        [SerializeField] AudioClip _tom1;
        [SerializeField] AudioClip _tom2;
        [SerializeField] AudioClip _floorTom;
        [SerializeField] AudioClip _crash;
        [SerializeField] AudioClip _ride;

        public DrumKitType Kit => _kit;
        public string DisplayName => _displayName;

        public AudioClip Get(DrumPiece piece) => piece switch
        {
            DrumPiece.Kick => _kick,
            DrumPiece.Snare => _snare,
            DrumPiece.HiHatClosed => _hiHatClosed,
            DrumPiece.HiHatOpen => _hiHatOpen,
            DrumPiece.HiHatPedal => _hiHatPedal,
            DrumPiece.Tom1 => _tom1,
            DrumPiece.Tom2 => _tom2,
            DrumPiece.FloorTom => _floorTom,
            DrumPiece.Crash => _crash,
            DrumPiece.Ride => _ride,
            _ => null
        };

        /// <summary>Carga en memoria todos los samples del banco para que el primer golpe no tenga retardo.</summary>
        public void Preload()
        {
            foreach (DrumPiece piece in System.Enum.GetValues(typeof(DrumPiece)))
            {
                var clip = Get(piece);
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }
    }
}
