using System.Collections.Generic;
using System.Linq;
using Harpoon.Core;
using UnityEngine;

namespace Harpoon.Runtime
{
    public sealed class FormationView : MonoBehaviour
    {
        private LineRenderer _sensorRing;
        private bool _radiating;
        private bool _contactKnown;
        private LineRenderer _damageRing;
        private LineRenderer _tacticalRing;
        private bool _legalTarget;
        private bool _active;
        private bool _selected;
        private readonly List<Transform> _smokePuffs = new List<Transform>();
        private Renderer[] _shipRenderers;
        private Color[] _baseColors;
        private Transform[] _shipModels;
        private float _damageFraction;
        private bool _destroyed;
        private bool _hasBoardPosition;
        private bool _hasHeading;
        private bool _directionalCounter;
        private Vector3 _boardPosition;
        private Vector3 _modelForwardLocal = Vector3.forward;
        private Quaternion _targetRotation;
        public Side Side { get; private set; }
        public string FormationId { get; private set; }
        public void Initialize(Side side, string formationId = "")
        {
            Side = side;
            FormationId = formationId ?? string.Empty;
            _shipModels = transform.Cast<Transform>().Where(child =>
                child.name == "Escort" || child.name == "Amphibious Ship").ToArray();
            ConfigureModelForward();
            _shipRenderers = _shipModels.SelectMany(ship => ship.GetComponentsInChildren<Renderer>()).ToArray();
            _baseColors = _shipRenderers.Select(renderer => renderer.material.color).ToArray();
            BuildSensorRing();
            BuildDamageEffects();
            BuildTacticalRing();
        }

        public void SetBoardPosition(Vector3 position)
        {
            if (_hasBoardPosition)
            {
                var travel = position - _boardPosition;
                travel.y = 0f;
                if (_directionalCounter && travel.sqrMagnitude > 0.001f)
                {
                    var modelHeading = Quaternion.LookRotation(_modelForwardLocal, Vector3.up);
                    _targetRotation = Quaternion.LookRotation(travel.normalized, Vector3.up) *
                                      Quaternion.Inverse(modelHeading);
                    _hasHeading = true;
                }
            }
            transform.position = position;
            _boardPosition = position;
            _hasBoardPosition = true;
        }

        private void ConfigureModelForward()
        {
            var model = transform.Cast<Transform>().FirstOrDefault(child =>
                child.name == "Escort" || child.name == "Amphibious Ship" ||
                child.name == "Aircraft Carrier" || child.name == "P-8A Poseidon");
            if (model != null)
            {
                _modelForwardLocal = model.localRotation * Vector3.right;
                _modelForwardLocal.y = 0f;
                _modelForwardLocal.Normalize();
                _directionalCounter = true;
                return;
            }
            if (transform.Find("Submarine Hull") != null)
            {
                _modelForwardLocal = Vector3.right;
                _directionalCounter = true;
            }
        }

        public void SetTacticalState(bool legalTarget, bool active, bool selected)
        {
            _legalTarget = legalTarget;
            _active = active;
            _selected = selected;
            if (_tacticalRing != null) _tacticalRing.gameObject.SetActive(legalTarget || active || selected);
        }

        public void SetDamageState(float damageFraction, bool missionKilled, bool destroyed)
        {
            _damageFraction = Mathf.Clamp01(damageFraction);
            _destroyed = destroyed;
            if (_damageRing != null) _damageRing.gameObject.SetActive(_damageFraction > 0f);
            for (var index = 0; index < _smokePuffs.Count; index++)
                _smokePuffs[index].gameObject.SetActive(_damageFraction >= (index + 1) * 0.22f || destroyed);
            for (var index = 0; index < _shipRenderers.Length; index++)
            {
                var severity = destroyed ? 0.82f : missionKilled ? 0.62f : _damageFraction * 0.5f;
                _shipRenderers[index].material.color = Color.Lerp(_baseColors[index],
                    new Color(0.16f, 0.13f, 0.11f), severity);
            }
            for (var index = 0; index < _shipModels.Length; index++)
            {
                var yaw = index == 0 ? -35f : -35f;
                _shipModels[index].localRotation = Quaternion.Euler(0f, yaw,
                    destroyed ? (index == 0 ? 18f : -14f) : missionKilled ? (index == 0 ? 4f : -3f) : 0f);
                var position = _shipModels[index].localPosition;
                position.y = destroyed ? -0.22f : 0f;
                _shipModels[index].localPosition = position;
            }
        }

        public void SetSensorState(bool radiating, bool contactKnown)
        {
            _radiating = radiating;
            _contactKnown = contactKnown;
            if (_sensorRing != null) _sensorRing.gameObject.SetActive(radiating || contactKnown);
        }

        private void Update()
        {
            const int segments = 48;
            if (_hasHeading)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRotation,
                    360f * Time.deltaTime);
            if (_sensorRing != null && (_radiating || _contactKnown))
            {
                var radius = (_radiating ? 1.12f : 0.82f) +
                             (_radiating ? Mathf.Sin(Time.time * 3.2f) * 0.12f : 0f);
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
            if (_damageRing != null && _damageRing.gameObject.activeSelf)
            {
                var radius = 0.9f + Mathf.Sin(Time.time * 4.5f) * 0.06f;
                for (var index = 0; index < segments; index++)
                {
                    var angle = index * Mathf.PI * 2f / segments;
                    _damageRing.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius,
                        0.115f, Mathf.Sin(angle) * radius));
                }
                var color = _destroyed ? new Color(0.9f, 0.08f, 0.02f, 0.82f)
                    : Color.Lerp(new Color(1f, 0.65f, 0.08f, 0.64f),
                        new Color(1f, 0.12f, 0.02f, 0.82f), _damageFraction);
                _damageRing.startColor = _damageRing.endColor = color;
            }
            if (_tacticalRing != null && _tacticalRing.gameObject.activeSelf)
            {
                var radius = (_legalTarget ? 1.28f : 1.08f) + Mathf.Sin(Time.time * 5f) * 0.07f;
                for (var index = 0; index < segments; index++)
                {
                    var angle = index * Mathf.PI * 2f / segments;
                    _tacticalRing.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius,
                        0.14f, Mathf.Sin(angle) * radius));
                }
                var color = _legalTarget ? new Color(1f, 0.28f, 0.12f, 0.95f)
                    : _active ? new Color(0.18f, 1f, 0.58f, 0.82f)
                    : new Color(1f, 0.86f, 0.22f, 0.72f);
                _tacticalRing.startColor = _tacticalRing.endColor = color;
                _tacticalRing.widthMultiplier = _legalTarget ? 0.085f : 0.052f;
            }
            for (var index = 0; index < _smokePuffs.Count; index++)
            {
                var puff = _smokePuffs[index];
                if (!puff.gameObject.activeSelf) continue;
                var cycle = Mathf.Repeat(Time.time * (0.42f + index * 0.05f) + index * 0.31f, 1f);
                puff.localPosition = new Vector3((index - 1) * 0.2f,
                    0.55f + cycle * 1.05f, (index % 2 == 0 ? -0.12f : 0.18f));
                puff.localScale = Vector3.one * (0.13f + cycle * (0.34f + _damageFraction * 0.18f));
            }
        }

        private void BuildDamageEffects()
        {
            var ring = new GameObject("Damage State Ring");
            ring.transform.SetParent(transform, false);
            _damageRing = ring.AddComponent<LineRenderer>();
            _damageRing.useWorldSpace = false;
            _damageRing.loop = true;
            _damageRing.positionCount = 48;
            _damageRing.widthMultiplier = 0.055f;
            _damageRing.numCornerVertices = 2;
            _damageRing.material = new Material(Shader.Find("Sprites/Default"));
            ring.SetActive(false);
            for (var index = 0; index < 3; index++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Damage Smoke " + (index + 1);
                puff.transform.SetParent(transform, false);
                var collider = puff.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                puff.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(
                    new Color(0.18f, 0.16f, 0.14f, 0.78f), 0f, 0.05f);
                puff.SetActive(false);
                _smokePuffs.Add(puff.transform);
            }
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

        private void BuildTacticalRing()
        {
            var ring = new GameObject("Legal Target / Selection Ring");
            ring.transform.SetParent(transform, false);
            _tacticalRing = ring.AddComponent<LineRenderer>();
            _tacticalRing.useWorldSpace = false;
            _tacticalRing.loop = true;
            _tacticalRing.positionCount = 48;
            _tacticalRing.numCornerVertices = 3;
            _tacticalRing.material = new Material(Shader.Find("Sprites/Default"));
            ring.SetActive(false);
        }
    }
}
