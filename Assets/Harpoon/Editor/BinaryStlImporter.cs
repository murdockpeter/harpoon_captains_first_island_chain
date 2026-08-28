using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Harpoon.Editor
{
    [ScriptedImporter(1, "stl")]
    public sealed class BinaryStlImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            using var stream = File.OpenRead(context.assetPath);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 84) throw new InvalidDataException("STL file is too short.");
            reader.ReadBytes(80);
            var triangleCount = reader.ReadUInt32();
            if (84L + triangleCount * 50L != stream.Length)
                throw new InvalidDataException("Only binary STL files are supported.");

            var vertices = new List<Vector3>(Math.Min((int)triangleCount * 3, 250000));
            var triangles = new List<int>(Math.Min((int)triangleCount * 3, 250000));
            var lookup = new Dictionary<Vector3, int>();
            var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (var triangle = 0u; triangle < triangleCount; triangle++)
            {
                reader.ReadBytes(12); // Source normal; normals are rebuilt after welding.
                var indices = new int[3];
                for (var corner = 0; corner < 3; corner++)
                {
                    var sourceX = reader.ReadSingle();
                    var sourceY = reader.ReadSingle();
                    var sourceZ = reader.ReadSingle();
                    // The supplied model is Z-up with its nose toward -Y. Convert it to the
                    // game's Y-up, nose-forward +X convention.
                    var vertex = new Vector3(-sourceY, sourceZ, sourceX);
                    minimum = Vector3.Min(minimum, vertex);
                    maximum = Vector3.Max(maximum, vertex);
                    if (!lookup.TryGetValue(vertex, out var index))
                    {
                        index = vertices.Count;
                        lookup.Add(vertex, index);
                        vertices.Add(vertex);
                    }
                    indices[corner] = index;
                }
                reader.ReadUInt16();
                // Axis conversion changes handedness, so reverse source winding.
                triangles.Add(indices[0]);
                triangles.Add(indices[2]);
                triangles.Add(indices[1]);
            }

            var center = (minimum + maximum) * 0.5f;
            var length = Mathf.Max(0.0001f, maximum.x - minimum.x);
            for (var index = 0; index < vertices.Count; index++)
                vertices[index] = (vertices[index] - center) / length;

            var mesh = new Mesh
            {
                name = Path.GetFileNameWithoutExtension(context.assetPath),
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            context.AddObjectToAsset("mesh", mesh);
            context.SetMainObject(mesh);
        }
    }
}
