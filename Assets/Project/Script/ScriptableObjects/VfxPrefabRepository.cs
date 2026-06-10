using UnityEngine;

namespace Gazeus.DesafioMatch3.ScriptableObjects {
    [CreateAssetMenu(fileName = "VfxPrefabRepository", menuName = "Gameplay/VfxPrefabRepository")]
    public class VfxPrefabRepository : ScriptableObject {
        [SerializeField] private GameObject[] _vfxPrefabList;
        public GameObject[] VfxPrefabList => _vfxPrefabList;
    }
}