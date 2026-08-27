using UnityEngine;

namespace Harpoon.Runtime
{
    public static class VisualFactory
    {
        private static Material _baseMaterial;

        public static Material Material(Color color, float metallic = 0f, float smoothness = 0.35f)
        {
            if (_baseMaterial == null)
            {
                _baseMaterial = Resources.Load<Material>("OperationalMaterial");
                if (_baseMaterial == null) _baseMaterial = new Material(Shader.Find("Standard"));
            }
            var material = new Material(_baseMaterial) { color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        public static Transform CreateFormation(string name, Color sideColor, bool amphibiousFormation,
            int shipCount = 2)
        {
            var root = new GameObject(name).transform;
            shipCount = Mathf.Clamp(shipCount, 1, 4);
            var positions = shipCount == 1
                ? new[] { Vector3.zero }
                : shipCount == 2
                    ? new[] { new Vector3(-0.15f, 0f, -0.38f), new Vector3(0.2f, 0f, 0.42f) }
                    : new[]
                    {
                        new Vector3(-0.42f, 0f, -0.34f), new Vector3(0.38f, 0f, -0.28f),
                        new Vector3(-0.28f, 0f, 0.42f), new Vector3(0.48f, 0f, 0.38f)
                    };
            var scale = shipCount >= 3 ? 0.62f : shipCount == 2 ? 0.78f : 1.05f;
            for (var i = 0; i < shipCount; i++)
            {
                var amphibious = amphibiousFormation && i == shipCount - 1;
                CreateShip(root, positions[i], amphibious ? scale * 1.1f : scale,
                    Color.Lerp(sideColor, Color.gray, i * 0.08f), amphibious, false);
            }
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Side Indicator";
            ring.transform.SetParent(root);
            ring.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            ring.transform.localScale = new Vector3(0.92f, 0.025f, 0.92f);
            ring.GetComponent<Renderer>().sharedMaterial = Material(sideColor, 0f, 0.65f);
            return root;
        }

        public static Transform CreateCarrier(string name, Color sideColor)
        {
            var root = new GameObject(name).transform;
            CreateShip(root, Vector3.zero, 1.12f, sideColor, false, true);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Carrier Indicator";
            ring.transform.SetParent(root);
            ring.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            ring.transform.localScale = new Vector3(1.05f, 0.025f, 1.05f);
            ring.GetComponent<Renderer>().sharedMaterial = Material(sideColor, 0f, 0.7f);
            return root;
        }

        public static Transform CreateContactMarker(string name, Color sideColor)
        {
            var root = new GameObject(name).transform;
            var buoy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            buoy.name = "Unresolved Contact";
            buoy.transform.SetParent(root);
            buoy.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            buoy.transform.localScale = new Vector3(0.18f, 0.42f, 0.18f);
            buoy.GetComponent<Renderer>().sharedMaterial = Material(
                Color.Lerp(sideColor, new Color(0.78f, 0.28f, 0.95f), 0.48f), 0.42f, 0.72f);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Contact Beacon";
            beacon.transform.SetParent(root);
            beacon.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            beacon.transform.localScale = Vector3.one * 0.34f;
            beacon.GetComponent<Renderer>().sharedMaterial = Material(
                Color.Lerp(sideColor, Color.white, 0.35f), 0.18f, 0.85f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Contact Ring";
            ring.transform.SetParent(root);
            ring.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            ring.transform.localScale = new Vector3(0.72f, 0.025f, 0.72f);
            ring.GetComponent<Renderer>().sharedMaterial = Material(sideColor, 0f, 0.7f);
            return root;
        }

        public static Transform CreateSubmarine(string name, Color sideColor)
        {
            var root = new GameObject(name).transform;
            var hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hull.name = "Submarine Hull";
            hull.transform.SetParent(root);
            hull.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            hull.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hull.transform.localScale = new Vector3(0.26f, 0.82f, 0.3f);
            hull.GetComponent<Renderer>().sharedMaterial = Material(
                Color.Lerp(sideColor, new Color(0.08f, 0.14f, 0.19f), 0.62f), 0.68f, 0.32f);

            AddBox(root, "Sail", new Vector3(0f, 0.25f, 0f),
                new Vector3(0.28f, 0.24f, 0.18f), Color.Lerp(sideColor, Color.black, 0.52f));
            AddBox(root, "Dive Plane", new Vector3(0f, 0.18f, 0f),
                new Vector3(0.12f, 0.035f, 0.76f), Color.Lerp(sideColor, Color.black, 0.58f));
            AddBox(root, "Wake", new Vector3(-0.82f, -0.2f, 0f),
                new Vector3(0.75f, 0.018f, 0.09f), new Color(0.48f, 0.8f, 0.94f, 0.55f));

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Submarine Indicator";
            ring.transform.SetParent(root);
            ring.transform.localPosition = new Vector3(0f, -0.36f, 0f);
            ring.transform.localScale = new Vector3(0.82f, 0.025f, 0.82f);
            ring.GetComponent<Renderer>().sharedMaterial = Material(sideColor, 0f, 0.68f);
            return root;
        }

        public static Transform CreatePatrolAircraft(string name, Color sideColor)
        {
            var root = new GameObject(name).transform;
            var aircraft = new GameObject("P-8A Poseidon").transform;
            aircraft.SetParent(root);
            aircraft.localRotation = Quaternion.Euler(0f, -35f, 0f);

            var airframe = Color.Lerp(Color.white, sideColor, 0.18f);
            var fuselage = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fuselage.name = "Fuselage";
            fuselage.transform.SetParent(aircraft);
            fuselage.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            fuselage.transform.localScale = new Vector3(0.18f, 0.72f, 0.18f);
            fuselage.GetComponent<Renderer>().sharedMaterial = Material(airframe, 0.28f, 0.62f);
            AddBox(aircraft, "Main Wings", new Vector3(0f, 0f, 0f),
                new Vector3(0.42f, 0.035f, 1.45f), airframe);
            AddBox(aircraft, "Tailplane", new Vector3(-0.58f, 0.03f, 0f),
                new Vector3(0.25f, 0.035f, 0.64f), airframe);
            AddBox(aircraft, "Vertical Tail", new Vector3(-0.62f, 0.19f, 0f),
                new Vector3(0.26f, 0.36f, 0.045f), Color.Lerp(airframe, sideColor, 0.35f));
            for (var engine = -1; engine <= 1; engine += 2)
            {
                var nacelle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                nacelle.name = engine < 0 ? "Port Engines" : "Starboard Engines";
                nacelle.transform.SetParent(aircraft);
                nacelle.transform.localPosition = new Vector3(0.06f, -0.04f, engine * 0.43f);
                nacelle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                nacelle.transform.localScale = new Vector3(0.1f, 0.28f, 0.1f);
                nacelle.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.3f, 0.34f, 0.38f), 0.65f, 0.5f);
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Aviation Patrol Indicator";
            ring.transform.SetParent(root);
            ring.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            ring.transform.localScale = new Vector3(0.92f, 0.025f, 0.92f);
            ring.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.08f, 0.86f, 1f), 0f, 0.82f);
            return root;
        }

        public static GameObject CreateMissile(Color color)
        {
            var missile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            missile.name = "Missile Salvo";
            missile.transform.localScale = Vector3.one * 0.17f;
            missile.GetComponent<Renderer>().sharedMaterial = Material(color, 0.15f, 0.8f);
            var trail = missile.AddComponent<TrailRenderer>();
            trail.time = 0.7f;
            trail.startWidth = 0.09f;
            trail.endWidth = 0.01f;
            trail.material = Material(new Color(1f, 0.62f, 0.18f), 0f, 0f);
            trail.startColor = new Color(1f, 0.8f, 0.35f, 1f);
            trail.endColor = new Color(1f, 0.15f, 0.02f, 0f);
            return missile;
        }

        private static void CreateShip(Transform parent, Vector3 localPosition, float scale,
            Color color, bool amphibious, bool carrier)
        {
            var ship = new GameObject(carrier ? "Aircraft Carrier" : amphibious ? "Amphibious Ship" : "Escort").transform;
            ship.SetParent(parent);
            ship.localPosition = localPosition;
            ship.localRotation = Quaternion.Euler(0f, -35f, 0f);
            ship.localScale = Vector3.one * scale;

            var hull = new GameObject("Hull");
            hull.transform.SetParent(ship);
            hull.transform.localPosition = Vector3.zero;
            var mesh = BuildHullMesh(carrier ? 1.75f : amphibious ? 1.45f : 1.15f,
                carrier ? 0.5f : amphibious ? 0.42f : 0.3f);
            hull.AddComponent<MeshFilter>().sharedMesh = mesh;
            hull.AddComponent<MeshRenderer>().sharedMaterial = Material(color, 0.55f, 0.35f);

            AddBox(ship, carrier ? "Flight Deck" : "Deck", new Vector3(-0.1f, 0.22f, 0f),
                new Vector3(carrier ? 1.75f : amphibious ? 1.45f : 1.05f, 0.08f,
                    carrier ? 0.82f : amphibious ? 0.65f : 0.42f),
                Color.Lerp(color, Color.white, 0.16f));
            AddBox(ship, carrier ? "Island" : "Superstructure",
                new Vector3(carrier ? -0.28f : amphibious ? -0.32f : -0.12f, 0.38f, carrier ? -0.28f : 0f),
                new Vector3(carrier ? 0.36f : amphibious ? 0.5f : 0.35f,
                    carrier ? 0.38f : amphibious ? 0.3f : 0.26f,
                    carrier ? 0.22f : amphibious ? 0.52f : 0.32f),
                new Color(0.54f, 0.58f, 0.6f));
            AddBox(ship, "Bridge", new Vector3(carrier ? -0.25f : amphibious ? -0.38f : 0.05f,
                    0.57f, carrier ? -0.28f : 0f),
                new Vector3(0.24f, 0.14f, carrier ? 0.2f : amphibious ? 0.42f : 0.26f),
                new Color(0.68f, 0.72f, 0.73f));
            if (carrier)
            {
                AddBox(ship, "Runway Stripe", new Vector3(0.12f, 0.275f, 0.08f),
                    new Vector3(1.35f, 0.012f, 0.035f), new Color(0.92f, 0.92f, 0.78f));
                AddBox(ship, "Deck Aircraft", new Vector3(0.28f, 0.31f, -0.2f),
                    new Vector3(0.18f, 0.035f, 0.16f), new Color(0.82f, 0.84f, 0.86f));
            }

            AddBox(ship, "Wake Port", new Vector3(-0.78f, -0.12f, 0.12f),
                new Vector3(0.72f, 0.015f, 0.055f), new Color(0.62f, 0.86f, 0.96f, 0.72f));
            AddBox(ship, "Wake Starboard", new Vector3(-0.78f, -0.12f, -0.12f),
                new Vector3(0.72f, 0.015f, 0.055f), new Color(0.62f, 0.86f, 0.96f, 0.72f));

            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Mast";
            mast.transform.SetParent(ship);
            mast.transform.localPosition = new Vector3(-0.08f, 0.78f, 0f);
            mast.transform.localScale = new Vector3(0.035f, 0.28f, 0.035f);
            mast.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.34f, 0.37f, 0.39f), 0.7f, 0.3f);
        }

        private static void AddBox(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = Material(color, 0.35f, 0.3f);
        }

        private static Mesh BuildHullMesh(float length, float width)
        {
            var halfLength = length * 0.5f;
            var vertices = new[]
            {
                new Vector3(halfLength, 0.15f, 0f),
                new Vector3(halfLength * 0.45f, 0.15f, width),
                new Vector3(-halfLength, 0.15f, width * 0.85f),
                new Vector3(-halfLength, 0.15f, -width * 0.85f),
                new Vector3(halfLength * 0.45f, 0.15f, -width),
                new Vector3(halfLength * 0.35f, -0.15f, 0f),
                new Vector3(-halfLength * 0.85f, -0.15f, width * 0.42f),
                new Vector3(-halfLength * 0.85f, -0.15f, -width * 0.42f)
            };
            var triangles = new[]
            {
                0,2,1, 0,3,2, 0,4,3,
                0,1,5, 1,6,5, 1,2,6,
                2,3,7, 2,7,6, 3,4,5, 3,5,7,
                5,6,7
            };
            var mesh = new Mesh { name = "Low Poly Ship Hull", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
