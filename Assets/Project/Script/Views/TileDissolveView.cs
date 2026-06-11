using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Gazeus.DesafioMatch3.Views {
    public class TileDissolveView : MonoBehaviour {
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

        [SerializeField] private Image _image;

        private Material _runtimeMaterial;
        private Tween _dissolveTween;
        private bool _materialChecked;
        private bool _supportsDissolve;

        private void Awake() {
            EnsureRuntimeMaterial();
            ResetDissolve();
        }

        private void OnDestroy() {
            KillDissolveTween();

            if (_runtimeMaterial == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(_runtimeMaterial);
            } else {
                DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
        }

        public Tween PlayDissolve(float duration) {
            EnsureRuntimeMaterial();
            if (!_supportsDissolve) {
                return null;
            }

            KillDissolveTween();
            SetDissolveAmount(0f);

            _dissolveTween = DOVirtual.Float(0f, 1f, duration, SetDissolveAmount)
                .SetTarget(this);

            return _dissolveTween;
        }

        public void ResetDissolve() {
            EnsureRuntimeMaterial();
            KillDissolveTween();
            SetDissolveAmount(0f);
        }

        private void EnsureRuntimeMaterial() {
            if (_materialChecked) {
                return;
            }

            _materialChecked = true;

            if (_image == null) {
                return;
            }

            Material sourceMaterial = _image.material;
            if (sourceMaterial == null || !sourceMaterial.HasProperty(DissolveAmountId)) {
                return;
            }

            _runtimeMaterial = Instantiate(sourceMaterial);
            _runtimeMaterial.name = $"{sourceMaterial.name} (Runtime)";
            _image.material = _runtimeMaterial;
            _supportsDissolve = true;
        }

        private void KillDissolveTween() {
            if (_dissolveTween == null) {
                return;
            }

            _dissolveTween.Kill();
            _dissolveTween = null;
        }

        private void SetDissolveAmount(float amount) {
            if (_runtimeMaterial == null) {
                return;
            }

            _runtimeMaterial.SetFloat(DissolveAmountId, amount);
        }
    }
}
