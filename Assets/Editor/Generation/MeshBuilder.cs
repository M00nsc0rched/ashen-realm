using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Accumulates flat-shaded, palette-coloured geometry. Every triangle owns its vertices, giving the
    /// faceted low-poly look. Shape helpers orient faces automatically, so winding mistakes cannot occur.
    /// </summary>
    internal sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        readonly List<bool> fixedNormal = new List<bool>();
        readonly Stack<Matrix4x4> matrixStack = new Stack<Matrix4x4>();

        public Matrix4x4 Matrix = Matrix4x4.identity;
        Vector2 currentUV = Palette.UV(Pal.Stone, 0.5f);

        public int VertexCount => vertices.Count;
        public int TriangleCount => triangles.Count / 3;

        public void SetColor(Pal family, float shade = 0.5f) => currentUV = Palette.UV(family, shade);

        public void Push(Matrix4x4 local)
        {
            matrixStack.Push(Matrix);
            Matrix *= local;
        }

        public void Push(Vector3 position, Quaternion rotation, Vector3 scale) => Push(Matrix4x4.TRS(position, rotation, scale));

        public void Pop() => Matrix = matrixStack.Pop();

        // ---- Primitives --------------------------------------------------------------------------

        /// <summary>Adds a triangle (local space) whose front face points along <paramref name="facing"/> (world space).</summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            a = Matrix.MultiplyPoint3x4(a);
            b = Matrix.MultiplyPoint3x4(b);
            c = Matrix.MultiplyPoint3x4(c);
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, facing) < 0f)
            {
                (b, c) = (c, b);
                n = -n;
            }
            Emit(a, b, c, n.normalized);
        }

        /// <summary>Adds a triangle facing away from <paramref name="interior"/> (local space point).</summary>
        public void TriangleOutward(Vector3 a, Vector3 b, Vector3 c, Vector3 interior)
        {
            Vector3 centroid = (a + b + c) / 3f;
            Vector3 facing = Matrix.MultiplyVector(centroid - interior);
            Triangle(a, b, c, facing);
        }

        public void QuadOutward(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 interior)
        {
            TriangleOutward(a, b, c, interior);
            TriangleOutward(a, c, d, interior);
        }

        /// <summary>
        /// Two-sided triangle sharing one fixed normal on both sides (foliage lit like the ground beneath it, cloth).
        /// The normal is given in local space and transformed by the current matrix.
        /// </summary>
        public void DoubleSided(Vector3 a, Vector3 b, Vector3 c, Vector3 localNormal)
        {
            a = Matrix.MultiplyPoint3x4(a);
            b = Matrix.MultiplyPoint3x4(b);
            c = Matrix.MultiplyPoint3x4(c);
            Vector3 n = Matrix.MultiplyVector(localNormal).normalized;
            Emit(a, b, c, n, true);
            Emit(a, c, b, n, true);
        }

        void Emit(Vector3 a, Vector3 b, Vector3 c, Vector3 n, bool isFixedNormal = false)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(currentUV); uvs.Add(currentUV); uvs.Add(currentUV);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            fixedNormal.Add(isFixedNormal);
        }

        // ---- Shapes --------------------------------------------------------------------------------

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
            Face(p3, p2, p6, p7, 0.08f);
            Face(p0, p1, p2, p3, 0f);
            Face(p5, p4, p7, p6, -0.02f);
            Face(p4, p0, p3, p7, -0.04f);
            Face(p1, p5, p6, p2, 0.02f);
            Face(p4, p5, p1, p0, -0.1f);
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
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Vector3 interior = baseCenter + Vector3.up * (height * 0.5f);
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

        /// <summary>Irregular boulder: an icosahedron with jittered vertices.</summary>
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
            // Flatten the underside so rocks sit on the ground.
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
        /// Regular faces get recomputed flat normals; fixed-normal foliage has its normal transformed.
        /// Matrices must not mirror (no negative scale), since source winding is kept.
        /// </summary>
        public void Append(MeshBuilder other, Matrix4x4 matrix)
        {
            Matrix4x4 m = Matrix * matrix;
            for (int t = 0; t < other.fixedNormal.Count; t++)
            {
                int ia = other.triangles[t * 3], ib = other.triangles[t * 3 + 1], ic = other.triangles[t * 3 + 2];
                Vector3 a = m.MultiplyPoint3x4(other.vertices[ia]);
                Vector3 b = m.MultiplyPoint3x4(other.vertices[ib]);
                Vector3 c = m.MultiplyPoint3x4(other.vertices[ic]);
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                bool isFixed = other.fixedNormal[t];
                Vector3 normal = isFixed ? m.MultiplyVector(other.normals[ia]).normalized : n.normalized;

                int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uvs.Add(other.uvs[ia]); uvs.Add(other.uvs[ib]); uvs.Add(other.uvs[ic]);
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                fixedNormal.Add(isFixed);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static float Lerp(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
