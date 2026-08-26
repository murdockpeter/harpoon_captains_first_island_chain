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
                    Color.Lerp(sideColor, Color.gray, i * 0.08f), amphibious);
            }
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Side Indicator";
            ring.transform.SetParent(root);
            ring.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            ring.transform.localScale = new Vector3(0.92f, 0.025f, 0.92f);
            ring.GetComponent<Renderer>().sharedMaterial = Material(sideColor, 0f, 0.65f);
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
            Color color, bool amphibious)
        {
            var ship = new GameObject(amphibious ? "Amphibious Ship" : "Escort").transform;
            ship.SetParent(parent);
            ship.localPosition = localPosition;
            ship.localRotation = Quaternion.Euler(0f, -35f, 0f);
            ship.localScale = Vector3.one * scale;

            var hull = new GameObject("Hull");
            hull.transform.SetParent(ship);
            hull.transform.localPosition = Vector3.zero;
            var mesh = BuildHullMesh(amphibious ? 1.45f : 1.15f, amphibious ? 0.42f : 0.3f);
            hull.AddComponent<MeshFilter>().sharedMesh = mesh;
            hull.AddComponent<MeshRenderer>().sharedMaterial = Material(color, 0.55f, 0.35f);

            AddBox(ship, "Deck", new Vector3(-0.1f, 0.22f, 0f),
                new Vector3(amphibious ? 1.45f : 1.05f, 0.08f, amphibious ? 0.65f : 0.42f),
                Color.Lerp(color, Color.white, 0.16f));
            AddBox(ship, "Superstructure", new Vector3(amphibious ? -0.32f : -0.12f, 0.38f, 0f),
                new Vector3(amphibious ? 0.5f : 0.35f, amphibious ? 0.3f : 0.26f, amphibious ? 0.52f : 0.32f),
                new Color(0.54f, 0.58f, 0.6f));
            AddBox(ship, "Bridge", new Vector3(amphibious ? -0.38f : 0.05f, 0.57f, 0f),
                new Vector3(0.24f, 0.14f, amphibious ? 0.42f : 0.26f), new Color(0.68f, 0.72f, 0.73f));

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
