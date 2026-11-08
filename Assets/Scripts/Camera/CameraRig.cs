using UnityEngine;

namespace Nexus.CameraSystem
{
    /// <summary>
    /// Minimal strategy camera rig for the gameplay shell: slow orbit around a focus target.
    /// Player input (pan, zoom, edge scrolling, touch gestures) is Phase 02 scope; the rig is
    /// kept separate from the UI and from simulation so input can be added without refactoring.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private float _orbitDegreesPerSecond = 2f;
        [SerializeField] private float _distance = 16f;
        [SerializeField] private float _height = 4f;

        private Transform _focus;

        public Transform Focus
        {
            get { return _focus; }
        }

        public void Initialize(Transform focus)
        {
            _focus = focus;

            if (_focus == null)
            {
                return;
            }

            transform.position = _focus.position + new Vector3(0f, _height, -_distance);
            transform.LookAt(_focus.position);
        }

        private void LateUpdate()
        {
            if (_focus == null)
            {
                return;
            }

            transform.RotateAround(_focus.position, Vector3.up, _orbitDegreesPerSecond * Time.deltaTime);
            transform.LookAt(_focus.position);
        }
    }
}
