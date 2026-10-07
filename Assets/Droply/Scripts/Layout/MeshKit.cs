using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>Finished mesh data without a UnityEngine.Mesh, so the player and Tools/CompositionCheck build exactly the same geometry.</summary>
    public sealed class MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[] Triangles;

        public int TriangleCount { get { return Triangles.Length / 3; } }
    }

    /// <summary>
    /// Collects triangles. Every triangle gets its own three vertices and its face normal (flat shading, the faceted low-poly look);
    /// URP/Lit has no vertex colours, so colour comes from the material and shading from light alone.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int> triangles = new List<int>();

        public int TriangleCount { get { return triangles.Count / 3; } }

        /// <summary>Front face = the side the normal Cross(b - a, c - a) points to (Unity's winding).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }

        /// <summary>Front face pointing away from <paramref name="inside"/>: closed, roughly convex shapes (crowns, stones).</summary>
        public void TriAway(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - inside) < 0f) Tri(a, c, b); else Tri(a, b, c);
        }

        /// <summary>Front face towards <paramref name="target"/> (ribbons that are seen from the inside, ground triangles seen from above).</summary>
        public void TriToward(Vector3 a, Vector3 b, Vector3 c, Vector3 target)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), target - (a + b + c) / 3f) < 0f) Tri(a, c, b); else Tri(a, b, c);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
        {
            TriAway(a, b, c, inside); TriAway(a, c, d, inside);
        }

        public MeshData ToMesh()
        {
            return new MeshData { Vertices = vertices.ToArray(), Normals = normals.ToArray(), Triangles = triangles.ToArray() };
        }
    }

    /// <summary>Rotation as three axes (no Quaternion, so it also runs outside Unity).</summary>
    public struct Basis
    {
        public Vector3 X, Y, Z;

        public static Basis Identity { get { return new Basis { X = Vector3.right, Y = Vector3.up, Z = Vector3.forward }; } }

        public Vector3 Apply(Vector3 v) { return X * v.x + Y * v.y + Z * v.z; }

        static Vector3 Rotate(Vector3 v, Vector3 axis, float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }

        /// <summary>Roll around Z, then pitch around X, then yaw around Y (radians).</summary>
        public static Basis Euler(float yaw, float pitch, float roll)
        {
            Vector3 x = Vector3.right, y = Vector3.up, z = Vector3.forward;
            x = Rotate(x, Vector3.forward, roll); y = Rotate(y, Vector3.forward, roll); z = Rotate(z, Vector3.forward, roll);
            x = Rotate(x, Vector3.right, pitch); y = Rotate(y, Vector3.right, pitch); z = Rotate(z, Vector3.right, pitch);
            x = Rotate(x, Vector3.up, yaw); y = Rotate(y, Vector3.up, yaw); z = Rotate(z, Vector3.up, yaw);
            return new Basis { X = x, Y = y, Z = z };
        }
    }
}
