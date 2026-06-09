using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gazeus.DesafioMatch3.Controllers {
    [RequireComponent(typeof(Button))]
    public class HomeSceneTransitionController : MonoBehaviour {
        [SerializeField] private string _gameplaySceneName = "Gameplay";
        [SerializeField] private float _fadeDuration = 0.35f;

        private Button _playButton;
        private bool _isTransitioning;

        #region Unity

        private void Awake() {
            _playButton = GetComponent<Button>();
            _playButton.onClick.AddListener(Play);
        }

        private void OnDestroy() {
            if (_playButton != null) {
                _playButton.onClick.RemoveListener(Play);
            }
        }

        #endregion

        private void Play() {
            if (_isTransitioning) return;

            StartCoroutine(TransitionToGameplay());
        }

        private IEnumerator TransitionToGameplay() {
            _isTransitioning = true;
            _playButton.interactable = false;

            Image fadeOverlay = CreateFadeOverlay();
            if (fadeOverlay == null) {
                SceneManager.LoadScene(_gameplaySceneName);
                yield break;
            }

            Color color = fadeOverlay.color;
            float elapsed = 0f;

            while (elapsed < _fadeDuration) {
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Clamp01(elapsed / _fadeDuration);
                fadeOverlay.color = color;
                yield return null;
            }

            SceneManager.LoadScene(_gameplaySceneName);
        }

        private Image CreateFadeOverlay() {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            GameObject overlay = new GameObject("SceneTransitionFade", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            overlay.transform.SetParent(canvas.transform, false);
            overlay.transform.SetAsLastSibling();

            RectTransform rectTransform = overlay.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = overlay.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            return image;
        }
    }
}