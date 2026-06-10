using System;
using System.Collections;
using UnityEngine;

namespace Gazeus.DesafioMatch3.Views {
    public class PooledParticleVfx : MonoBehaviour {
        private ParticleSystem[] _particleSystems;
        private Action<GameObject> _release;
        private Coroutine _releaseRoutine;

        private void Awake() {
            CacheParticleSystems();
        }

        public void Initialize(Action<GameObject> release) {
            _release = release;
        }

        public void PlayAt(Vector3 worldPosition) {
            CacheParticleSystems();

            transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            for (int i = 0; i < _particleSystems.Length; i++) {
                _particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _particleSystems[i].Play(true);
            }

            if (_releaseRoutine != null) {
                StopCoroutine(_releaseRoutine);
            }

            _releaseRoutine = StartCoroutine(ReleaseWhenFinished());
        }

        public void StopAndClear() {
            if (_releaseRoutine != null) {
                StopCoroutine(_releaseRoutine);
                _releaseRoutine = null;
            }

            CacheParticleSystems();

            for (int i = 0; i < _particleSystems.Length; i++) {
                _particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private IEnumerator ReleaseWhenFinished() {
            yield return null;

            while (IsAlive()) {
                yield return null;
            }

            _releaseRoutine = null;
            _release?.Invoke(gameObject);
        }

        private bool IsAlive() {
            for (int i = 0; i < _particleSystems.Length; i++) {
                if (_particleSystems[i].IsAlive(true)) {
                    return true;
                }
            }

            return false;
        }

        private void CacheParticleSystems() {
            if (_particleSystems != null && _particleSystems.Length > 0) {
                return;
            }

            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }
}
