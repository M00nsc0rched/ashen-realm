using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Accumulates geometry for the AshenRealm/PixelLit shader. Every triangle owns its vertices (hard edges by
    /// default); terrain supplies smooth per-vertex normals. Shape helpers orient faces automatically, so winding
    /// mistakes cannot occur. The current <see cref="Surface"/> decides colour, texture layer and UV mapping.
    /// </summary>
    internal sealed class MeshBuilder
    {
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector4> uv0 = new List<Vector4>();
        readonly List<Vector4> uv1 = new List<Vector4>();
        readonly List<int> triangles = new List<int>();
        readonly Stack<Matrix4x4> matrixStack = new Stack<Matrix4x4>();

        public Matrix4x4 Matrix = Matrix4x4.identity;
        public Surface Current = Palette.SurfaceFor(Pal.Stone, 0.5f);

        public int VertexCount => positions.Count;
        public int TriangleCount => triangles.Count / 3;

        public void SetColor(Pal family, float shade = 0.5f) => Current = Palette.SurfaceFor(family, shade);

        /// <summary>Overrides the texture layer / mapping of the current surface (colour is kept).</summary>
        public void SetLayer(TexLayer layer, UVMode mode, float tileSize = 1f, float direct = 0f)
        {
            Current.Layer = layer;
            Current.Mode = mode;
            Current.TileSize = tileSize;
            Current.Direct = direct;
        }

        public void Push(Matrix4x4 local)
        {
            matrixStack.Push(Matrix);
            Matrix *= local;
        }

        public void Push(Vector3 position, Quaternion rotation, Vector3 scale) => Push(Matrix4x4.TRS(position, rotation, scale));

        public void Pop() => Matrix = matrixStack.Pop();

        // ---- Low level ---------------------------------------------------------------------------------

        struct Corner
        {
            public Vector3 Position;
            public Vector2 UV;
            public float Wind;
        }

        /// <summary>Emits one triangle already in builder space with a flat normal and the current surface.</summary>
        void Emit(Corner a, Corner b, Corner c, Vector3 normal, bool explicitUV)
        {
            if (!explicitUV)
            {
                a.UV = Project(a.Position, normal);
                b.UV = Project(b.Position, normal);
                c.UV = Project(c.Position, normal);
            }
            AddVertex(a, normal);
            AddVertex(b, normal);
            AddVertex(c, normal);
        }

        void AddVertex(Corner corner, Vector3 normal)
        {
            triangles.Add(positions.Count);
            positions.Add(corner.Position);
            normals.Add(normal);
            colors.Add(new Color(Current.Color.r, Current.Color.g, Current.Color.b, 0f));
            int layer = (int)Current.Layer;
            uv0.Add(new Vector4(corner.UV.x, corner.UV.y, layer, layer));
            uv1.Add(new Vector4(Current.Direct, Current.Emission, corner.Wind, Current.Sheen));
        }

        Vector2 Project(Vector3 p, Vector3 n)
        {
            float size = Mathf.Max(0.01f, Current.TileSize);
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z) / size;
            if (ax >= az) return new Vector2(p.z, p.y) / size;
            return new Vector2(p.x, p.y) / size;
        }

        /// <summary>
        /// Adds a fully specified vertex triangle (builder-space positions). Used by the terrain for smooth normals,
        /// per-vertex colours and two-layer texture blending.
        /// </summary>
        public void RawTriangle(Vector3[] p, Vector3[] n, Color[] c, Vector4[] texUV, Vector4 parameters)
        {
            for (int i = 0; i < 3; i++)
            {
                triangles.Add(positions.Count);
                positions.Add(p[i]);
                normals.Add(n[i]);
                colors.Add(c[i]);
                uv0.Add(texUV[i]);
                uv1.Add(parameters);
            }
        }

        // ---- Primitives --------------------------------------------------------------------------------

        /// <summary>Adds a triangle (local space) whose front face points along <paramref name="facing"/> (builder space).</summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing) =>
            TriangleUV(a, b, c, Vector2.zero, Vector2.zero, Vector2.zero, facing, false);

        void TriangleUV(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 facing, bool explicitUV)
        {
            var ca = new Corner { Position = Matrix.MultiplyPoint3x4(a), UV = ua };
            var cb = new Corner { Position = Matrix.MultiplyPoint3x4(b), UV = ub };
            var cc = new Corner { Position = Matrix.MultiplyPoint3x4(c), UV = uc };
            Vector3 n = Vector3.Cross(cb.Position - ca.Position, cc.Position - ca.Position);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, facing) < 0f)
            {
                (cb, cc) = (cc, cb);
                n = -n;
            }
            Emit(ca, cb, cc, n.normalized, explicitUV);
        }

        /// <summary>Adds a triangle facing away from <paramref name="interior"/> (local space point).</summary>
        public void TriangleOutward(Vector3 a, Vector3 b, Vector3 c, Vector3 interior)
        {
            Vector3 centroid = (a + b + c) / 3f;
            Triangle(a, b, c, Matrix.MultiplyVector(centroid - interior));
        }

        /// <summary>
        /// Quad a→b→c→d facing away from <paramref name="interior"/>. In FaceUnit mode a→b is U and b→c is V, and the
        /// tile repeats along the longer side so tiles stay roughly square.
        /// </summary>
        public void QuadOutward(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 interior)
        {
            Vector3 centroid = (a + b + c + d) * 0.25f;
            Vector3 facing = Matrix.MultiplyVector(centroid - interior);
            if (Current.Mode == UVMode.FaceUnit)
            {
                float width = Matrix.MultiplyVector(b - a).magnitude, height = Matrix.MultiplyVector(c - b).magnitude;
                float ru = height > 1e-4f ? Mathf.Max(1f, Mathf.Round(width / height)) : 1f;
                float rv = width > 1e-4f ? Mathf.Max(1f, Mathf.Round(height / width)) : 1f;
                Vector2 ua = Vector2.zero, ub = new Vector2(ru, 0f), uc = new Vector2(ru, rv), ud = new Vector2(0f, rv);
                TriangleUV(a, b, c, ua, ub, uc, facing, true);
                TriangleUV(a, c, d, ua, uc, ud, facing, true);
            }
            else
            {
                Triangle(a, b, c, facing);
                Triangle(a, c, d, facing);
            }
        }

        /// <summary>Two-sided triangle sharing one normal (given in local space) on both sides: cloth, thin strips.</summary>
        public void DoubleSided(Vector3 a, Vector3 b, Vector3 c, Vector3 localNormal)
        {
            Vector3 n = Matrix.MultiplyVector(localNormal).normalized;
            var ca = new Corner { Position = Matrix.MultiplyPoint3x4(a) };
            var cb = new Corner { Position = Matrix.MultiplyPoint3x4(b) };
            var cc = new Corner { Position = Matrix.MultiplyPoint3x4(c) };
            Emit(ca, cb, cc, n, false);
            Emit(ca, cc, cb, n, false);
        }

        /// <summary>
        /// Textured card (quad) with explicit UV rectangle: bl, br at the bottom (no wind), tr, tl at the top
        /// (wind weight <paramref name="windTop"/>). Two-sided cards share <paramref name="localNormal"/>.
        /// </summary>
        public void Card(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Vector3 localNormal, float windTop, bool twoSided = true,
            float windBottom = 0f)
        {
            Vector3 n = Matrix.MultiplyVector(localNormal).normalized;
            var a = new Corner { Position = Matrix.MultiplyPoint3x4(bl), UV = new Vector2(0f, 0f), Wind = windBottom };
            var b = new Corner { Position = Matrix.MultiplyPoint3x4(br), UV = new Vector2(1f, 0f), Wind = windBottom };
            var c = new Corner { Position = Matrix.MultiplyPoint3x4(tr), UV = new Vector2(1f, 1f), Wind = windTop };
            var d = new Corner { Position = Matrix.MultiplyPoint3x4(tl), UV = new Vector2(0f, 1f), Wind = windTop };
            Emit(a, b, c, n, true);
            Emit(a, c, d, n, true);
            if (!twoSided) return;
            Emit(a, c, b, n, true);
            Emit(a, d, c, n, true);
        }

        // ---- Shapes ------------------------------------------------------------------------------------

        public void Box(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 p0 = center + new Vector3(-h.x, -h.y, -h.z), p1 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p2 = center + new Vector3(h.x, h.y, -h.z), p3 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p4 = center + new Vector3(-h.x, -h.y, h.z), p5 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p6 = center + new Vector3(h.x, h.y, h.z), p7 = center + new Vector3(-h.x, h.y, h.z);
            QuadOutward(p0, p1, p2, p3, center);
            QuadOutward(p5, p4, p7, p6, center);
            QuadOutward(p4, p0, p3, p7, center);
            QuadOutward(p1, p5, p6, p2, center);
            QuadOutward(p3, p2, p6, p7, center);
            QuadOutward(p4, p5, p1, p0, center);
        }

        /// <summary>Box whose faces each get a random shade of the family (stone blocks, planks).</summary>
        public void ShadedBox(Vector3 center, Vector3 size, Pal family, System.Random rng, float shadeMin, float shadeMax)
        {
            Vector3 h = size * 0.5f;
            Vector3 p0 = center + new Vector3(-h.x, -h.y, -h.z), p1 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p2 = center + new Vector3(h.x, h.y, -h.z), p3 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p4 = center + new Vector3(-h.x, -h.y, h.z), p5 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p6 = center + new Vector3(h.x, h.y, h.z), p7 = center + new Vector3(-h.x, h.y, h.z);
            float baseShade = Lerp(rng, shadeMin, shadeMax);
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float offset)
            {
                SetColor(family, Mathf.Clamp01(baseShade + offset));
                QuadOutward(a, b, c, d, center);
            }
            Face(p3, p2, p6, p7, 0.06f);
            Face(p0, p1, p2, p3, 0f);
            Face(p5, p4, p7, p6, -0.02f);
            Face(p4, p0, p3, p7, -0.03f);
            Face(p1, p5, p6, p2, 0.02f);
            Face(p4, p5, p1, p0, -0.08f);
        }

        /// <summary>Frustum along local +Y (cylinder, cone or tapered prism).</summary>
        public void Prism(Vector3 baseCenter, float bottomRadius, float topRadius, float height, int sides,
            bool capBottom = true, bool capTop = true, float angleOffset = 0f)
        {
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = angleOffset + i * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                bottom[i] = baseCenter + dir * bottomRadius;
                top[i] = baseCenter + dir * topRadius + Vector3.up * height;
            }
            Vector3 topCenter = baseCenter + Vector3.up * height;
            Vector3 interior = baseCenter + Vector3.up * (height * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (topRadius > 0.0001f)
                    QuadOutward(bottom[i], bottom[j], top[j], top[i], interior);
                else
                    TriangleOutward(bottom[i], bottom[j], topCenter, interior);
                if (capBottom && bottomRadius > 0.0001f)
                    Triangle(bottom[i], bottom[j], baseCenter, Matrix.MultiplyVector(Vector3.down));
                if (capTop && topRadius > 0.0001f)
                    Triangle(top[i], top[j], topCenter, Matrix.MultiplyVector(Vector3.up));
            }
        }

        /// <summary>Irregular boulder: an icosahedron with jittered vertices and a flattened base.</summary>
        public void Rock(Vector3 center, Vector3 size, System.Random rng, Pal family, float shadeMin, float shadeMax, float jitter = 0.28f)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] v =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 n = v[i].normalized;
                float r = 1f + Lerp(rng, -jitter, jitter);
                v[i] = center + Vector3.Scale(n * r * 0.5f, size);
            }
            for (int i = 0; i < v.Length; i++)
                if (v[i].y < center.y - size.y * 0.25f) v[i].y = center.y - size.y * 0.25f;

            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = v[f[i]], b = v[f[i + 1]], c = v[f[i + 2]];
                float up = Vector3.Cross(b - a, c - a).normalized.y;
                SetColor(family, Mathf.Clamp01(Lerp(rng, shadeMin, shadeMax) + Mathf.Abs(up) * 0.12f));
                TriangleOutward(a, b, c, center);
            }
        }

        /// <summary>
        /// Appends another builder's triangles transformed by <paramref name="matrix"/> (after the current matrix).
        /// Matrices must not mirror (no negative scale), since source winding is kept.
        /// </summary>
        public void Append(MeshBuilder other, Matrix4x4 matrix)
        {
            Matrix4x4 m = Matrix * matrix;
            Matrix4x4 normalMatrix = m.inverse.transpose;
            for (int i = 0; i < other.triangles.Count; i++)
            {
                int index = other.triangles[i];
                triangles.Add(positions.Count);
                positions.Add(m.MultiplyPoint3x4(other.positions[index]));
                normals.Add(normalMatrix.MultiplyVector(other.normals[index]).normalized);
                colors.Add(other.colors[index]);
                uv0.Add(other.uv0[index]);
                uv1.Add(other.uv1[index]);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static float Lerp(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
