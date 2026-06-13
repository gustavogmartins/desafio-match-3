using UnityEngine;

namespace Gazeus.DesafioMatch3.ScriptableObjects {
    
    [CreateAssetMenu(fileName = "BombEffectParams", menuName = "Gameplay/BombEffectParams")]
    public class BombEffectParams : ScriptableObject {
        
        [SerializeField] private float _bombCenterVfxDelay = 0.08f;
        [SerializeField] private float _bombTilePulseDuration = 0.2f;
        [SerializeField] private float _bombAffectedTilePunchDuration = 0.22f;
        [SerializeField] private float _bombAffectedTileStagger = 0.025f;

        public float BombCenterVfxDelay => _bombCenterVfxDelay;
        public float BombTilePulseDuration => _bombTilePulseDuration;
        public float BombAffectedTilePunchDuration => _bombAffectedTilePunchDuration;
        public float BombAffectedTileStagger => _bombAffectedTileStagger;
        
    }
    
}
