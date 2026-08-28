using UnityEngine;
using System.Collections.Generic;

namespace Harpoon.Runtime
{
    public static class VisualFactory
    {
        private static Material _baseMaterial;
        private static readonly Dictionary<int, Texture2D> LandTextures = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Texture2D> SeaTextures = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Material> LandAccentMaterials = new Dictionary<int, Material>();

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

        public static Material TerrainMaterial(Color tint, bool land, int variant)
        {
            variant = Mathf.Abs(variant) % 6;
            var material = Material(tint, land ? 0.01f : 0.14f, land ? 0.16f : 0.72f);
            // Every tile samples the same atlas through world-derived UVs. This removes visible
            // per-hex texture changes while BaseColor supplies broad geographic variation.
            material.mainTexture = TerrainTexture(land, 0);
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            return material;
        }

        private static Texture2D TerrainTexture(bool land, int variant)
        {
            var cache = land ? LandTextures : SeaTextures;
            if (cache.TryGetValue(variant, out var existing)) return existing;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = land ? $"Procedural Land {variant}" : $"Procedural Sea {variant}",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;
                var grain = Hash01(x, y, variant) - 0.5f;
                if (land)
                {
                    var broad = Mathf.Sin((u * 2.3f + variant * 0.19f) * Mathf.PI * 2f) *
                                Mathf.Cos((v * 2.7f - variant * 0.13f) * Mathf.PI * 2f);
                    var fine = Mathf.Sin((u * 13.7f - v * 9.2f + variant * 0.47f) * Mathf.PI * 2f) * 0.035f;
                    var mottled = broad * 0.1f + fine + grain * 0.085f;
                    var dryPatch = Mathf.Max(0f, Mathf.Sin((u * 5.1f + v * 4.4f + variant) *
                        Mathf.PI * 2f) - 0.72f) * 0.09f;
                    pixels[y * size + x] = new Color(0.9f + mottled * 0.4f + dryPatch,
                        0.93f + mottled - dryPatch * 0.5f, 0.72f + mottled * 0.3f, 1f);
                }
                else
                {
                    var swell = Mathf.Sin((u * 4.2f + v * 1.35f + variant * 0.31f) * Mathf.PI * 2f);
                    var cross = Mathf.Sin((u * 9f - v * 3.4f + variant) * Mathf.PI * 2f);
                    var ripples = Mathf.Sin((u * 22f + v * 7.5f - variant * 0.21f) * Mathf.PI * 2f);
                    var crest = Mathf.Max(0f, swell * 0.55f + cross * 0.27f + ripples * 0.18f - 0.62f);
                    var depth = Mathf.Sin((u * 1.5f - v * 1.8f + variant * 0.11f) * Mathf.PI * 2f) * 0.025f;
                    pixels[y * size + x] = new Color(0.72f + grain * 0.028f - depth,
                        0.87f + crest * 0.17f + depth, 0.98f + crest * 0.2f + depth, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            cache[variant] = texture;
            return texture;
        }

        public static void AddLandRelief(Transform tile, float surfaceHeight, int variant)
        {
            if ((variant & 1) != 0) return;
            var count = variant % 3 == 0 ? 3 : 2;
            for (var index = 0; index < count; index++)
            {
                var relief = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                relief.name = index == 0 ? "Vegetation Patch" : "Rocky Terrain";
                relief.transform.SetParent(tile, false);
                var angle = (variant * 1.7f + index * 2.35f) * Mathf.Rad2Deg;
                var radius = 0.26f + index * 0.15f;
                relief.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    surfaceHeight + 0.018f + index * 0.006f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
                relief.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                relief.transform.localScale = new Vector3(0.15f + index * 0.035f,
                    0.018f + index * 0.006f, 0.1f + index * 0.025f);
                var collider = relief.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                relief.GetComponent<Renderer>().sharedMaterial = LandAccentMaterial(variant + index);
            }
        }

        private static Material LandAccentMaterial(int variant)
        {
            variant = Mathf.Abs(variant) % 8;
            if (LandAccentMaterials.TryGetValue(variant, out var existing)) return existing;
            var color = variant % 3 == 0
                ? new Color(0.19f, 0.29f, 0.1f)
                : variant % 3 == 1 ? new Color(0.34f, 0.3f, 0.16f)
                : new Color(0.25f, 0.35f, 0.13f);
            var material = Material(color, 0f, 0.08f);
            LandAccentMaterials[variant] = material;
            return material;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                var hash = x * 374761393 + y * 668265263 + seed * 1442695041;
                hash = (hash ^ (hash >> 13)) * 1274126177;
                hash ^= hash >> 16;
                return (hash & 0x7fffffff) / (float)int.MaxValue;
            }
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

            var airframe = Color.Lerp(new Color(0.88f, 0.91f, 0.93f), sideColor, 0.12f);
            var underside = Color.Lerp(airframe, new Color(0.42f, 0.48f, 0.53f), 0.2f);
            var trim = Color.Lerp(sideColor, new Color(0.1f, 0.2f, 0.31f), 0.34f);
            var fuselage = new GameObject("Tapered Airliner Fuselage");
            fuselage.name = "Tapered Airliner Fuselage";
            fuselage.transform.SetParent(aircraft);
            fuselage.transform.localPosition = new Vector3(-0.02f, 0.145f, 0f);
            fuselage.AddComponent<MeshFilter>().sharedMesh = BuildTaperedFuselageMesh(
                "P-8A Continuous Fuselage", 1.74f, 0.135f, 16, 0.72f);
            fuselage.AddComponent<MeshRenderer>().sharedMaterial = Material(airframe, 0.28f, 0.62f);

            AddAircraftPlanform(aircraft, "Swept Main Wings", new Vector3(0.02f, 0.075f, 0f),
                new[]
                {
                    new Vector2(0.27f, 0.085f), new Vector2(-0.08f, 0.75f),
                    new Vector2(-0.23f, 0.72f), new Vector2(-0.28f, 0.11f),
                    new Vector2(-0.28f, -0.11f), new Vector2(-0.23f, -0.72f),
                    new Vector2(-0.08f, -0.75f), new Vector2(0.27f, -0.085f)
                }, 0.032f, underside);
            AddAircraftPlanform(aircraft, "Swept Tailplanes", new Vector3(-0.66f, 0.15f, 0f),
                new[]
                {
                    new Vector2(0.12f, 0.05f), new Vector2(-0.06f, 0.32f),
                    new Vector2(-0.18f, 0.3f), new Vector2(-0.15f, 0.045f),
                    new Vector2(-0.15f, -0.045f), new Vector2(-0.18f, -0.3f),
                    new Vector2(-0.06f, -0.32f), new Vector2(0.12f, -0.05f)
                }, 0.026f, airframe);
            AddVerticalFin(aircraft, "Swept Vertical Stabilizer", new Vector3(-0.65f, 0.15f, 0f),
                0.29f, 0.27f, 0.038f, Color.Lerp(airframe, sideColor, 0.3f));

            var cockpit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cockpit.name = "Integrated Cockpit Glazing";
            cockpit.transform.SetParent(aircraft);
            cockpit.transform.localPosition = new Vector3(0.62f, 0.235f, 0f);
            cockpit.transform.localScale = new Vector3(0.17f, 0.035f, 0.085f);
            cockpit.GetComponent<Renderer>().sharedMaterial = Material(trim, 0.52f, 0.82f);
            for (var engine = -1; engine <= 1; engine += 2)
            {
                var nacelle = new GameObject(engine < 0 ? "Port CFM56 Engine" : "Starboard CFM56 Engine");
                nacelle.name = engine < 0 ? "Port CFM56 Engine" : "Starboard CFM56 Engine";
                nacelle.transform.SetParent(aircraft);
                nacelle.transform.localPosition = new Vector3(0.015f, 0.035f, engine * 0.34f);
                nacelle.AddComponent<MeshFilter>().sharedMesh = BuildTaperedFuselageMesh(
                    engine < 0 ? "Port Integrated Nacelle" : "Starboard Integrated Nacelle",
                    0.38f, 0.06f, 12, 0.9f);
                nacelle.AddComponent<MeshRenderer>().sharedMaterial = Material(
                    Color.Lerp(underside, new Color(0.27f, 0.31f, 0.34f), 0.32f), 0.48f, 0.54f);

                var intake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                intake.name = engine < 0 ? "Port Engine Intake" : "Starboard Engine Intake";
                intake.transform.SetParent(aircraft);
                intake.transform.localPosition = new Vector3(0.198f, 0.035f, engine * 0.34f);
                intake.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                intake.transform.localScale = new Vector3(0.043f, 0.008f, 0.043f);
                intake.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.08f, 0.11f, 0.14f), 0.72f, 0.35f);
            }

            var sensor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sensor.name = "Ventral EO-IR Sensor";
            sensor.transform.SetParent(aircraft);
            sensor.transform.localPosition = new Vector3(0.39f, 0.035f, 0f);
            sensor.transform.localScale = new Vector3(0.04f, 0.03f, 0.04f);
            sensor.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.18f, 0.23f, 0.27f), 0.54f, 0.72f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Aviation Patrol Indicator";
            ring.transform.SetParent(root);
            ring.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            ring.transform.localScale = new Vector3(0.92f, 0.025f, 0.92f);
            ring.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.08f, 0.86f, 1f), 0f, 0.82f);
            return root;
        }

        private static void AddAircraftPlanform(Transform parent, string name, Vector3 position,
            Vector2[] outline, float thickness, Color color)
        {
            var surface = new GameObject(name);
            surface.transform.SetParent(parent);
            surface.transform.localPosition = position;
            surface.AddComponent<MeshFilter>().sharedMesh = BuildExtrudedPlanform(name, outline, thickness);
            surface.AddComponent<MeshRenderer>().sharedMaterial = Material(color, 0.24f, 0.55f);
        }

        private static Mesh BuildExtrudedPlanform(string name, Vector2[] outline, float thickness)
        {
            // Retain the designed hard-point silhouette while doubling edge sampling. The extra
            // vertices improve lighting interpolation on swept wings without rounding their tips.
            outline = SubdivideOutline(outline, false);
            var count = outline.Length;
            var vertices = new Vector3[count * 2];
            for (var index = 0; index < count; index++)
            {
                vertices[index] = new Vector3(outline[index].x, thickness * 0.5f, outline[index].y);
                vertices[index + count] = new Vector3(outline[index].x, -thickness * 0.5f, outline[index].y);
            }
            var triangles = new List<int>((count - 2) * 6 + count * 6);
            for (var index = 1; index < count - 1; index++)
            {
                triangles.Add(0); triangles.Add(index); triangles.Add(index + 1);
                triangles.Add(count); triangles.Add(count + index + 1); triangles.Add(count + index);
            }
            for (var index = 0; index < count; index++)
            {
                var next = (index + 1) % count;
                triangles.Add(index); triangles.Add(index + count); triangles.Add(next);
                triangles.Add(next); triangles.Add(index + count); triangles.Add(next + count);
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildTaperedFuselageMesh(string name, float length, float radius,
            int radialSegments, float verticalScale)
        {
            radialSegments = Mathf.Max(8, radialSegments);
            var stations = new[] { -0.5f, -0.43f, -0.27f, 0.02f, 0.3f, 0.44f, 0.5f };
            var radii = new[] { 0.16f, 0.68f, 0.96f, 1f, 0.94f, 0.64f, 0.08f };
            var ringCount = stations.Length;
            var vertices = new Vector3[ringCount * radialSegments + 2];
            for (var ring = 0; ring < ringCount; ring++)
            for (var segment = 0; segment < radialSegments; segment++)
            {
                var angle = segment / (float)radialSegments * Mathf.PI * 2f;
                var ringRadius = radius * radii[ring];
                vertices[ring * radialSegments + segment] = new Vector3(stations[ring] * length,
                    Mathf.Cos(angle) * ringRadius * verticalScale, Mathf.Sin(angle) * ringRadius);
            }
            var rearCenter = ringCount * radialSegments;
            var noseCenter = rearCenter + 1;
            vertices[rearCenter] = new Vector3(-length * 0.5f, 0f, 0f);
            vertices[noseCenter] = new Vector3(length * 0.5f, 0f, 0f);
            var triangles = new List<int>((ringCount - 1) * radialSegments * 6 + radialSegments * 6);
            for (var ring = 0; ring < ringCount - 1; ring++)
            for (var segment = 0; segment < radialSegments; segment++)
            {
                var next = (segment + 1) % radialSegments;
                var currentRing = ring * radialSegments;
                var nextRing = (ring + 1) * radialSegments;
                triangles.Add(currentRing + segment); triangles.Add(nextRing + segment); triangles.Add(currentRing + next);
                triangles.Add(currentRing + next); triangles.Add(nextRing + segment); triangles.Add(nextRing + next);
            }
            for (var segment = 0; segment < radialSegments; segment++)
            {
                var next = (segment + 1) % radialSegments;
                triangles.Add(rearCenter); triangles.Add(segment); triangles.Add(next);
                var lastRing = (ringCount - 1) * radialSegments;
                triangles.Add(noseCenter); triangles.Add(lastRing + next); triangles.Add(lastRing + segment);
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddVerticalFin(Transform parent, string name, Vector3 position,
            float length, float height, float thickness, Color color)
        {
            var halfThickness = thickness * 0.5f;
            var profile = new[]
            {
                new Vector2(length * 0.45f, 0f), new Vector2(-length * 0.08f, height),
                new Vector2(-length * 0.42f, height * 0.91f), new Vector2(-length * 0.5f, 0f)
            };
            profile = SubdivideOutline(profile, false);
            var count = profile.Length;
            var vertices = new Vector3[count * 2];
            for (var index = 0; index < count; index++)
            {
                vertices[index] = new Vector3(profile[index].x, profile[index].y, halfThickness);
                vertices[index + count] = new Vector3(profile[index].x, profile[index].y, -halfThickness);
            }
            var triangles = new List<int>((count - 2) * 6 + count * 6);
            for (var index = 1; index < count - 1; index++)
            {
                triangles.Add(0); triangles.Add(index); triangles.Add(index + 1);
                triangles.Add(count); triangles.Add(count + index + 1); triangles.Add(count + index);
            }
            for (var index = 0; index < count; index++)
            {
                var next = (index + 1) % count;
                triangles.Add(index); triangles.Add(next); triangles.Add(index + count);
                triangles.Add(next); triangles.Add(next + count); triangles.Add(index + count);
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var fin = new GameObject(name);
            fin.transform.SetParent(parent);
            fin.transform.localPosition = position;
            fin.AddComponent<MeshFilter>().sharedMesh = mesh;
            fin.AddComponent<MeshRenderer>().sharedMaterial = Material(color, 0.2f, 0.48f);
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

        public static GameObject CreateTacticalJet(Color sideColor, string aircraftId = null)
        {
            aircraftId = aircraftId ?? string.Empty;
            var bomber = aircraftId == "us-b1b" || aircraftId == "plan-h6j";
            var lancer = aircraftId == "us-b1b";
            var stealth = aircraftId == "us-f35c" || aircraftId == "plan-j35";
            var electronicAttack = aircraftId == "us-ea18g";
            var twinTail = !bomber;
            var root = new GameObject(string.IsNullOrEmpty(aircraftId)
                ? "Tactical Strike Aircraft" : $"Tactical Aircraft {aircraftId}");
            var airframe = new GameObject("Detailed Airframe").transform;
            airframe.SetParent(root.transform);
            // Strike animation uses Transform.LookAt (+Z); model construction uses nose-forward +X.
            airframe.localRotation = Quaternion.Euler(0f, -90f, 0f);
            airframe.localScale = Vector3.one * (bomber ? 1.18f : 0.86f);

            var bodyColor = Color.Lerp(new Color(0.62f, 0.67f, 0.7f), sideColor, 0.25f);
            var panelColor = Color.Lerp(bodyColor, Color.black, 0.16f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = bomber ? "Bomber Fuselage" : "Fighter Fuselage";
            body.transform.SetParent(airframe);
            body.transform.localPosition = new Vector3(bomber ? 0f : 0.05f, 0.05f, 0f);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            body.transform.localScale = bomber
                ? new Vector3(0.13f, 0.67f, 0.13f) : new Vector3(0.115f, 0.47f, 0.115f);
            body.GetComponent<Renderer>().sharedMaterial = Material(bodyColor, 0.42f, 0.62f);

            var wings = bomber
                ? lancer
                    ? new[]
                    {
                        new Vector2(0.16f, 0.08f), new Vector2(-0.28f, 0.59f),
                        new Vector2(-0.43f, 0.55f), new Vector2(-0.23f, 0.09f),
                        new Vector2(-0.23f, -0.09f), new Vector2(-0.43f, -0.55f),
                        new Vector2(-0.28f, -0.59f), new Vector2(0.16f, -0.08f)
                    }
                    : new[]
                    {
                        new Vector2(0.28f, 0.09f), new Vector2(-0.02f, 0.72f),
                        new Vector2(-0.28f, 0.68f), new Vector2(-0.3f, 0.1f),
                        new Vector2(-0.3f, -0.1f), new Vector2(-0.28f, -0.68f),
                        new Vector2(-0.02f, -0.72f), new Vector2(0.28f, -0.09f)
                    }
                : stealth
                    ? new[]
                    {
                        new Vector2(0.31f, 0.06f), new Vector2(-0.2f, 0.48f),
                        new Vector2(-0.39f, 0.34f), new Vector2(-0.28f, 0.07f),
                        new Vector2(-0.28f, -0.07f), new Vector2(-0.39f, -0.34f),
                        new Vector2(-0.2f, -0.48f), new Vector2(0.31f, -0.06f)
                    }
                    : new[]
                    {
                        new Vector2(0.24f, 0.07f), new Vector2(-0.08f, 0.53f),
                        new Vector2(-0.27f, 0.49f), new Vector2(-0.25f, 0.08f),
                        new Vector2(-0.25f, -0.08f), new Vector2(-0.27f, -0.49f),
                        new Vector2(-0.08f, -0.53f), new Vector2(0.24f, -0.07f)
                    };
            AddAircraftPlanform(airframe, bomber ? "Swept Bomber Wings" : stealth ? "Stealth Planform" : "Swept Fighter Wings",
                new Vector3(0f, 0.055f, 0f), wings, bomber ? 0.035f : 0.028f, bodyColor);

            AddAircraftPlanform(airframe, "Tailplanes", new Vector3(bomber ? -0.55f : -0.36f, 0.1f, 0f),
                new[]
                {
                    new Vector2(0.1f, 0.04f), new Vector2(-0.07f, bomber ? 0.28f : 0.22f),
                    new Vector2(-0.17f, bomber ? 0.25f : 0.19f), new Vector2(-0.14f, 0.04f),
                    new Vector2(-0.14f, -0.04f), new Vector2(-0.17f, bomber ? -0.25f : -0.19f),
                    new Vector2(-0.07f, bomber ? -0.28f : -0.22f), new Vector2(0.1f, -0.04f)
                }, 0.025f, bodyColor);
            if (twinTail)
            {
                AddVerticalFin(airframe, "Port Tail Fin", new Vector3(-0.38f, 0.1f, 0.105f),
                    0.23f, 0.24f, 0.025f, panelColor);
                AddVerticalFin(airframe, "Starboard Tail Fin", new Vector3(-0.38f, 0.1f, -0.105f),
                    0.23f, 0.24f, 0.025f, panelColor);
            }
            else
            {
                AddVerticalFin(airframe, "Vertical Tail", new Vector3(bomber ? -0.55f : -0.38f, 0.1f, 0f),
                    bomber ? 0.3f : 0.22f, bomber ? 0.31f : 0.2f, 0.035f, panelColor);
            }

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Cockpit Canopy";
            canopy.transform.SetParent(airframe);
            canopy.transform.localPosition = new Vector3(bomber ? 0.5f : 0.36f, 0.16f, 0f);
            canopy.transform.localScale = bomber
                ? new Vector3(0.2f, 0.075f, 0.13f) : new Vector3(0.2f, 0.09f, 0.13f);
            canopy.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.06f, 0.16f, 0.23f), 0.62f, 0.8f);

            var engineOffset = lancer ? 0.16f : bomber ? 0.31f : 0.09f;
            var engineStations = aircraftId == "us-f35c" ? new[] { 0f } : new[] { -1f, 1f };
            foreach (var engine in engineStations)
            {
                var exhaust = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                exhaust.name = engine < 0f ? "Port Engine Exhaust" : engine > 0f
                    ? "Starboard Engine Exhaust" : "Single Engine Exhaust";
                exhaust.transform.SetParent(airframe);
                exhaust.transform.localPosition = new Vector3(bomber ? -0.46f : -0.37f, 0.035f, engine * engineOffset);
                exhaust.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                exhaust.transform.localScale = new Vector3(bomber ? 0.075f : 0.055f, 0.11f, bomber ? 0.075f : 0.055f);
                exhaust.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.12f, 0.14f, 0.16f), 0.78f, 0.42f);
            }
            if (electronicAttack)
            {
                AddBox(airframe, "Port Electronic Attack Pod", new Vector3(-0.08f, -0.025f, 0.49f),
                    new Vector3(0.34f, 0.045f, 0.045f), panelColor);
                AddBox(airframe, "Starboard Electronic Attack Pod", new Vector3(-0.08f, -0.025f, -0.49f),
                    new Vector3(0.34f, 0.045f, 0.045f), panelColor);
            }
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.65f;
            trail.startWidth = 0.055f;
            trail.endWidth = 0.008f;
            trail.material = Material(new Color(0.68f, 0.84f, 0.95f, 0.7f), 0f, 0f);
            return root;
        }

        private static void CreateShip(Transform parent, Vector3 localPosition, float scale,
            Color color, bool amphibious, bool carrier)
        {
            var ship = new GameObject(carrier ? "Aircraft Carrier" : amphibious ? "Amphibious Ship" : "Escort").transform;
            ship.SetParent(parent);
            ship.localPosition = localPosition;
            ship.localRotation = Quaternion.Euler(0f, -35f, 0f);
            ship.localScale = Vector3.one * scale;

            var hullLength = carrier ? 1.92f : amphibious ? 1.58f : 1.32f;
            var hullWidth = carrier ? 0.47f : amphibious ? 0.37f : 0.255f;

            var hull = new GameObject("Hull");
            hull.transform.SetParent(ship);
            hull.transform.localPosition = Vector3.zero;
            var mesh = BuildHullMesh(hullLength, hullWidth);
            hull.AddComponent<MeshFilter>().sharedMesh = mesh;
            hull.AddComponent<MeshRenderer>().sharedMaterial = Material(color, 0.55f, 0.35f);

            AddShapedDeck(ship, carrier ? "Flight Deck" : "Deck", new Vector3(-0.06f, 0.21f, 0f),
                carrier ? 1.82f : amphibious ? 1.46f : 1.18f,
                carrier ? 0.41f : amphibious ? 0.315f : 0.205f,
                Color.Lerp(color, Color.white, 0.16f));
            AddBox(ship, carrier ? "Island" : "Superstructure",
                new Vector3(carrier ? -0.3f : amphibious ? -0.34f : -0.14f, 0.35f, carrier ? -0.27f : 0f),
                new Vector3(carrier ? 0.34f : amphibious ? 0.46f : 0.32f,
                    carrier ? 0.3f : amphibious ? 0.24f : 0.21f,
                    carrier ? 0.2f : amphibious ? 0.44f : 0.26f),
                new Color(0.54f, 0.58f, 0.6f));
            AddBox(ship, "Bridge", new Vector3(carrier ? -0.25f : amphibious ? -0.38f : 0.05f,
                    0.5f, carrier ? -0.27f : 0f),
                new Vector3(0.23f, 0.12f, carrier ? 0.18f : amphibious ? 0.36f : 0.22f),
                new Color(0.68f, 0.72f, 0.73f));
            if (carrier)
            {
                AddBox(ship, "Runway Stripe", new Vector3(0.12f, 0.275f, 0.08f),
                    new Vector3(1.35f, 0.012f, 0.035f), new Color(0.92f, 0.92f, 0.78f));
                AddDeckAircraft(ship, new Vector3(0.3f, 0.31f, -0.2f), 0.28f);
                AddDeckAircraft(ship, new Vector3(-0.12f, 0.31f, 0.2f), 0.25f);
            }

            AddBox(ship, "Wake Port", new Vector3(-hullLength * 0.58f, -0.12f, hullWidth * 0.42f),
                new Vector3(0.72f, 0.015f, 0.055f), new Color(0.62f, 0.86f, 0.96f, 0.72f));
            AddBox(ship, "Wake Starboard", new Vector3(-hullLength * 0.58f, -0.12f, -hullWidth * 0.42f),
                new Vector3(0.72f, 0.015f, 0.055f), new Color(0.62f, 0.86f, 0.96f, 0.72f));

            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Mast";
            mast.transform.SetParent(ship);
            mast.transform.localPosition = new Vector3(-0.08f, 0.7f, 0f);
            mast.transform.localScale = new Vector3(0.032f, 0.24f, 0.032f);
            mast.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.34f, 0.37f, 0.39f), 0.7f, 0.3f);

            if (!carrier)
            {
                var radar = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                radar.name = "Radar Array";
                radar.transform.SetParent(ship);
                radar.transform.localPosition = new Vector3(-0.08f, 0.86f, 0f);
                radar.transform.localScale = new Vector3(0.13f, 0.08f, 0.13f);
                radar.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.68f, 0.72f, 0.7f), 0.55f, 0.4f);
                var gun = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                gun.name = "Forward Gun Mount";
                gun.transform.SetParent(ship);
                gun.transform.localPosition = new Vector3(hullLength * 0.28f, 0.34f, 0f);
                gun.transform.localScale = new Vector3(0.1f, 0.055f, 0.1f);
                gun.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.4f, 0.44f, 0.46f), 0.5f, 0.3f);
                AddBox(ship, "Gun Barrel", new Vector3(hullLength * 0.39f, 0.38f, 0f),
                    new Vector3(0.3f, 0.035f, 0.035f), new Color(0.32f, 0.35f, 0.37f));
            }
        }

        private static void AddShapedDeck(Transform parent, string name, Vector3 position,
            float length, float width, Color color)
        {
            var deck = new GameObject(name);
            deck.transform.SetParent(parent);
            deck.transform.localPosition = position;
            deck.transform.localScale = new Vector3(1f, 0.18f, 1f);
            deck.AddComponent<MeshFilter>().sharedMesh = BuildHullMesh(length, width);
            deck.AddComponent<MeshRenderer>().sharedMaterial = Material(color, 0.38f, 0.32f);
        }

        private static void AddDeckAircraft(Transform parent, Vector3 position, float length)
        {
            var miniature = new GameObject("Deck Aircraft").transform;
            miniature.SetParent(parent);
            miniature.localPosition = position;
            var scale = length / 0.72f;
            miniature.localScale = Vector3.one * scale;
            AddAircraftPlanform(miniature, "Folded Wings", Vector3.zero,
                new[]
                {
                    new Vector2(0.2f, 0.035f), new Vector2(-0.04f, 0.2f),
                    new Vector2(-0.16f, 0.15f), new Vector2(-0.12f, 0.03f),
                    new Vector2(-0.12f, -0.03f), new Vector2(-0.16f, -0.15f),
                    new Vector2(-0.04f, -0.2f), new Vector2(0.2f, -0.035f)
                }, 0.025f, new Color(0.72f, 0.75f, 0.76f));
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Deck Jet Fuselage";
            body.transform.SetParent(miniature);
            body.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            body.transform.localScale = new Vector3(0.045f, 0.24f, 0.045f);
            body.GetComponent<Renderer>().sharedMaterial = Material(new Color(0.76f, 0.79f, 0.8f), 0.3f, 0.5f);
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
            // A rounded 32-point waterline duplicated at keel level gives 64 vertices. Chaikin
            // sampling softens the former 16 hard transitions without changing ship proportions.
            var outline = new[]
            {
                new Vector2(halfLength, width * 0.04f),
                new Vector2(halfLength * 0.88f, width * 0.35f),
                new Vector2(halfLength * 0.68f, width * 0.7f),
                new Vector2(halfLength * 0.35f, width * 0.94f),
                new Vector2(-halfLength * 0.1f, width),
                new Vector2(-halfLength * 0.52f, width * 0.94f),
                new Vector2(-halfLength * 0.88f, width * 0.72f),
                new Vector2(-halfLength, width * 0.42f),
                new Vector2(-halfLength, -width * 0.42f),
                new Vector2(-halfLength * 0.88f, -width * 0.72f),
                new Vector2(-halfLength * 0.52f, -width * 0.94f),
                new Vector2(-halfLength * 0.1f, -width),
                new Vector2(halfLength * 0.35f, -width * 0.94f),
                new Vector2(halfLength * 0.68f, -width * 0.7f),
                new Vector2(halfLength * 0.88f, -width * 0.35f),
                new Vector2(halfLength, -width * 0.04f)
            };
            outline = SubdivideOutline(outline, true);
            var count = outline.Length;
            var vertices = new Vector3[count * 2];
            for (var index = 0; index < outline.Length; index++)
            {
                vertices[index] = new Vector3(outline[index].x, 0.15f, outline[index].y);
                vertices[index + count] = new Vector3(outline[index].x * 0.88f, -0.16f,
                    outline[index].y * 0.62f);
            }
            var triangles = new List<int>((count - 2) * 6 + count * 6);
            for (var index = 1; index < count - 1; index++)
            {
                triangles.Add(0); triangles.Add(index + 1); triangles.Add(index);
                triangles.Add(count); triangles.Add(count + index); triangles.Add(count + index + 1);
            }
            for (var index = 0; index < count; index++)
            {
                var next = (index + 1) % count;
                triangles.Add(index); triangles.Add(index + count); triangles.Add(next);
                triangles.Add(next); triangles.Add(index + count); triangles.Add(next + count);
            }
            var mesh = new Mesh
            {
                name = "Detailed 64-Vertex Ship Hull",
                vertices = vertices,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2[] SubdivideOutline(Vector2[] outline, bool rounded)
        {
            var result = new Vector2[outline.Length * 2];
            for (var index = 0; index < outline.Length; index++)
            {
                var current = outline[index];
                var next = outline[(index + 1) % outline.Length];
                if (rounded)
                {
                    result[index * 2] = Vector2.Lerp(current, next, 0.25f);
                    result[index * 2 + 1] = Vector2.Lerp(current, next, 0.75f);
                }
                else
                {
                    result[index * 2] = current;
                    result[index * 2 + 1] = Vector2.Lerp(current, next, 0.5f);
                }
            }
            return result;
        }
    }
}
