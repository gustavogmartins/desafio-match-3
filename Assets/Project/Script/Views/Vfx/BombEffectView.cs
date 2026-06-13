using UnityEngine;
using DG.Tweening;
using Gazeus.DesafioMatch3.Models;
using Gazeus.DesafioMatch3.ScriptableObjects;

namespace Gazeus.DesafioMatch3.Project.Views.Vfx {
    public interface IBombEffectContext {
        RectTransform BoardContainerRect { get; }
        Vector2 BoardCellSize { get; }
        bool TryGetTileTransform(Vector2Int position, out Transform tileTransform);
        bool TryGetTileWorldCenter(Vector2Int position, out Vector3 worldCenter);
        void PlayPooledVfx(int prefabIndex, Vector3 worldPosition);
    }

    public class BombEffectView : MonoBehaviour {
        private const int BombExplosionVfxIndex = 2;
        private const float DefaultBombCenterVfxDelay = 0.08f;
        private const float DefaultBombTilePulseDuration = 0.2f;
        private const float DefaultBombAffectedTilePunchDuration = 0.22f;
        private const float DefaultBombAffectedTileStagger = 0.025f;

        [SerializeField] private BombEffectParams _bombEffectParams;

        public Tween CreateBombImpactAnimation(SpecialEffectAnimationInfo specialEffect, IBombEffectContext context) {
            Sequence sequence = DOTween.Sequence();
            if (specialEffect == null || context == null ||
                !context.TryGetTileWorldCenter(specialEffect.Origin, out Vector3 originWorldCenter)) {
                return sequence;
            }

            if (context.TryGetTileTransform(specialEffect.Origin, out Transform bombTransform)) {
                sequence.Join(CreateBombTilePulse(bombTransform));
            }

            sequence.InsertCallback(BombCenterVfxDelay, () =>
                context.PlayPooledVfx(BombExplosionVfxIndex, originWorldCenter));

            sequence.Join(CreateBoardImpactShake(context.BoardContainerRect, context.BoardCellSize));
            sequence.Join(CreateAffectedTileReaction(specialEffect, context));

            return sequence;
        }

        private Tween CreateBombTilePulse(Transform bombTransform) {
            Vector3 originalScale = bombTransform.localScale;
            Quaternion originalRotation = bombTransform.localRotation;

            bombTransform.DOKill(false);

            Sequence sequence = DOTween.Sequence();
            sequence.Append(
                bombTransform
                    .DOScale(originalScale * 1.5f, BombTilePulseDuration * 0.55f)
                    .SetEase(Ease.OutBack));
            sequence.Append(
                bombTransform
                    .DOScale(originalScale, BombTilePulseDuration * 0.45f)
                    .SetEase(Ease.InOutSine));
            sequence.Join(
                bombTransform
                    .DOPunchRotation(new Vector3(0f, 0f, 26f), BombTilePulseDuration, 8, 0.65f));
            sequence.OnComplete(() => {
                bombTransform.localScale = originalScale;
                bombTransform.localRotation = originalRotation;
            });

            return sequence;
        }

        private Tween CreateBoardImpactShake(RectTransform boardContainerRect, Vector2 boardCellSize) {
            if (boardContainerRect == null) {
                return DOTween.Sequence();
            }

            Vector2 originalAnchoredPosition = boardContainerRect.anchoredPosition;
            float cellSize = Mathf.Max(boardCellSize.x, boardCellSize.y);
            float strength = Mathf.Clamp(cellSize * 0.08f, 4f, 10f);

            return boardContainerRect
                .DOShakeAnchorPos(0.22f, strength, 18, 75f, false, true)
                .OnComplete(() => boardContainerRect.anchoredPosition = originalAnchoredPosition);
        }

        private Tween CreateAffectedTileReaction(SpecialEffectAnimationInfo specialEffect, IBombEffectContext context) {
            Sequence sequence = DOTween.Sequence();
            if (specialEffect.AffectedPositions == null) {
                return sequence;
            }

            for (int i = 0; i < specialEffect.AffectedPositions.Count; i++) {
                Vector2Int position = specialEffect.AffectedPositions[i];
                if (position == specialEffect.Origin) {
                    continue;
                }

                if (!context.TryGetTileTransform(position, out Transform tileTransform)) {
                    continue;
                }

                float delay = Vector2Int.Distance(position, specialEffect.Origin) * BombAffectedTileStagger;
                sequence.Insert(
                    delay,
                    tileTransform
                        .DOPunchScale(Vector3.one * 1.18f, BombAffectedTilePunchDuration, 6, 0.6f)
                        .SetEase(Ease.OutQuad));
            }

            return sequence;
        }

        private float BombCenterVfxDelay =>
            _bombEffectParams != null ? _bombEffectParams.BombCenterVfxDelay : DefaultBombCenterVfxDelay;

        private float BombTilePulseDuration =>
            _bombEffectParams != null ? _bombEffectParams.BombTilePulseDuration : DefaultBombTilePulseDuration;

        private float BombAffectedTilePunchDuration =>
            _bombEffectParams != null
                ? _bombEffectParams.BombAffectedTilePunchDuration
                : DefaultBombAffectedTilePunchDuration;

        private float BombAffectedTileStagger =>
            _bombEffectParams != null ? _bombEffectParams.BombAffectedTileStagger : DefaultBombAffectedTileStagger;
    }
}