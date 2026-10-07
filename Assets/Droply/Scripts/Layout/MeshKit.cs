using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// Finished mesh data without a UnityEngine.Mesh, so the player and Tools/CompositionCheck build exactly the same geometry.
    /// Besides positions, normals and triangles it carries what the look needs: a tag per vertex (position inside its shape), a brightness jitter per shape,
    /// the owner (which tree or stone) and, after <see cref="Bake"/>, the final vertex colours with the light already in them.
    /// </summary>
    public sealed class MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[] Triangles;
        /// <summary>0 at the bottom, root or centre of a shape, 1 at its top, tip or rim (crowns, trunks, blades, petals, stones, hills).</summary>
        public float[] Tags;
        /// <summary>Small brightness offset shared by all vertices of one crown, tier or stone, so no two are the same shade.</summary>
        public float[] Jitters;
        /// <summary>Which tree, bush or stone a vertex belongs to (0: the ground and meadow details); a shape does not shadow itself.</summary>
        public int[] Owners;
        /// <summary>Baked colours (sRGB bytes). Null until <see cref="Bake.Run"/> has run; sky layers arrive with colours.</summary>
        public Color32[] Colors;

        public int TriangleCount { get { return Triangles.Length / 3; } }
    }

    /// <summary>
    /// Collects triangles. Every triangle gets its own three vertices and its face normal (flat shading, the faceted low-poly look).
    /// Colour is not decided here: each vertex carries a tag, a jitter and an owner, and <see cref="Bake"/> turns them into colours with the light baked in.
    /// </summary>
    public sealed class MeshBuilder
    {
        static readonly Vector3 Middle = new Vector3(.5f, .5f, .5f);

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int> triangles = new List<int>();
        readonly List<float> tags = new List<float>();
        readonly List<float> jitters = new List<float>();
        readonly List<int> owners = new List<int>();

        /// <summary>Applied to every vertex added from now on (set per crown, tier or stone).</summary>
        public float Jitter;
        public int Owner;

        public int TriangleCount { get { return triangles.Count / 3; } }

        /// <summary>Front face = the side the normal Cross(b - a, c - a) points to (Unity's winding). <paramref name="tag"/> holds one tag per corner.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 tag)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            tags.Add(tag.x); tags.Add(tag.y); tags.Add(tag.z);
            jitters.Add(Jitter); jitters.Add(Jitter); jitters.Add(Jitter);
            owners.Add(Owner); owners.Add(Owner); owners.Add(Owner);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c) { Tri(a, b, c, Middle); }

        /// <summary>Front face pointing away from <paramref name="inside"/>: closed, roughly convex shapes (crowns, stones).</summary>
        public void TriAway(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Vector3 tag)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - inside) < 0f) Tri(a, c, b, new Vector3(tag.x, tag.z, tag.y)); else Tri(a, b, c, tag);
        }

        public void TriAway(Vector3 a, Vector3 b, Vector3 c, Vector3 inside) { TriAway(a, b, c, inside, Middle); }

        /// <summary>Front face towards <paramref name="target"/> (ribbons that are seen from the inside, ground triangles seen from above).</summary>
        public void TriToward(Vector3 a, Vector3 b, Vector3 c, Vector3 target, Vector3 tag)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), target - (a + b + c) / 3f) < 0f) Tri(a, c, b, new Vector3(tag.x, tag.z, tag.y)); else Tri(a, b, c, tag);
        }

        public void TriToward(Vector3 a, Vector3 b, Vector3 c, Vector3 target) { TriToward(a, b, c, target, Middle); }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside, float ta, float tb, float tc, float td)
        {
            TriAway(a, b, c, inside, new Vector3(ta, tb, tc)); TriAway(a, c, d, inside, new Vector3(ta, tc, td));
        }

        public MeshData ToMesh()
        {
            return new MeshData
            {
                Vertices = vertices.ToArray(), Normals = normals.ToArray(), Triangles = triangles.ToArray(),
                Tags = tags.ToArray(), Jitters = jitters.ToArray(), Owners = owners.ToArray()
            };
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
