using UnityEngine;

namespace Harpoon.Runtime
{
    public sealed class TacticalCamera : MonoBehaviour
    {
        [SerializeField] private float panSpeed = 14f;
        [SerializeField] private float zoomSpeed = 4f;
        private Camera _camera;
        private Vector3 _focus;
        private float _distance = 32f;
        private float _yaw;
        private float _pitch = 55f;

        private void Awake() => _camera = GetComponent<Camera>();

        public void Initialize(Vector3 focus)
        {
            _focus = focus;
            ApplyTransform();
        }

        private void Update()
        {
            var horizontal = Input.GetAxisRaw("Horizontal");
            var vertical = Input.GetAxisRaw("Vertical");
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            _focus += (right * horizontal + forward * vertical).normalized * (panSpeed * Time.deltaTime);

            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * 2.5f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 2f, 30f, 78f);
            }
            var rotate = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            _yaw += rotate * 35f * Time.deltaTime;
            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _distance = Mathf.Clamp(_distance - scroll * zoomSpeed, 12f, 55f);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance, rotation);
        }
    }
}
