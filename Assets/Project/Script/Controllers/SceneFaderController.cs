using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gazeus.DesafioMatch3 {
    public class SceneFaderController : MonoBehaviour {
        [SerializeField] private float _fadeDuration = 0.35f;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private string _nextSceneName;
        private bool _isTransitioning;
        
        public void StartFadeOut() {
            _canvasGroup.DOFade(1, _fadeDuration).SetEase(Ease.Linear).onComplete += () => {
                SceneManager.LoadScene(_nextSceneName);
            };
        }
        
        public void StartFadeIn() {
            _canvasGroup.DOFade(0, _fadeDuration).SetEase(Ease.Linear);
        }
    }
}