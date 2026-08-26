using Harpoon.Core;
using UnityEngine;

namespace Harpoon.Runtime
{
    public sealed class FormationView : MonoBehaviour
    {
        private LineRenderer _sensorRing;
        private bool _radiating;
        private bool _contactKnown;
        public Side Side { get; private set; }
        public string FormationId { get; private set; }
        public void Initialize(Side side, string formationId = "")
        {
            Side = side;
            FormationId = formationId ?? string.Empty;
            BuildSensorRing();
        }

        public void SetSensorState(bool radiating, bool contactKnown)
        {
            _radiating = radiating;
            _contactKnown = contactKnown;
            if (_sensorRing != null) _sensorRing.gameObject.SetActive(radiating || contactKnown);
        }

        private void Update()
        {
            if (_sensorRing == null || (!_radiating && !_contactKnown)) return;
            var radius = (_radiating ? 1.12f : 0.82f) +
                         (_radiating ? Mathf.Sin(Time.time * 3.2f) * 0.12f : 0f);
            const int segments = 48;
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                _sensorRing.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius,
                    0.08f, Mathf.Sin(angle) * radius));
            }
            _sensorRing.startColor = _sensorRing.endColor = _radiating
                ? new Color(0.15f, 0.95f, 1f, 0.78f)
                : new Color(1f, 0.82f, 0.18f, 0.62f);
        }

        private void BuildSensorRing()
        {
            var ring = new GameObject("Sensor Contact Ring");
            ring.transform.SetParent(transform, false);
            _sensorRing = ring.AddComponent<LineRenderer>();
            _sensorRing.useWorldSpace = false;
            _sensorRing.loop = true;
            _sensorRing.positionCount = 48;
            _sensorRing.widthMultiplier = 0.035f;
            _sensorRing.numCornerVertices = 2;
            _sensorRing.material = new Material(Shader.Find("Sprites/Default"));
            ring.SetActive(false);
        }
    }
}
