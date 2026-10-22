using UnityEngine;

namespace AnyDrums
{
    /// <summary>
    /// Relaciona cada modalidad de batería con su banco de samples y define la escena del estudio.
    /// Vive en Resources para que cualquier escena pueda encontrarlo.
    /// </summary>
    [CreateAssetMenu(menuName = "AnyDrums/Catálogo de kits", fileName = "DrumKitCatalog")]
    public class DrumKitCatalog : ScriptableObject
    {
        public const string ResourcePath = "AnyDrums/DrumKitCatalog";

        [SerializeField] DrumSampleBank _acousticBank;
        [SerializeField] DrumSampleBank _electronicBank;
        [SerializeField] string _menuScene = "MainMenu";
        [SerializeField] string _studioScene = "Studio";

        static DrumKitCatalog _cached;

        public string MenuScene => _menuScene;
        public string StudioScene => _studioScene;

        public static DrumKitCatalog Load()
        {
            if (_cached == null) _cached = Resources.Load<DrumKitCatalog>(ResourcePath);
            return _cached;
        }

        public DrumSampleBank GetBank(DrumKitType kit) => kit == DrumKitType.Acoustic ? _acousticBank : _electronicBank;
    }
}
