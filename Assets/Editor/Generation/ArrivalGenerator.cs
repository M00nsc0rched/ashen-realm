using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    internal sealed class ArrivalLayout
    {
        public Vector3 Tunnel;
        public Vector3 Caravan;
        public readonly List<(string name, Vector3 position, float yaw, Action<MeshBuilder> figure)> People = new List<(string, Vector3, float, Action<MeshBuilder>)>();
    }

    /// <summary>The road that brought the soul here ends at a closed mountain tunnel and a waiting caravan.</summary>
    internal sealed class ArrivalGenerator
    {
        readonly Material material;
        readonly System.Random rng = new System.Random(ValleyGenerator.Seed + 107);
        MeshBuilder mesh;
        Transform root;

        public ArrivalGenerator(Material material) => this.material = material;

        public ArrivalLayout Generate(Transform parent, Func<float, float, Vector3> ground)
        {
            root = new GameObject("Arrival Caravan And Tunnel").transform;
            root.SetParent(parent, false);
            mesh = new MeshBuilder();
            Vector3 mouth = ground(0f, -94.5f);
            Tunnel(mouth);
            Vector3 wagon = ground(-7.4f, -84.8f);
            Wagon(wagon, 12f, true);
            Wagon(ground(7.5f, -91f), -12f, false);
            var layout = new ArrivalLayout
            {
                Tunnel = mouth + new Vector3(0f, 0f, -2.3f),
                Caravan = wagon + Quaternion.Euler(0f, 12f, 0f) * new Vector3(1.98f, 0f, 0f),
            };
            layout.People.Add(("Caravan Driver", ground(-5f, -81.5f), 145f, PeopleMeshes.CaravanDriver));
            layout.People.Add(("Caravan Guard", ground(4.6f, -87.5f), 315f, PeopleMeshes.CaravanGuard));
            layout.People.Add(("Caravan Traveller", ground(-9.4f, -80.2f), 90f, PeopleMeshes.CaravanDriver));

            foreach (Vector2 p in new[] { new Vector2(-5f, -88f), new Vector2(8.8f, -87.5f) })
            {
                Vector3 at = ground(p.x, p.y);
                mesh.Push(at, Quaternion.identity, Vector3.one);
                PropMeshes.Barrel(mesh, rng);
                mesh.Pop();
                Solid("Caravan Barrel", at + Vector3.up * 0.45f, new Vector3(0.8f, 0.9f, 0.8f));
            }
            var geometry = new GameObject("Arrival Road Geometry");
            geometry.transform.SetParent(root, false);
            geometry.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.SaveMesh(mesh, "Arrival_Road");
            geometry.AddComponent<MeshRenderer>().sharedMaterial = material;
            return layout;
        }

        void Tunnel(Vector3 at)
        {
            mesh.Push(at, Quaternion.identity, Vector3.one);
            // The mountain surrounds a black recess. Its invisible threshold keeps the road back out of reach.
            foreach (float side in new[] { -1f, 1f })
            {
                for (int row = 0; row < 5; row++)
                    mesh.ShadedBox(new Vector3(side * 3.65f, 0.4f + row * 0.8f, -0.8f), new Vector3(1.3f, 0.77f, 3f), Pal.Stone, rng, 0.23f, 0.4f);
                mesh.ShadedBox(new Vector3(side * 6.3f, 3f, -2.4f), new Vector3(4.3f, 6.4f, 6f), Pal.RockDark, rng, 0.26f, 0.4f);
                mesh.SetColor(Pal.RockDark, 0.18f);
                mesh.Box(new Vector3(side * 3.2f, 2f, -3f), new Vector3(0.4f, 4f, 6f));
                Solid("Tunnel Side", at + new Vector3(side * 3.5f, 2.8f, -2f), new Vector3(1f, 5.6f, 6f));
            }
            for (int i = 0; i < 13; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector3 centre = new Vector3(Mathf.Cos(angle) * 3.05f, 3.7f + Mathf.Sin(angle) * 3.05f, -0.1f);
                mesh.Push(centre, Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg), Vector3.one);
                mesh.ShadedBox(Vector3.zero, new Vector3(1f, 0.78f, 1.25f), Pal.Stone, rng, 0.3f, 0.48f);
                mesh.Pop();
            }
            mesh.SetColor(Pal.RockDark, 0.2f);
            mesh.Box(new Vector3(0f, 7.5f, -2.7f), new Vector3(10.2f, 2f, 6f));
            mesh.SetColor(Pal.Black, 0.02f);
            mesh.Box(new Vector3(0f, 2.8f, -5.1f), new Vector3(6.2f, 5.8f, 0.3f));
            mesh.Box(new Vector3(0f, 5.9f, -3f), new Vector3(6.2f, 0.35f, 4.8f));
            mesh.SetColor(Pal.Stone, 0.23f);
            mesh.Box(new Vector3(0f, -0.09f, -2.7f), new Vector3(6.2f, 0.18f, 5.6f));
            mesh.Pop();
            Solid("Closed Arrival Tunnel", at + new Vector3(0f, 3.1f, -2.65f), new Vector3(6.2f, 6.2f, 0.4f));
            Solid("Tunnel Roof", at + new Vector3(0f, 6.5f, -2.6f), new Vector3(8f, 1.2f, 5.5f));
        }

        void Wagon(Vector3 at, float yaw, bool covered)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            mesh.Push(at, rotation, Vector3.one);
            mesh.ShadedBox(new Vector3(0f, 1.02f, 0f), new Vector3(2.75f, 0.25f, 4.2f), Pal.WoodDark, rng, 0.3f, 0.42f);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int row = 0; row < 3; row++)
                    mesh.ShadedBox(new Vector3(side * 1.35f, 1.25f + row * 0.24f, 0f), new Vector3(0.12f, 0.2f, 4.2f), Pal.Wood, rng, 0.32f, 0.48f);
                foreach (float z in new[] { -1.42f, 1.42f }) Wheel(new Vector3(side * 1.5f, 0.73f, z));
                mesh.SetColor(Pal.WoodDark, 0.35f);
                mesh.Box(new Vector3(side * 0.8f, 0.9f, 3.1f), new Vector3(0.12f, 0.12f, 2.5f));
            }
            foreach (float z in new[] { -2f, 2f })
                mesh.ShadedBox(new Vector3(0f, 1.52f, z), new Vector3(2.65f, 0.8f, 0.12f), Pal.Wood, rng, 0.32f, 0.45f);
            if (covered)
            {
                for (int span = 0; span < 10; span++)
                {
                    float a = span * Mathf.PI / 10f, next = (span + 1) * Mathf.PI / 10f;
                    Vector3 p = new Vector3(Mathf.Cos(a) * 1.36f, 2f + Mathf.Sin(a) * 1.1f, -1.95f);
                    Vector3 q = new Vector3(Mathf.Cos(next) * 1.36f, 2f + Mathf.Sin(next) * 1.1f, -1.95f);
                    mesh.SetColor(span % 3 == 0 ? Pal.ClothDark : Pal.Thatch, span % 3 == 0 ? 0.43f : 0.55f);
                    mesh.SetLayer(TexLayer.Cloth, UVMode.Triplanar, 0.8f);
                    mesh.DoubleSided(p, q, q + Vector3.forward * 3.9f, Vector3.up);
                    mesh.DoubleSided(p, q + Vector3.forward * 3.9f, p + Vector3.forward * 3.9f, Vector3.up);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    mesh.Push(new Vector3(i % 2 == 0 ? -0.6f : 0.6f, 1.15f, i < 2 ? -0.9f : 0.6f), Quaternion.identity, Vector3.one);
                    PropMeshes.Crate(mesh, rng, i == 3 ? 1f : 0.85f);
                    mesh.Pop();
                }
            }
            mesh.Pop();
            Solid("Caravan Wagon", at + Vector3.up * 1.35f, new Vector3(3.4f, 2.7f, 4.6f), yaw);
        }

        void Wheel(Vector3 at)
        {
            mesh.SetColor(Pal.SteelDark, 0.34f);
            mesh.Push(at, Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            mesh.Prism(Vector3.down * 0.09f, 0.73f, 0.73f, 0.18f, 12);
            mesh.SetColor(Pal.WoodDark, 0.28f);
            mesh.Prism(Vector3.down * 0.1f, 0.63f, 0.63f, 0.2f, 12);
            mesh.Pop();
            for (int spoke = 0; spoke < 6; spoke++)
            {
                mesh.SetColor(Pal.Wood, 0.45f);
                mesh.Push(at, Quaternion.Euler(spoke * 30f, 0f, 0f), Vector3.one);
                mesh.Box(Vector3.zero, new Vector3(0.23f, 0.08f, 1.26f));
                mesh.Pop();
            }
        }

        void Solid(string name, Vector3 position, Vector3 size, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<BoxCollider>().size = size;
        }
    }
}
