using System.Collections.Generic;
using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// The places of the north land: the toll ledger, Patkós Stable, Kévés Farm and the burned farm, Gallows Crossroads,
    /// Feketefenyves, Tükörtó, the watchtower, the Ash Field, the approach with Hollowgate's silhouette, the wayside shrines and
    /// the horse posts. Built into the decor chunks; haystacks, cairns and dead trees go through the instanced scatter.
    /// </summary>
    internal sealed partial class ValleyGenerator
    {
        const float LakeDepth = 3.5f;
        static readonly Vector2 BellIsland = new Vector2(195f, 380f);
        float lakeShore;

        /// <summary>The jetty runs on from the lake road's last stretch, which ends on this line, out over the water.</summary>
        internal static readonly Vector2 JettyStart = new Vector2(128f, 344f), JettyAxis = new Vector2(0.894f, 0.447f).normalized;
        internal const float JettyLength = 24f, JettyHalfWidth = 1.6f;

        readonly List<(string name, Vector3 baseCentre, float yaw, Vector3 size)> footings = new List<(string, Vector3, float, Vector3)>();
        int haystackKind;
        readonly Dictionary<Pal, Mesh> paddockHorses = new Dictionary<Pal, Mesh>();
        int[] cairnKinds, deadTreeKinds;

        Vector3 Ground(Vector2 p) => Ground(p.x, p.y);

        /// <summary>The yaw that turns a prop's front (-Z) from <paramref name="from"/> towards <paramref name="to"/>.</summary>
        static float YawFacing(Vector2 from, Vector2 to) => Mathf.Atan2(from.x - to.x, from.y - to.y) * Mathf.Rad2Deg;

        (float low, float high) Span(Vector2 centre, float yaw, Vector2 size)
        {
            float low = float.MaxValue, high = float.MinValue;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i <= 4; i++)
            for (int j = 0; j <= 4; j++)
            {
                Vector3 o = rotation * new Vector3((i / 4f - 0.5f) * size.x, 0f, (j / 4f - 0.5f) * size.y);
                float h = SurfaceHeight(centre.x + o.x, centre.y + o.z);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
            return (low, high);
        }

        /// <summary>
        /// Where a building stands: on the lowest ground under its footprint, a little sunk, so no corner floats. The footprint is
        /// kept as a marker for the validator's check that it reaches the ground and is not buried.
        /// </summary>
        Vector3 Settle(string name, Vector2 centre, float yaw, Vector2 size, float height, float sink = 0.15f)
        {
            var at = new Vector3(centre.x, Span(centre, yaw, size).low - sink, centre.y);
            footings.Add((name, at, yaw, new Vector3(size.x, height, size.y)));
            return at;
        }

        void BuildNorthPlaces()
        {
            var hay = new MeshBuilder();
            hay.SetColor(Pal.Thatch, 0.56f);
            hay.Prism(Vector3.zero, 1.7f, 1.2f, 1.5f, 12);
            hay.Prism(Vector3.up * 1.5f, 1.2f, 0.08f, 1.4f, 12);
            haystackKind = AddKind("Haystack", hay, null, 160f, true);
            cairnKinds = new int[3];
            deadTreeKinds = new int[3];
            for (int v = 0; v < 3; v++)
            {
                var cairn = new MeshBuilder();
                float y = 0f;
                for (int s = 0; s < 4 - v % 2; s++)
                {
                    float w = R(1.2f, 1.5f) * (1f - s * 0.2f), h = w * 0.6f;
                    // Rock() flattens its base a quarter of its height below the centre: that is where the stone sits.
                    cairn.Rock(new Vector3(R(-0.08f, 0.08f), y + h * 0.25f, R(-0.08f, 0.08f)), new Vector3(w, h, w * R(0.85f, 1.05f)), rng,
                        s % 2 == 0 ? Pal.Rock : Pal.RockDark, 0.28f, 0.46f, 0.2f);
                    y += h * 0.6f;
                }
                cairnKinds[v] = AddKind($"Cairn_{v}", cairn, null, 140f, true);
                var dead = new MeshBuilder();
                PropMeshes.DeadTree(dead, rng, 5f + v * 1.5f);
                deadTreeKinds[v] = AddKind($"DeadTree_{v}", dead, null, 240f, true);
            }

            BuildTollLedger();
            BuildStable();
            BuildFarm();
            BuildBurnedFarm();
            BuildCrossroads();
            BuildForestPlaces();
            BuildLake();
            BuildWatchtower();
            BuildAshField();
            Shrine("Harvest Shrine", new Vector2(-3f, 205f), new Vector2(1f, 205f), true, GameText.HarvestShrineLore);
            Shrine("North Shrine", new Vector2(8f, 470f), new Vector2(-4f, 470f), false, GameText.NorthShrineLore);
            BuildHollowgateSilhouette();
            BuildHorsePosts();

            var parent = new GameObject("Footings").transform;
            parent.SetParent(root, false);
            foreach (var f in footings)
            {
                var marker = new GameObject(f.name).transform;
                marker.SetParent(parent, false);
                marker.SetPositionAndRotation(f.baseCentre, Quaternion.Euler(0f, f.yaw, 0f));
                marker.localScale = f.size;
            }

            WorldVista.Add(root, "Toll Ledger", Ground(6.5f, 98.5f) + Vector3.up * 1.7f, Ground(10f, 95f) + Vector3.up * 1f);
            WorldVista.Add(root, "Patkos Stable And Paddock", Ground(20f, 128f) + Vector3.up * 1.7f, Ground(42f, 118f) + Vector3.up * 1.5f);
            WorldVista.Add(root, "Keves Farmyard", Ground(112f, 192f) + Vector3.up * 1.7f, Ground(135f, 202f) + Vector3.up * 2.5f);
            WorldVista.Add(root, "Golden Fields Haystacks", Ground(84f, 200f) + Vector3.up * 1.7f, Ground(110f, 230f) + Vector3.up * 1f);
            WorldVista.Add(root, "Burned Farm", Ground(-98f, 162f) + Vector3.up * 1.7f, Ground(-112f, 172f) + Vector3.up * 2f);
            WorldVista.Add(root, "Gallows Crossroads", Ground(-6f, 286f) + Vector3.up * 1.7f, Ground(5f, 300f) + Vector3.up * 3f);
            WorldVista.Add(root, "Charcoal Kiln", Ground(-66f, 292f) + Vector3.up * 1.7f, Ground(-74.5f, 286.5f) + Vector3.up * 1f);
            WorldVista.Add(root, "Broken Arch", Ground(-110f, 348f) + Vector3.up * 1.7f, Ground(-110f, 340f) + Vector3.up * 2.5f);
            WorldVista.Add(root, "Lamplighter Post", Ground(-153f, 404f) + Vector3.up * 1.7f, Ground(-146.5f, 400f) + Vector3.up * 2f);
            WorldVista.Add(root, "Wolf Den", Ground(-192f, 233f) + Vector3.up * 1.7f, Ground(-201.5f, 230f) + Vector3.up * 1.5f);
            WorldVista.Add(root, "Tukorto Jetty", Ground(118f, 338f) + Vector3.up * 1.7f, Ground(150f, 352f) + Vector3.up * 0.5f);
            Vector2 jettyEnd = JettyStart + JettyAxis * (JettyLength - 3f);
            WorldVista.Add(root, "Drowned Bell Tower", new Vector3(jettyEnd.x, lakeShore + 1.6f, jettyEnd.y), new Vector3(BellIsland.x, lakeShore + 5f, BellIsland.y));
            WorldVista.Add(root, "Watchtower Rise", Ground(40f, 432f) + Vector3.up * 1.7f, Ground(60f, 450f) + Vector3.up * 9f);
            WorldVista.Add(root, "Ash Field Cairns", Ground(-70f, 522f) + Vector3.up * 1.7f, Ground(-90f, 540f) + Vector3.up * 1.5f);
            WorldVista.Add(root, "Harvest Shrine", Ground(2f, 200f) + Vector3.up * 1.7f, Ground(-3f, 205f) + Vector3.up * 1.6f);
            WorldVista.Add(root, "North Shrine", Ground(1f, 475f) + Vector3.up * 1.7f, Ground(8f, 470f) + Vector3.up * 1.6f);
            WorldVista.Add(root, "Hollowgate From The Watchtower Road", Ground(-3f, 455f) + Vector3.up * 1.7f, new Vector3(0f, 60f, 670f));
            WorldVista.Add(root, "Hollowgate Between The Hills", Ground(1f, 525f) + Vector3.up * 1.7f, new Vector3(0f, 50f, 670f));
            WorldVista.Add(root, "Hollowgate Over The Gate", Ground(-2f, 572f) + Vector3.up * 1.7f, new Vector3(0f, 45f, 660f));
        }

        // ---- 2 Old Toll Gate: the toll keeper's desk on the north side ------------------------------------------------

        void BuildTollLedger()
        {
            Vector2 at = new Vector2(10f, 95.2f);
            Vector3 p = Settle("Toll Desk", at, 180f, new Vector2(1.8f, 0.9f), 1.15f, 0.05f);
            var rotation = Quaternion.Euler(0f, 180f, 0f);
            var b = DecorChunk(at.x, at.y);
            b.Push(p, rotation, Vector3.one);
            b.ShadedBox(new Vector3(0f, 1.02f, 0f), new Vector3(1.7f, 0.08f, 0.8f), Pal.WoodDark, rng, 0.32f, 0.4f);
            foreach (float sx in new[] { -0.75f, 0.75f })
            foreach (float sz in new[] { -0.32f, 0.32f })
                b.ShadedBox(new Vector3(sx, 0.5f, sz), new Vector3(0.1f, 1f, 0.1f), Pal.WoodDark, rng, 0.28f, 0.34f);
            // The ledger lies open; every name on its last pages is struck through.
            foreach (float side in new[] { -1f, 1f })
            {
                b.Push(new Vector3(side * 0.21f, 1.08f, 0f), Quaternion.Euler(0f, 0f, side * -4f), Vector3.one);
                b.SetColor(Pal.FlowerWhite, 0.42f);
                b.Box(Vector3.zero, new Vector3(0.4f, 0.04f, 0.52f));
                b.SetColor(Pal.Black, 0.2f);
                for (int line = 0; line < 7; line++)
                    b.Box(new Vector3(0f, 0.022f, -0.2f + line * 0.06f), new Vector3(0.3f, 0.005f, 0.012f));
                b.Pop();
            }
            b.SetColor(Pal.Leather, 0.3f);
            b.Box(new Vector3(0f, 1.07f, 0f), new Vector3(0.88f, 0.03f, 0.58f));
            b.SetColor(Pal.Black, 0.12f);
            b.Prism(new Vector3(0.62f, 1.06f, 0.18f), 0.06f, 0.05f, 0.09f, 6);
            WayfarerMeshes.Beam(b, new Vector3(0.62f, 1.1f, 0.18f), new Vector3(0.7f, 1.32f, 0.26f), 0.015f, Pal.Thatch);
            b.ShadedBox(new Vector3(0f, 0.28f, -0.75f), new Vector3(0.45f, 0.56f, 0.4f), Pal.Wood, rng, 0.35f, 0.45f);
            b.Pop();
            AddBoxCollider("Toll Desk", p + Vector3.up * 0.55f, rotation, new Vector3(1.7f, 1.1f, 0.8f));
            WorldVista.Inspect(root, "Toll Ledger", p + Vector3.up * 1.12f, new Vector3(0.9f, 0.26f, 0.6f), GameText.TollLedgerLore);
        }

        // ---- 3 Patkós Stable ---------------------------------------------------------------------------------------

        (Vector3 at, Quaternion rotation) Building(string name, Vector2 centre, float yaw, float width, float depth, float wallHeight, bool tiles)
        {
            Vector3 p = Settle(name, centre, yaw, new Vector2(width, depth), 0.6f + wallHeight + depth * 0.55f);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = DecorChunk(p.x, p.z);
            b.Push(p, rotation, Vector3.one);
            PropMeshes.House(b, rng, width, depth, wallHeight, tiles);
            b.Pop();
            AddBoxCollider(name, p + Vector3.up * (0.6f + wallHeight) * 0.5f, rotation, new Vector3(width + 0.3f, 0.6f + wallHeight + 0.4f, depth + 0.3f));
            AddBoxCollider(name + " Roof", p + Vector3.up * (0.6f + wallHeight + depth * 0.25f), rotation, new Vector3(width + 0.6f, depth * 0.5f, depth * 0.6f));
            return (p, rotation);
        }

        /// <summary>A wide plank door on a building's front, for the stable and the barn.</summary>
        void WideDoor(Vector3 p, Quaternion rotation, float x, float depth, float width, float height)
        {
            var b = DecorChunk(p.x, p.z);
            b.Push(p, rotation, Vector3.one);
            // In front of the house's own painted door and windows, which reach 0.12 m out from the wall.
            b.SetColor(Pal.Black, 0.16f);
            b.Box(new Vector3(x, 0.6f + height * 0.5f, -depth * 0.5f - 0.13f), new Vector3(width, height, 0.06f));
            for (int plank = 0; plank < 4; plank++)
                b.ShadedBox(new Vector3(x - width * 0.5f + (plank + 0.5f) * width / 8f, 0.6f + height * 0.5f, -depth * 0.5f - 0.17f),
                    new Vector3(width / 8f - 0.02f, height, 0.05f), Pal.WoodDark, rng, 0.3f, 0.4f);
            b.SetColor(Pal.WoodDark, 0.26f);
            b.Box(new Vector3(x, 0.6f + height + 0.08f, -depth * 0.5f - 0.17f), new Vector3(width + 0.3f, 0.16f, 0.12f));
            b.Pop();
            Doorway(p + rotation * new Vector3(x, 0f, -depth * 0.5f - 0.2f));
        }

        /// <summary>Marks the ground before a door, which the validator keeps clear of people.</summary>
        void Doorway(Vector3 at)
        {
            var marker = new GameObject("Doorway").transform;
            marker.SetParent(root, false);
            marker.position = at;
        }

        /// <summary>A straight fence of whole sections, one collider for the side, and a post at its far end.</summary>
        void Fence(Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            int sections = Mathf.Max(1, Mathf.RoundToInt(length / 2.4f));
            for (int i = 0; i < sections; i++)
            {
                Vector3 a = Ground(Vector2.Lerp(from, to, i / (float)sections)), c = Ground(Vector2.Lerp(from, to, (i + 1f) / sections));
                Vector3 delta = c - a;
                var chunk = DecorChunk(a.x, a.z);
                chunk.Push(a + Vector3.down * 0.05f, Quaternion.FromToRotation(Vector3.right, new Vector3(delta.x, delta.y * 0.6f, delta.z).normalized), Vector3.one);
                PropMeshes.FenceSection(chunk, rng, delta.magnitude, false);
                chunk.Pop();
            }
            Vector3 end = Ground(to);
            DecorChunk(end.x, end.z).ShadedBox(end + new Vector3(0f, 0.55f, 0f), new Vector3(0.13f, 1.2f, 0.13f), Pal.WoodDark, rng, 0.35f, 0.55f);
            Vector3 mid = Ground((from + to) * 0.5f);
            Vector2 d = to - from;
            AddBoxCollider("Paddock Fence", mid + Vector3.up * 0.6f, Quaternion.Euler(0f, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0f), new Vector3(0.2f, 1.2f, length));
        }

        void BuildStable()
        {
            // The stable faces the road; the paddock lies east of it, its gate towards the stable door.
            var (p, rotation) = Building("Patkos Stable", new Vector2(32f, 118f), 90f, 10f, 6f, 3.2f, false);
            WideDoor(p, rotation, 1.6f, 6f, 2.6f, 2.5f);

            const float x0 = 37f, x1 = 55f, z0 = 113f, z1 = 125f;
            Fence(new Vector2(x0, z0), new Vector2(x1, z0));
            Fence(new Vector2(x1, z0), new Vector2(x1, z1));
            Fence(new Vector2(x1, z1), new Vector2(x0, z1));
            Fence(new Vector2(x0, z1), new Vector2(x0, 120.4f));
            Fence(new Vector2(x0, 117.8f), new Vector2(x0, z0));

            // Three horses standing in the paddock, a mesh per coat; the stable hand's troughs along the fence.
            Transform paddock = new GameObject("Paddock Horses").transform;
            paddock.SetParent(root, false);
            foreach (var (x, z, yaw, coat, mane) in new[]
            {
                (42.5f, 117.5f, 210f, Pal.Leather, Pal.Black), (48.5f, 121.5f, 20f, Pal.FurDark, Pal.Black), (51.5f, 116.5f, 290f, Pal.FurGrey, Pal.FurDark),
            })
            {
                // The placeholder pack horses drew from the shared random sequence: keep drawing, so nothing built after moves.
                WayfarerMeshes.PackHorse(new MeshBuilder(), rng);
                var horseRotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 hoof = new Vector3(x, Span(new Vector2(x, z), yaw, new Vector2(0.8f, 1.6f)).low - 0.03f, z);
                if (!paddockHorses.TryGetValue(coat, out Mesh mesh))
                {
                    var b = new MeshBuilder();
                    HorseMeshes.Standing(b, coat, mane);
                    paddockHorses[coat] = mesh = GeneratedAssets.SaveMesh(b, "Horse_Paddock_" + coat);
                }
                CreateMeshObject("Paddock Horse " + coat, mesh, worldMaterial, hoof, horseRotation, paddock);
                AddBoxCollider("Paddock Horse", hoof + horseRotation * new Vector3(0f, 1.15f, -0.3f), horseRotation, new Vector3(0.7f, 1.7f, 2.7f));
            }
            foreach (var (x, z, yaw) in new[] { (40.5f, 123.9f, 0f), (53.9f, 119.5f, 90f) })
            {
                var troughRotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 t = Settle("Paddock Trough", new Vector2(x, z), yaw, new Vector2(1.9f, 0.7f), 0.55f, 0.05f);
                var b = DecorChunk(x, z);
                b.Push(t, troughRotation, Vector3.one);
                b.ShadedBox(new Vector3(0f, 0.3f, 0f), new Vector3(1.9f, 0.5f, 0.7f), Pal.Wood, rng, 0.34f, 0.44f);
                b.SetColor(Pal.Water, 0.35f);
                b.Box(new Vector3(0f, 0.52f, 0f), new Vector3(1.7f, 0.04f, 0.5f));
                b.Pop();
                AddBoxCollider("Paddock Trough", t + Vector3.up * 0.3f, troughRotation, new Vector3(1.9f, 0.6f, 0.7f));
            }
            AddTree(haystackKind, Ground(53.2f, 123.2f) + Vector3.down * 0.1f, 20f, 0.55f, true, 1.5f);
            clearings.Add((new Vector2(53.2f, 123.2f), 1.5f));
        }

        // ---- 4 Kévés Farm and the burned farm ------------------------------------------------------------------------

        void BuildFarm()
        {
            Building("Keves Farmhouse", new Vector2(126f, 210f), 0f, 9f, 6f, 3.3f, true);
            var (barn, barnRotation) = Building("Keves Barn", new Vector2(149f, 197f), 90f, 12f, 8f, 4.4f, false);
            WideDoor(barn, barnRotation, 0f, 8f, 3.4f, 3.3f);
            Building("Keves Granary", new Vector2(128f, 186f), 180f, 5f, 4f, 2.4f, true);

            Vector3 well = Settle("Keves Well", new Vector2(135f, 208.5f), 0f, new Vector2(2.8f, 2.4f), 3.8f, 0.05f);
            var b = DecorChunk(well.x, well.z);
            b.Push(well, Quaternion.identity, Vector3.one);
            WayfarerMeshes.Well(b, rng);
            b.Pop();
            AddBoxCollider("Farm Well", well + Vector3.up * 1.8f, Quaternion.identity, new Vector3(2.75f, 3.6f, 2.4f));
            Vector3 wood = Ground(120.3f, 209f);
            b = DecorChunk(wood.x, wood.z);
            b.Push(wood, Quaternion.identity, Vector3.one);
            WayfarerMeshes.LogPile(b, rng, 2.4f, 4);
            b.Pop();
            AddBoxCollider("Farm Woodpile", wood + Vector3.up * 0.55f, Quaternion.identity, new Vector3(1.5f, 1.1f, 2.4f));

            // Haystacks by the yard and out in the fields, where the reapers have been.
            foreach (Vector2 spot in new[]
            {
                new Vector2(141.5f, 210.5f), new Vector2(144.3f, 213f), new Vector2(100f, 214f), new Vector2(106f, 224f), new Vector2(160f, 222f),
                new Vector2(170f, 184f), new Vector2(88f, 252f), new Vector2(64f, 262f), new Vector2(36f, 214f), new Vector2(-22f, 182f),
                new Vector2(150f, 250f), new Vector2(118f, 266f), new Vector2(20f, 250f), new Vector2(-60f, 230f), new Vector2(-40f, 268f),
            })
            {
                if (PathDistance(spot.x, spot.y) < 6f) continue;
                AddTree(haystackKind, Ground(spot) + Vector3.down * 0.1f, R(0f, 360f), R(0.85f, 1.1f), true, 1.5f);
                clearings.Add((spot, 2.6f));
            }
        }

        void BuildBurnedFarm()
        {
            Vector2 at = new Vector2(-112f, 172f);
            Vector3 p = Settle("Burned Farm Walls", at, 15f, new Vector2(9f, 7f), 1.1f, 0.1f);
            var rotation = Quaternion.Euler(0f, 15f, 0f);
            var b = DecorChunk(at.x, at.y);
            b.Push(p, rotation, Vector3.one);
            // What fire leaves of a farmhouse: a ring of footing stones, gaps where the door and the windows were.
            foreach (var (from, to) in new[]
            {
                (new Vector2(-4.5f, -3.5f), new Vector2(-1.2f, -3.5f)), (new Vector2(0.3f, -3.5f), new Vector2(4.5f, -3.5f)),
                (new Vector2(4.5f, -3.5f), new Vector2(4.5f, 3.5f)), (new Vector2(4.5f, 3.5f), new Vector2(-4.5f, 3.5f)), (new Vector2(-4.5f, 3.5f), new Vector2(-4.5f, -3.5f)),
            })
            {
                float length = Vector2.Distance(from, to);
                Vector2 dir = (to - from) / length;
                for (float s = 0f; s < length - 0.2f; s += 1.1f)
                {
                    Vector2 c = from + dir * (s + 0.55f);
                    int rows = 1 + (int)(Mathf.PerlinNoise(c.x * 0.7f, c.y * 0.7f) * 2.4f);
                    for (int row = 0; row < rows; row++)
                    {
                        b.Push(new Vector3(c.x, 0.3f + row * 0.45f, c.y), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)) * Quaternion.Euler(R(-3f, 3f), R(-5f, 5f), 0f), Vector3.one);
                        b.ShadedBox(Vector3.zero, new Vector3(0.6f, 0.44f, 1.05f), row == rows - 1 ? Pal.RockDark : Pal.Stone, rng, 0.18f, 0.32f);
                        b.Pop();
                    }
                }
                Vector2 mid = (from + to) * 0.5f;
                AddBoxCollider("Burned Farm Wall", p + rotation * new Vector3(mid.x, 0.55f, mid.y), rotation * Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)),
                    new Vector3(0.6f, 1.1f, length));
            }
            // The chimney is all that stands.
            for (int row = 0; row < 9; row++)
                PropMeshes.StoneBlock(b, rng, new Vector3(-3.2f + (row > 6 ? 0.15f : 0f), 0.25f + row * 0.5f, 2.6f), new Vector3(1.3f - row * 0.03f, 0.5f, 1.1f - row * 0.02f), false);
            b.SetColor(Pal.Black, 0.18f);
            b.Box(new Vector3(-3.2f, 1.1f, 2.03f), new Vector3(0.7f, 0.9f, 0.05f));
            for (int crow = 0; crow < 3; crow++)
            {
                Vector3 perch = new Vector3(-3.6f + crow * 0.4f, 4.75f, 2.5f + crow % 2 * 0.2f);
                b.SetColor(Pal.Black, 0.22f);
                b.Box(perch + Vector3.up * 0.12f, new Vector3(0.12f, 0.16f, 0.3f));
                b.Box(perch + new Vector3(0f, 0.26f, -0.15f), new Vector3(0.09f, 0.09f, 0.1f));
                b.SetColor(Pal.Brass, 0.3f);
                b.Box(perch + new Vector3(0f, 0.25f, -0.23f), new Vector3(0.025f, 0.025f, 0.07f));
            }
            // Charred beams fallen across the floor and leaning on the walls.
            foreach (var (from, to) in new[]
            {
                (new Vector3(-4.2f, 0.9f, -2.8f), new Vector3(0.5f, 0.15f, 1.9f)), (new Vector3(4.4f, 1.1f, 3.2f), new Vector3(0.8f, 0.1f, -1.5f)),
                (new Vector3(-2f, 0.12f, -1f), new Vector3(3.2f, 0.2f, -2.6f)), (new Vector3(-3.6f, 3.4f, 2.3f), new Vector3(-0.5f, 0.15f, 0.2f)),
                (new Vector3(1.5f, 0.15f, 2.8f), new Vector3(3.9f, 1.4f, 2.9f)), (new Vector3(-1f, 0.1f, 0.6f), new Vector3(2.2f, 0.12f, 1.1f)),
            })
                WayfarerMeshes.Beam(b, from, to, 0.24f, Pal.Black);
            // The door lies in the ash with its bar still across it: it was barred from inside.
            b.Push(new Vector3(-0.45f, 0.08f, -4.6f), Quaternion.Euler(0f, 12f, 4f), Vector3.one);
            b.ShadedBox(Vector3.zero, new Vector3(1.05f, 0.1f, 2f), Pal.WoodDark, rng, 0.08f, 0.14f);
            b.SetColor(Pal.SteelDark, 0.25f);
            b.Box(new Vector3(0f, 0.08f, 0.2f), new Vector3(1.25f, 0.07f, 0.14f));
            b.Pop();
            b.Pop();
            Vector3 chimney = p + rotation * new Vector3(-3.2f, 2.25f, 2.6f);
            AddBoxCollider("Burned Farm Chimney", chimney, rotation, new Vector3(1.3f, 4.5f, 1.1f));
            footings.Add(("Burned Farm Chimney", new Vector3(chimney.x, Span(new Vector2(chimney.x, chimney.z), 15f, new Vector2(1.3f, 1.1f)).low - 0.05f, chimney.z), 15f, new Vector3(1.3f, 4.6f, 1.1f)));
            WorldVista.Inspect(root, "Barred Door", p + rotation * new Vector3(-0.45f, 0.3f, -4.6f), new Vector3(1.4f, 0.6f, 2.2f), GameText.BurnedFarmLore);
        }

        // ---- 5 Gallows Crossroads ----------------------------------------------------------------------------------

        void BuildCrossroads()
        {
            Vector3 sign = Settle("Crossroads Signpost", new Vector2(-3f, 309f), 0f, new Vector2(0.3f, 0.3f), 2.6f, 0.05f);
            var b = DecorChunk(sign.x, sign.z);
            b.Push(sign, Quaternion.identity, Vector3.one);
            PropMeshes.Signpost(b, rng);
            b.SetColor(Pal.Wood, 0.5f);
            b.Box(new Vector3(0f, 2.3f, 0.45f), new Vector3(0.05f, 0.22f, 0.8f));
            b.TriangleOutward(new Vector3(-0.026f, 2.41f, 0.85f), new Vector3(-0.026f, 2.19f, 0.85f), new Vector3(-0.026f, 2.3f, 1.02f), new Vector3(1f, 2.3f, 0.45f));
            b.TriangleOutward(new Vector3(0.026f, 2.41f, 0.85f), new Vector3(0.026f, 2.19f, 0.85f), new Vector3(0.026f, 2.3f, 1.02f), new Vector3(-1f, 2.3f, 0.45f));
            b.Pop();
            AddBoxCollider("Signpost", sign + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.2f, 2.4f, 0.2f));

            // The gibbet: a post, an arm over the verge and an empty cage on its chain.
            Vector3 g = Settle("Crossroads Gibbet", new Vector2(7f, 294f), 0f, new Vector2(0.4f, 0.4f), 5.2f, 0.1f);
            b = DecorChunk(g.x, g.z);
            b.Push(g, Quaternion.identity, Vector3.one);
            b.ShadedBox(new Vector3(0f, 2.6f, 0f), new Vector3(0.3f, 5.3f, 0.3f), Pal.WoodDark, rng, 0.24f, 0.32f);
            b.ShadedBox(new Vector3(0.9f, 5.05f, 0f), new Vector3(2.1f, 0.24f, 0.24f), Pal.WoodDark, rng, 0.24f, 0.32f);
            WayfarerMeshes.Beam(b, new Vector3(0.1f, 4.1f, 0f), new Vector3(1.1f, 5f, 0f), 0.16f);
            WayfarerMeshes.Beam(b, new Vector3(1.75f, 4.95f, 0f), new Vector3(1.75f, 4.05f, 0f), 0.04f, Pal.SteelDark);
            b.SetColor(Pal.SteelDark, 0.3f);
            b.Prism(new Vector3(1.75f, 2.3f, 0f), 0.42f, 0.42f, 0.06f, 8, false, false);
            b.Prism(new Vector3(1.75f, 3.9f, 0f), 0.42f, 0.1f, 0.18f, 8, false, false);
            for (int bar = 0; bar < 8; bar++)
            {
                float a = bar * Mathf.PI / 4f;
                b.Box(new Vector3(1.75f + Mathf.Cos(a) * 0.4f, 3.1f, Mathf.Sin(a) * 0.4f), new Vector3(0.035f, 1.6f, 0.035f));
            }
            // The board that named the crime has fallen, face down, against the post.
            b.Push(new Vector3(-0.35f, 0.32f, 0.12f), Quaternion.Euler(0f, 0f, 22f), Vector3.one);
            b.ShadedBox(Vector3.zero, new Vector3(0.06f, 0.6f, 0.9f), Pal.Wood, rng, 0.3f, 0.38f);
            b.Pop();
            b.Pop();
            AddBoxCollider("Gibbet Post", g + Vector3.up * 2.6f, Quaternion.identity, new Vector3(0.3f, 5.3f, 0.3f));
            WorldVista.Inspect(root, "Crossroads Gibbet", g + Vector3.up * 1.1f, new Vector3(1.1f, 2.2f, 1.1f), GameText.GibbetLore);
        }

        // ---- 6 Feketefenyves --------------------------------------------------------------------------------------

        void BuildForestPlaces()
        {
            // The charcoal kiln: an earth mound whose fire shows only at its vents and smoke hole (emissive, no light).
            Vector3 kiln = Settle("Charcoal Kiln", new Vector2(-74.5f, 286.5f), 0f, new Vector2(4.6f, 4.6f), 2.1f, 0.2f);
            var b = DecorChunk(kiln.x, kiln.z);
            b.Push(kiln, Quaternion.identity, Vector3.one);
            b.SetColor(Pal.DirtDark, 0.3f);
            b.Prism(Vector3.zero, 2.3f, 1.7f, 1.05f, 10, false, false);
            b.SetColor(Pal.DirtDark, 0.25f);
            b.Prism(Vector3.up * 1.05f, 1.7f, 0.5f, 0.95f, 10, false, false);
            b.SetColor(Pal.Black, 0.12f);
            b.Prism(Vector3.up * 2f, 0.5f, 0.42f, 0.1f, 10, false, true);
            b.SetColor(Pal.FireOrange, 0.62f);
            b.Prism(Vector3.up * 2.08f, 0.32f, 0.22f, 0.04f, 8, false, true);
            for (int vent = 0; vent < 7; vent++)
            {
                float a = vent * Mathf.PI * 2f / 7f + 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.Push(dir * 1.93f + Vector3.up * 0.68f, Quaternion.LookRotation(dir) * Quaternion.Euler(-30f, 0f, 0f), Vector3.one);
                b.SetColor(Pal.Lava, 0.55f);
                b.Box(Vector3.zero, new Vector3(0.34f, 0.16f, 0.1f));
                b.Pop();
            }
            b.Pop();
            AddBoxCollider("Charcoal Kiln", kiln + Vector3.up * 1f, Quaternion.identity, new Vector3(3.8f, 2f, 3.8f));
            foreach (var (x, z, yaw, length) in new[] { (-71f, 283.2f, 40f, 1.6f), (-62.5f, 303.2f, 30f, 2.2f) })
            {
                Vector3 pile = Ground(x, z);
                b = DecorChunk(x, z);
                b.Push(pile, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                WayfarerMeshes.LogPile(b, rng, length, 3);
                b.Pop();
                AddBoxCollider("Log Pile", pile + Vector3.up * 0.45f, Quaternion.Euler(0f, yaw, 0f), new Vector3(1.2f, 0.9f, length));
            }
            // The woodcutter's block, the axe left in it.
            Vector3 block = Ground(-58.8f, 300f);
            b = DecorChunk(block.x, block.z);
            b.Push(block, Quaternion.Euler(0f, 25f, 0f), Vector3.one);
            b.SetColor(Pal.Bark, 0.35f);
            b.Prism(Vector3.down * 0.15f, 0.38f, 0.34f, 0.75f, 8, false, false);
            b.SetColor(Pal.Wood, 0.62f);
            b.Prism(Vector3.up * 0.6f, 0.34f, 0.34f, 0.02f, 8, false, true);
            WayfarerMeshes.Beam(b, new Vector3(0.05f, 0.62f, 0f), new Vector3(0.42f, 1.18f, 0f), 0.05f, Pal.Wood);
            b.SetColor(Pal.SteelDark, 0.45f);
            b.Box(new Vector3(0.02f, 0.66f, 0f), new Vector3(0.22f, 0.12f, 0.04f));
            b.Pop();
            AddBoxCollider("Chopping Block", block + Vector3.up * 0.3f, Quaternion.identity, new Vector3(0.7f, 0.6f, 0.7f));

            BuildBrokenArch();

            // The lamplighter's post: a lantern that burns by the road with no one living near.
            Vector3 post = Settle("Lamplighter Post", new Vector2(-146.5f, 400f), 180f, new Vector2(0.3f, 0.3f), 2.9f, 0.05f);
            b = DecorChunk(post.x, post.z);
            b.Push(post, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            PropMeshes.LanternPost(b, rng);
            WayfarerMeshes.Beam(b, new Vector3(0.35f, 0f, -0.3f), new Vector3(0.08f, 1.45f, -0.08f), 0.045f, Pal.Wood);
            b.Pop();
            AddBoxCollider("Lantern Post", post + Vector3.up * 1.4f, Quaternion.identity, new Vector3(0.22f, 2.9f, 0.22f));
            WorldVista.Inspect(root, "Lamplighter Post", post + Vector3.up * 1.3f, new Vector3(1f, 2.6f, 1f), GameText.LamplighterLore);

            // The wolf den: a horseshoe of boulders open to the east, a black mouth and gnawed bones before it.
            // The centre boulder stays under 1.8 m from its middle, so the mouth and the discovery sit clear of it.
            Vector2 den = new Vector2(-201.5f, 230f);
            PlaceRock(den.x, den.y, 2.2f, true);
            foreach (float angle in new[] { 100f, 140f, 180f, 220f, 260f })
            {
                float a = angle * Mathf.Deg2Rad;
                PlaceRock(den.x + Mathf.Cos(a) * 3f, den.y + Mathf.Sin(a) * 3f, R(2.4f, 3.2f), true);
            }
            Vector3 mouth = Ground(den.x + 1.4f, den.y);
            b = DecorChunk(mouth.x, mouth.z);
            // A low dark opening into the boulder's face, framed by two jamb stones under a lintel, so it reads as a hole.
            b.SetColor(Pal.Black, 0.06f);
            b.Box(mouth + new Vector3(-0.15f, 0.38f, 0f), new Vector3(0.7f, 0.76f, 1.2f));
            b.SetColor(Pal.RockDark, 0.3f);
            foreach (float side in new[] { -1f, 1f })
                b.Box(mouth + new Vector3(0f, 0.36f, side * 0.74f), new Vector3(0.62f, 0.84f, 0.32f));
            b.SetColor(Pal.RockDark, 0.34f);
            b.Push(mouth + new Vector3(0.02f, 0.9f, 0f), Quaternion.Euler(4f, 0f, -5f), Vector3.one);
            b.Box(Vector3.zero, new Vector3(0.74f, 0.28f, 1.86f));
            b.Pop();
            b.SetColor(Pal.DirtDark, 0.2f);
            b.Box(mouth + new Vector3(0.5f, 0.01f, 0f), new Vector3(0.7f, 0.04f, 1.1f));
            b.Push(Ground(den.x + 3.4f, den.y + 0.6f), Quaternion.Euler(0f, 30f, 0f), Vector3.one);
            PropMeshes.BonePile(b, rng);
            b.Pop();
            AddBoxCollider("Wolf Den Mouth", mouth + Vector3.up * 0.6f, Quaternion.identity, new Vector3(0.6f, 1.2f, 1.6f));
            WorldVista.Inspect(root, "Wolf Den", Ground(den.x + 2.3f, den.y) + Vector3.up * 0.75f, new Vector3(0.9f, 1.4f, 1.8f), GameText.WolfDenLore);
        }

        void BuildBrokenArch()
        {
            Vector2 at = new Vector2(-110f, 340f);
            const float radius = 3.4f;
            Vector3 left = Settle("Broken Arch Pier", at + new Vector2(-radius, 0f), 0f, new Vector2(1.3f, 1.5f), 5.6f, 0.2f);
            Vector3 right = Settle("Broken Arch Pier", at + new Vector2(radius, 0f), 0f, new Vector2(1.3f, 1.5f), 3.4f, 0.2f);
            float spring = Mathf.Max(left.y, right.y) + 5f;
            var b = DecorChunk(at.x, at.y);
            b.ShadedBox(new Vector3(left.x, (left.y + spring) * 0.5f, left.z), new Vector3(1.3f, spring - left.y, 1.5f), Pal.StoneMoss, rng, 0.26f, 0.42f);
            float rightTop = spring - 1.8f;
            b.ShadedBox(new Vector3(right.x, (right.y + rightTop) * 0.5f, right.z), new Vector3(1.3f, rightTop - right.y, 1.5f), Pal.StoneMoss, rng, 0.26f, 0.42f);
            AddBoxCollider("Arch Pier", new Vector3(left.x, (left.y + spring) * 0.5f, left.z), Quaternion.identity, new Vector3(1.3f, spring - left.y, 1.5f));
            AddBoxCollider("Arch Pier", new Vector3(right.x, (right.y + rightTop) * 0.5f, right.z), Quaternion.identity, new Vector3(1.3f, rightTop - right.y, 1.5f));
            // Half the arch still springs from the left pier; the rest lies in the moss below.
            for (int stone = 5; stone <= 10; stone++)
            {
                float a = stone * Mathf.PI / 10f;
                b.Push(new Vector3(at.x + Mathf.Cos(a) * radius, spring + Mathf.Sin(a) * radius, at.y), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(0.9f, 1.1f, 1.45f), Pal.StoneMoss, rng, 0.28f, 0.46f);
                b.Pop();
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 fallen = Ground(at.x + 2.2f + i * 0.8f, at.y + (i % 2 == 0 ? 1.6f : -1.7f));
                b.Push(fallen + Vector3.up * 0.3f, Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-15f, 15f)), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(0.9f, 0.8f, 1.3f), Pal.StoneMoss, rng, 0.26f, 0.42f);
                b.Pop();
            }
            // The soldier sits against the left pier under moss: helmet, shoulders, the hilt of a sword still in his hand.
            Vector3 seat = Ground(at.x - 1.5f, at.y);
            b.Rock(seat + new Vector3(0f, 0.3f, 0f), new Vector3(1.5f, 0.9f, 1.2f), rng, Pal.GrassOlive, 0.22f, 0.32f, 0.15f);
            b.SetColor(Pal.SteelDark, 0.24f);
            b.Prism(seat + new Vector3(-0.1f, 0.95f, 0f), 0.22f, 0.15f, 0.3f, 8);
            b.SetColor(Pal.GrassOlive, 0.3f);
            b.Prism(seat + new Vector3(-0.1f, 1.24f, 0f), 0.16f, 0.05f, 0.1f, 8, false);
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(Pal.SteelDark, 0.22f);
                b.Box(seat + new Vector3(-0.1f, 0.78f, side * 0.32f), new Vector3(0.32f, 0.14f, 0.26f));
            }
            for (int fern = 0; fern < 6; fern++)
            {
                b.SetColor(Pal.GrassOlive, R(0.3f, 0.45f));
                b.Push(seat + new Vector3(R(-0.7f, 0.7f), 0.2f, R(-0.6f, 0.6f)), Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f)), Vector3.one);
                b.Prism(Vector3.zero, 0.12f, 0.02f, R(0.4f, 0.7f), 4, false, false);
                b.Pop();
            }
            var sword = new MeshBuilder();
            PropMeshes.Sword(sword, Pal.RockWarm, 0.3f);
            b.Append(sword, Matrix4x4.TRS(seat + new Vector3(0.55f, 1.1f, 0.15f), Quaternion.Euler(8f, 20f, -6f) * Quaternion.Euler(180f, 0f, 0f), Vector3.one * 1.5f));
            AddBoxCollider("Overgrown Soldier", seat + Vector3.up * 0.4f, Quaternion.identity, new Vector3(1.4f, 0.8f, 1.1f));
            WorldVista.Inspect(root, "Overgrown Soldier", seat + new Vector3(0.3f, 0.9f, 0f), new Vector3(1.7f, 1.4f, 1.4f), GameText.OvergrownSoldierLore);
        }

        // ---- 7 Tükörtó ---------------------------------------------------------------------------------------------

        void BuildLake()
        {
            float water = lakeShore - 0.6f, deckTop = lakeShore - 0.1f, bed = lakeShore - LakeDepth - 1f;

            // The water in rings of quads rather than one fan, so the fog, worked out per vertex, lies evenly on it.
            var surface = new MeshBuilder();
            surface.SetColor(Pal.Water, 0.5f);
            float[] rings = { 0f, 0.3f, 0.55f, 0.75f, 0.9f, 1f };
            const int segments = 40;
            Vector3 On(float r, int s)
            {
                float a = s * Mathf.PI * 2f / segments;
                return new Vector3(LakeCenter.x + Mathf.Cos(a) * LakeRadii.x * r, water, LakeCenter.y + Mathf.Sin(a) * LakeRadii.y * r);
            }
            for (int k = 0; k < rings.Length - 1; k++)
            for (int s = 0; s < segments; s++)
            {
                if (k == 0) { surface.Triangle(On(0f, 0), On(rings[1], s), On(rings[1], s + 1), Vector3.up); continue; }
                surface.Triangle(On(rings[k], s), On(rings[k + 1], s), On(rings[k + 1], s + 1), Vector3.up);
                surface.Triangle(On(rings[k], s), On(rings[k + 1], s + 1), On(rings[k], s + 1), Vector3.up);
            }
            CreateMeshObject("Tukorto Water", GeneratedAssets.SaveMesh(surface, "Tukorto_Water"), worldMaterial, Vector3.zero, Quaternion.identity, root, false);

            // There is no swimming: a closed ring of walls stands on the bank, open only where the jetty's rails take over.
            Vector2 across = new Vector2(-JettyAxis.y, JettyAxis.x);
            var shore = new GameObject("Lake Shore").transform;
            shore.SetParent(colliderRoot, false);
            const int pieces = 180;
            const float ringRatio = 0.985f;
            Vector2 Ring(int i)
            {
                float a = i * Mathf.PI * 2f / pieces;
                return LakeCenter + new Vector2(Mathf.Cos(a) * LakeRadii.x, Mathf.Sin(a) * LakeRadii.y) * ringRatio;
            }
            for (int i = 0; i < pieces; i++)
            {
                Vector2 a = Ring(i), c = Ring(i + 1);
                bool crossesJetty = false;
                foreach (Vector2 q in new[] { a, (a + c) * 0.5f, c })
                {
                    Vector2 rel = q - JettyStart;
                    if (Mathf.Abs(Vector2.Dot(rel, across)) < JettyHalfWidth + 0.2f && Vector2.Dot(rel, JettyAxis) > -6f && Vector2.Dot(rel, JettyAxis) < 12f) crossesJetty = true;
                }
                if (crossesJetty || Vector2.Dot(a - JettyStart, across) * Vector2.Dot(c - JettyStart, across) < 0f && Vector2.Distance(a, JettyStart) < 12f) continue;
                Vector2 along = (c - a).normalized, inward = (LakeCenter - (a + c) * 0.5f).normalized;
                Vector2 middle = (a + c) * 0.5f + inward * 0.6f;
                var piece = new GameObject("Lake Shore");
                piece.transform.SetParent(shore, false);
                piece.transform.SetPositionAndRotation(new Vector3(middle.x, (bed + lakeShore + 3.5f) * 0.5f, middle.y),
                    Quaternion.LookRotation(new Vector3(along.x, 0f, along.y)));
                piece.AddComponent<BoxCollider>().size = new Vector3(1.2f, lakeShore + 3.5f - bed, Vector2.Distance(a, c) + 0.6f);
            }

            // The jetty: planks on two stringers, piles into the bed, a rail each side; the rails' colliders are wide enough to
            // meet the shore ring's open ends.
            float yaw = Mathf.Atan2(JettyAxis.x, JettyAxis.y) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 origin = new Vector3(JettyStart.x, 0f, JettyStart.y);
            var b = DecorChunk(JettyStart.x + JettyAxis.x * 12f, JettyStart.y + JettyAxis.y * 12f);
            b.Push(origin, rotation, Vector3.one);
            for (float s = -0.8f; s < JettyLength; s += 0.55f)
            {
                b.SetColor(Pal.Wood, R(0.38f, 0.52f));
                b.Box(new Vector3(R(-0.05f, 0.05f), deckTop - 0.04f, s), new Vector3(JettyHalfWidth * 2f, 0.08f, 0.5f));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                WayfarerMeshes.Beam(b, new Vector3(side * 1.1f, deckTop - 0.2f, -0.8f), new Vector3(side * 1.1f, deckTop - 0.2f, JettyLength), 0.2f);
                for (float s = 2f; s < JettyLength; s += 4f)
                {
                    b.SetColor(Pal.WoodDark, 0.3f);
                    b.Prism(new Vector3(side * 1.45f, bed + 0.5f, s), 0.15f, 0.13f, deckTop + 1f - bed - 0.5f, 6);
                }
                WayfarerMeshes.Beam(b, new Vector3(side * 1.45f, deckTop + 0.95f, 2f), new Vector3(side * 1.45f, deckTop + 0.95f, JettyLength - 0.2f), 0.1f, Pal.Wood);
            }
            // The mooring post at the end, wound with knotted ropes.
            Vector3 mooring = new Vector3(1.15f, deckTop, JettyLength - 0.4f);
            b.SetColor(Pal.WoodDark, 0.28f);
            b.Prism(mooring + Vector3.down * 0.3f, 0.16f, 0.14f, 1.5f, 6);
            for (int knot = 0; knot < 5; knot++)
            {
                b.SetColor(Pal.Thatch, 0.3f + knot * 0.05f);
                b.Prism(mooring + Vector3.up * (0.45f + knot * 0.16f), 0.2f, 0.2f, 0.07f, 6);
            }
            b.Pop();
            Vector3 World(float x, float y, float z) => origin + rotation * new Vector3(x, 0f, z) + Vector3.up * y;
            AddBoxCollider("Jetty Deck", World(0f, deckTop - 0.25f, (JettyLength - 1f) * 0.5f), rotation, new Vector3(JettyHalfWidth * 2f, 0.5f, JettyLength + 1f));
            float wallMid = (bed + deckTop + 2.5f) * 0.5f, wallHeight = deckTop + 2.5f - bed;
            foreach (float side in new[] { -1f, 1f })
                AddBoxCollider("Jetty Rail", World(side * 2.95f, wallMid, (JettyLength + 0.6f) * 0.5f + 0.3f), rotation, new Vector3(2.7f, wallHeight, JettyLength - 0.3f));
            AddBoxCollider("Jetty End", World(0f, wallMid, JettyLength + 0.3f), rotation, new Vector3(8.6f, wallHeight, 0.6f));
            WorldVista.Inspect(root, "Jetty Mooring Post", World(mooring.x, deckTop + 0.75f, mooring.z), new Vector3(0.6f, 1.3f, 0.6f), GameText.JettyLore);

            // The drowned bell tower: sunk and leaning on its island, seen from the shore and never reached.
            Vector3 tower = Settle("Drowned Bell Tower", BellIsland, 20f, new Vector2(3.6f, 3.6f), 13.5f, 1.6f);
            b = DecorChunk(BellIsland.x, BellIsland.y);
            b.Push(tower, Quaternion.Euler(6f, 20f, 4f), Vector3.one);
            b.ShadedBox(new Vector3(0f, 4.5f, 0f), new Vector3(3.4f, 9f, 3.4f), Pal.StoneMoss, rng, 0.28f, 0.42f);
            foreach (float sx in new[] { -1.35f, 1.35f })
            foreach (float sz in new[] { -1.35f, 1.35f })
                b.ShadedBox(new Vector3(sx, 10.3f, sz), new Vector3(0.7f, 2.6f, 0.7f), Pal.Stone, rng, 0.3f, 0.42f);
            b.ShadedBox(new Vector3(0f, 11.8f, 0f), new Vector3(3.8f, 0.4f, 3.8f), Pal.Stone, rng, 0.32f, 0.4f);
            b.SetColor(Pal.RoofTile, 0.24f);
            b.Prism(new Vector3(0f, 12f, 0f), 2.7f, 0f, 3.2f, 4, false, false, Mathf.PI / 4f);
            b.SetColor(Pal.WoodDark, 0.25f);
            b.Box(new Vector3(0f, 10.9f, 0f), new Vector3(3f, 0.18f, 0.18f));
            b.SetColor(Pal.Brass, 0.36f);
            b.Prism(new Vector3(0f, 9.5f, 0f), 0.75f, 0.36f, 1.1f, 8);
            b.Prism(new Vector3(0f, 10.6f, 0f), 0.36f, 0f, 0.3f, 8, false);
            b.SetColor(Pal.Black, 0.1f);
            foreach (float y in new[] { 3.5f, 6.5f })
                b.Box(new Vector3(0f, y, -1.72f), new Vector3(0.5f, 1.1f, 0.06f));
            b.Pop();
        }

        // ---- 8 Watchtower ------------------------------------------------------------------------------------------

        void BuildWatchtower()
        {
            Vector2 at = new Vector2(60f, 450f);
            float yaw = YawFacing(at, new Vector2(55f, 448f));
            Vector3 p = Settle("Watchtower", at, yaw, new Vector2(6.8f, 6.8f), 19.5f, 0.3f);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = DecorChunk(at.x, at.y);
            b.Push(p, rotation, Vector3.one);
            const float face = 0.924f;   // an octagon's flat side, as a fraction of its corner radius
            b.SetColor(Pal.Stone, 0.4f);
            b.Prism(Vector3.zero, 3.4f, 3.15f, 1.6f, 8, false, true, Mathf.PI / 8f);
            b.SetColor(Pal.Stone, 0.5f);
            b.Prism(Vector3.up * 1.6f, 2.85f, 2.5f, 14.4f, 8, false, false, Mathf.PI / 8f);
            b.SetColor(Pal.Stone, 0.38f);
            b.Prism(Vector3.up * 16f, 2.5f, 3.3f, 0.9f, 8, false, false, Mathf.PI / 8f);
            b.SetColor(Pal.Stone, 0.46f);
            b.Prism(Vector3.up * 16.9f, 3.3f, 3.3f, 0.3f, 8, true, true, Mathf.PI / 8f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + Mathf.PI / 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.Push(dir * (3.3f * face - 0.25f) + Vector3.up * 17.75f, Quaternion.LookRotation(dir), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(1.3f, 1.1f, 0.5f), Pal.Stone, rng, 0.4f, 0.52f);
                b.Pop();
                if (i % 2 == 1) continue;
                foreach (float y in new[] { 6f, 10.5f, 14f })
                {
                    float r = (2.85f - (y - 1.6f) / 14.4f * 0.35f) * face;
                    b.Push(dir * (r + 0.02f) + Vector3.up * y, Quaternion.LookRotation(dir), Vector3.one);
                    b.SetColor(Pal.Black, 0.12f);
                    b.Box(Vector3.zero, new Vector3(0.24f, 1f, 0.08f));
                    b.Pop();
                }
            }
            // The brazier on the platform glows by its own faces; the valley's lights stay at three.
            b.Push(Vector3.up * 17.2f, Quaternion.identity, Vector3.one * 1.6f);
            PropMeshes.Brazier(b, rng);
            b.Pop();
            // The door faces the spur road, up a step onto the plinth; beside it the board of tallies.
            // The shaft's door-side face lies 2.61 m out at the door; door and board stand just proud of it.
            b.SetColor(Pal.Black, 0.14f);
            b.Box(new Vector3(0f, 2.75f, -2.63f), new Vector3(1.3f, 2.3f, 0.06f));
            b.ShadedBox(new Vector3(0f, 2.7f, -2.68f), new Vector3(1.05f, 2.15f, 0.05f), Pal.WoodDark, rng, 0.26f, 0.34f);
            b.ShadedBox(new Vector3(0f, 0.35f, -3.65f), new Vector3(1.6f, 0.9f, 0.9f), Pal.Stone, rng, 0.38f, 0.46f);
            b.ShadedBox(new Vector3(0f, 0.9f, -3.2f), new Vector3(1.6f, 0.7f, 0.5f), Pal.Stone, rng, 0.4f, 0.48f);
            Vector3 board = new Vector3(0.82f, 2.35f, -2.7f);
            b.ShadedBox(board, new Vector3(0.5f, 1.2f, 0.08f), Pal.Wood, rng, 0.36f, 0.42f);
            b.SetColor(Pal.Black, 0.16f);
            for (int row = 0; row < 5; row++)
            {
                Vector3 g = board + new Vector3(0f, 0.45f - row * 0.22f, -0.05f);
                for (int mark = 0; mark < 4; mark++)
                    b.Box(g + new Vector3(-0.09f + mark * 0.06f, 0f, 0f), new Vector3(0.018f, 0.15f, 0.015f));
                b.Push(g, Quaternion.Euler(0f, 0f, 55f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(0.018f, 0.26f, 0.015f));
                b.Pop();
            }
            b.Pop();
            Doorway(p + rotation * new Vector3(0f, 0f, -4.1f));
            var collider = new GameObject("Watchtower");
            collider.transform.SetParent(colliderRoot, false);
            collider.transform.position = p;
            var capsule = collider.AddComponent<CapsuleCollider>();
            capsule.radius = 3.15f;
            capsule.height = 19.5f;
            capsule.center = Vector3.up * 9.75f;
            WorldVista.Inspect(root, "Watchtower Tally", p + rotation * new Vector3(0.82f, 2.3f, -3.15f), new Vector3(0.9f, 1.4f, 0.8f), GameText.WatchtowerTallyLore);
        }

        // ---- 9 Ash Field -------------------------------------------------------------------------------------------

        void BuildAshField()
        {
            Vector2 at = new Vector2(-90f, 540f);
            Vector3 p = Settle("Ash Field Cairn", at, 0f, new Vector2(3.8f, 3.8f), 3.4f, 0.2f);
            var b = DecorChunk(at.x, at.y);
            b.Push(p, Quaternion.identity, Vector3.one);
            float y = 0f;
            foreach (float w in new[] { 3.6f, 3f, 2.4f, 1.8f, 1.2f })
            {
                float h = w * 0.45f;
                b.Rock(new Vector3(R(-0.1f, 0.1f), y + h * 0.25f, R(-0.1f, 0.1f)), new Vector3(w, h, w * 0.92f), rng, Pal.Rock, 0.26f, 0.42f, 0.18f);
                y += h * 0.62f;
            }
            // The order's standard through the stones: the broken sun on top.
            WayfarerMeshes.Beam(b, new Vector3(0.6f, 0.5f, 0.4f), new Vector3(0.62f, y + 2.4f, 0.42f), 0.12f);
            b.Push(new Vector3(0.62f, y + 2.6f, 0.3f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            b.SetColor(Pal.Brass, 0.38f);
            b.Prism(Vector3.zero, 0.55f, 0.55f, 0.07f, 12);
            b.SetColor(Pal.Black, 0.1f);
            b.Push(new Vector3(0f, -0.01f, 0f), Quaternion.Euler(0f, 35f, 0f), Vector3.one);
            b.Box(Vector3.zero, new Vector3(0.09f, 0.1f, 1.15f));
            b.Pop();
            b.Pop();
            b.Pop();
            AddBoxCollider("Ash Field Cairn", p + Vector3.up * 1.5f, Quaternion.identity, new Vector3(3.4f, 3f, 3.3f));
            WorldVista.Inspect(root, "Ash Field Cairn", p + new Vector3(0f, 1.2f, -2.1f), new Vector3(2f, 1.6f, 0.8f), GameText.AshFieldLore);

            // The order's dead in their ranks: rows of cairns across the ash, the trees around them dead too.
            for (float z = 516f; z < 568f; z += 8f)
            for (float x = -120f; x < -60f; x += 6.5f)
            {
                float jx = x + R(-1f, 1f), jz = z + R(-1f, 1f);
                if (Vector2.Distance(new Vector2(jx, jz), at) < 6f || Vector2.Distance(new Vector2(jx, jz), at) > 33f) continue;
                if (PathDistance(jx, jz) < 5f || SurfaceNormal(jx, jz).y < 0.85f || Crowded(jx, jz, 2.5f)) continue;
                AddTree(cairnKinds[rng.Next(cairnKinds.Length)], new Vector3(jx, SurfaceHeight(jx, jz) - 0.05f, jz), R(0f, 360f), R(0.8f, 1.15f), true, 0.65f);
            }
            int trees = 0;
            for (int attempt = 0; attempt < 400 && trees < 24; attempt++)
            {
                float a = R(0f, Mathf.PI * 2f), r = R(10f, 36f);
                float x = at.x + Mathf.Cos(a) * r, z = at.y + Mathf.Sin(a) * r;
                if (PathDistance(x, z) < 5f || SurfaceNormal(x, z).y < 0.8f || Crowded(x, z, 4f) || !Walkable(x, z, -3f)) continue;
                AddTree(deadTreeKinds[rng.Next(deadTreeKinds.Length)], new Vector3(x, SurfaceHeight(x, z) - 0.15f, z), R(0f, 360f), R(0.85f, 1.25f), true, 0.25f);
                trees++;
            }
        }

        // ---- Wayside shrines ---------------------------------------------------------------------------------------

        void Shrine(string name, Vector2 at, Vector2 road, bool figure, string lore)
        {
            float yaw = YawFacing(at, road);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 p = Settle(name, at, yaw, new Vector2(0.9f, 0.9f), 2.7f, 0.1f);
            var b = DecorChunk(at.x, at.y);
            b.Push(p, rotation, Vector3.one);
            b.ShadedBox(new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 0.9f), Pal.StoneMoss, rng, 0.32f, 0.44f);
            b.ShadedBox(new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 1.3f, 0.5f), Pal.Stone, rng, 0.4f, 0.5f);
            b.ShadedBox(new Vector3(0f, 2f, 0f), new Vector3(0.8f, 0.75f, 0.6f), Pal.Stone, rng, 0.42f, 0.52f);
            b.SetColor(Pal.Black, 0.12f);
            b.Box(new Vector3(0f, 2f, -0.28f), new Vector3(0.52f, 0.55f, 0.06f));
            foreach (float side in new[] { -1f, 1f })
            {
                b.Push(new Vector3(side * 0.24f, 2.55f, 0f), Quaternion.Euler(0f, 0f, side * -38f), Vector3.one);
                b.SetColor(Pal.RoofTile, 0.4f);
                b.Box(Vector3.zero, new Vector3(0.66f, 0.07f, 0.8f));
                b.Pop();
            }
            if (figure)
            {
                b.SetColor(Pal.Wood, 0.5f);
                b.Prism(new Vector3(0f, 1.74f, -0.2f), 0.09f, 0.06f, 0.36f, 6);
                b.Prism(new Vector3(0f, 2.1f, -0.2f), 0.06f, 0.05f, 0.1f, 6);
                foreach (float side in new[] { -1f, 1f })
                {
                    b.SetColor(Pal.Leather, 0.36f);
                    b.Box(new Vector3(side * 0.12f, 0.46f, -0.55f), new Vector3(0.09f, 0.08f, 0.18f));
                }
            }
            else
            {
                b.SetColor(Pal.Plaster, 0.62f);
                for (int stroke = 0; stroke < 4; stroke++)
                    b.Box(new Vector3(-0.15f + stroke * 0.1f, 1.05f + stroke % 2 * 0.06f, -0.26f), new Vector3(0.07f, 0.02f, 0.01f));
            }
            b.SetColor(Pal.Thatch, 0.5f);
            b.Prism(new Vector3(0f, 1.66f, -0.22f), 0.2f, 0.2f, 0.06f, 8, false, false);
            b.SetColor(Pal.Plaster, 0.6f);
            b.Prism(new Vector3(0.24f, 1.66f, -0.2f), 0.025f, 0.025f, 0.12f, 5);
            b.SetColor(Pal.FireYellow, 0.8f);
            b.Prism(new Vector3(0.24f, 1.78f, -0.2f), 0.02f, 0f, 0.06f, 4, false);
            b.Pop();
            AddBoxCollider(name, p + Vector3.up * 1.2f, rotation, new Vector3(0.8f, 2.4f, 0.6f));
            WorldVista.Inspect(root, name, p + rotation * new Vector3(0f, 1.9f, -0.55f), new Vector3(0.9f, 0.9f, 0.5f), lore);
        }

        // ---- 10 Approach to Üregkapu: Hollowgate's silhouette beyond the wall ------------------------------------------

        /// <summary>
        /// Walls, towers, roofs and the keep on the hills at z 640..700, in the haze's colours and few faces. Every piece reaches
        /// two metres below the lowest ground under it and the keep stands on a crag whose foot is buried, so from no reachable
        /// point does any of it hang in the air.
        /// </summary>
        void BuildHollowgateSilhouette()
        {
            void Block(string name, Vector2 at, float yaw, Vector2 size, float above, float shade, bool roof)
            {
                var (low, high) = Span(at, yaw, size);
                float bottom = low - 2f, height = high + above - bottom;
                footings.Add((name, new Vector3(at.x, bottom, at.y), yaw, new Vector3(size.x, height, size.y)));
                var b = DecorChunk(at.x, at.y);
                b.Push(new Vector3(at.x, bottom, at.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                b.ShadedBox(new Vector3(0f, height * 0.5f, 0f), new Vector3(size.x, height, size.y), Pal.Mountain, rng, shade - 0.03f, shade + 0.03f);
                if (roof)
                {
                    b.SetColor(Pal.Mountain, shade - 0.1f);
                    b.Prism(new Vector3(0f, height, 0f), size.x * 0.76f, 0f, size.x * 0.8f, 4, false, false, Mathf.PI / 4f);
                }
                b.Pop();
            }

            Vector2[] wall = { new Vector2(-9f, 642f), new Vector2(-26f, 643f), new Vector2(-50f, 647f), new Vector2(-78f, 655f) };
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < wall.Length - 1; i++)
                {
                    Vector2 a = new Vector2(wall[i].x * -side, wall[i].y), c = new Vector2(wall[i + 1].x * -side, wall[i + 1].y);
                    int parts = Mathf.CeilToInt(Vector2.Distance(a, c) / 8f);
                    float yaw = Mathf.Atan2(c.x - a.x, c.y - a.y) * Mathf.Rad2Deg;
                    for (int k = 0; k < parts; k++)
                        Block("Hollowgate Wall", Vector2.Lerp(a, c, (k + 0.5f) / parts), yaw, new Vector2(3f, Vector2.Distance(a, c) / parts + 0.4f), 13f, 0.3f, false);
                    Block("Hollowgate Tower", c, 0f, new Vector2(6.5f, 6.5f), 20f, 0.27f, true);
                }
                Block("Hollowgate Gate Tower", new Vector2(side * 6f, 641f), 0f, new Vector2(6f, 6f), 23f, 0.28f, true);
            }
            Block("Hollowgate Gatehouse", new Vector2(0f, 641.5f), 0f, new Vector2(6.2f, 5f), 16f, 0.25f, false);

            // Roofs inside the wall, short of the keep's crag, a window lit in some.
            for (float z = 653f; z < 676f; z += 9f)
            for (float x = -64f; x <= 64f; x += 12f)
            {
                float jx = x + R(-3f, 3f), jz = z + R(-2f, 2f);
                if (Mathf.Abs(jx) < 46f && jz > 660f || Mathf.Abs(jx) > 72f - (jz - 653f) * 0.4f) continue;
                float size = R(6f, 9f), above = R(6f, 9f);
                Block("Hollowgate Roofs", new Vector2(jx, jz), 0f, new Vector2(size, size), above, R(0.3f, 0.36f), true);
                if (rng.Next(3) == 0) continue;
                var b = DecorChunk(jx, jz);
                b.SetColor(Pal.Lantern, 0.6f);
                b.Box(new Vector3(jx + R(-1.5f, 1.5f), Span(new Vector2(jx, jz), 0f, new Vector2(size, size)).high + above * 0.6f, jz - size * 0.5f - 0.05f), new Vector3(0.9f, 1.2f, 0.1f));
            }

            // The keep, as it stands behind the city, on a crag wider than all its towers.
            Vector2 keep = new Vector2(0f, 684f);
            const float scale = 0.7f;
            float top = Span(keep, 0f, new Vector2(54f, 20f)).high + 4f;
            float footLow = Span(keep, 0f, new Vector2(84f, 48f)).low;
            float depth = top - footLow + 3f;
            footings.Add(("Hollowgate Keep Crag", new Vector3(keep.x, top - depth, keep.y), 0f, new Vector3(84f, depth + 40f * scale, 48f)));
            var castle = DecorChunk(keep.x, keep.y);
            castle.Push(new Vector3(keep.x, top, keep.y), Quaternion.identity, Vector3.one);
            TownMeshes.KeepCrag(castle, rng, new Vector2(27f, 10f), new Vector2(42f, 24f), depth, 0f);
            castle.Push(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            TownMeshes.Keep(castle, rng);
            castle.Pop();
            castle.Pop();
        }

        // ---- The people of the north land --------------------------------------------------------------------------

        /// <summary>
        /// Who lives here, at the places above. Csend sits at the jetty's end and the net mender works further along it, each
        /// off the deck's middle so the walk out stays open; the hired hand stands beside the barn's door rather than in it.
        /// </summary>
        void PlaceNorthPeople(ValleyLayout layout)
        {
            Vector2 across = new Vector2(-JettyAxis.y, JettyAxis.x);
            Vector3 OnDeck(float along, float side)
            {
                Vector2 p = JettyStart + JettyAxis * along + across * side;
                return new Vector3(p.x, lakeShore - 0.1f, p.y);
            }
            float jettyYaw = Mathf.Atan2(JettyAxis.x, JettyAxis.y) * Mathf.Rad2Deg;

            layout.People.Add(("stablemaster", Ground(24f, 114f), 270f, PeopleMeshes.Stablemaster));
            layout.People.Add(("farmer", Ground(124f, 196f), 200f, PeopleMeshes.Farmer));
            layout.People.Add(("fisher", OnDeck(22.6f, 1.05f), jettyYaw, PeopleMeshes.Fisher));
            layout.People.Add(("watchman", Ground(54f, 444f), 200f, PeopleMeshes.Watchman));
            layout.People.Add(("woodcutter", Ground(-60f, 300f), 90f, PeopleMeshes.Woodcutter));

            layout.Ambient.Add(("Stable Hand", Ground(44f, 112f), 0f, PeopleMeshes.StableHand, null));
            layout.Ambient.Add(("Farm Wife", Ground(134f, 206f), 15f, PeopleMeshes.FarmWife, null));
            foreach (var (x, z, yaw, i) in new[] { (95f, 230f, 300f, 1), (102f, 236f, 290f, 2), (110f, 228f, 310f, 3) })
                layout.Ambient.Add(($"Reaper {i}", Ground(x, z), yaw, PeopleMeshes.Reaper, "Reaper"));
            layout.Ambient.Add(("Hired Hand", Ground(143f, 200.4f), 270f, PeopleMeshes.HiredHand, null));
            layout.Ambient.Add(("Pilgrim 1", Ground(-8f, 100f), 200f, PeopleMeshes.Pilgrim, "Pilgrim"));
            layout.Ambient.Add(("Pilgrim 2", Ground(-9.5f, 102f), 170f, PeopleMeshes.Pilgrim, "Pilgrim"));
            layout.Ambient.Add(("Pedlar", Ground(-6f, 292f), 100f, PeopleMeshes.Pedlar, null));
            layout.Ambient.Add(("Net Mender", OnDeck(14f, -1.05f), jettyYaw + 90f, PeopleMeshes.NetMender, null));
            layout.Ambient.Add(("Charcoal Burner", Ground(-70f, 290f), 230f, PeopleMeshes.CharcoalBurner, null));
        }

        // ---- Horse posts for stage B ------------------------------------------------------------------------------

        void BuildHorsePosts()
        {
            var parent = new GameObject("Horse Posts").transform;
            parent.SetParent(root, false);
            var posts = new (Vector2 post, Vector2 spot, bool rail)[]
            {
                (new Vector2(8f, 578f), new Vector2(10.6f, 577f), false),
                (new Vector2(-10f, 96f), new Vector2(-12.8f, 96.6f), false),
                (new Vector2(12f, 108f), new Vector2(12f, 110.4f), true),
                // In the hamlet's yard south-east of the well, clear of the houses, crates and lanterns: a player leaving any
                // of the three homes finds the horse within 20 m.
                (new Vector2(41.5f, -23f), new Vector2(41.5f, -20.6f), false),
            };
            for (int i = 0; i < posts.Length; i++)
            {
                var (at, spot, rail) = posts[i];
                Vector3 p = Settle($"Horse Post {i + 1}", at, 0f, rail ? new Vector2(3f, 0.3f) : new Vector2(0.3f, 0.3f), 1.3f, 0.05f);
                var b = DecorChunk(at.x, at.y);
                // The fourth post came after the scatter's random sequence was fixed: it shades from its own.
                System.Random rng = i < 3 ? this.rng : new System.Random(Seed + 4);
                b.Push(p, Quaternion.identity, Vector3.one);
                foreach (float x in rail ? new[] { -1.4f, 1.4f } : new[] { 0f })
                {
                    b.ShadedBox(new Vector3(x, 0.65f, 0f), new Vector3(0.18f, 1.4f, 0.18f), Pal.WoodDark, rng, 0.3f, 0.4f);
                    b.SetColor(Pal.SteelDark, 0.4f);
                    b.Prism(new Vector3(x, 1.05f, -0.12f), 0.08f, 0.08f, 0.03f, 8, false, false);
                }
                if (rail) b.ShadedBox(new Vector3(0f, 1.15f, 0f), new Vector3(3f, 0.14f, 0.14f), Pal.Wood, rng, 0.36f, 0.46f);
                b.Pop();
                var post = new GameObject($"Horse Post {i + 1}");
                post.transform.SetParent(parent, false);
                post.transform.position = p;
                var box = post.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.7f, 0f);
                box.size = rail ? new Vector3(3f, 1.4f, 0.2f) : new Vector3(0.2f, 1.4f, 0.2f);
                var standing = new GameObject("Standing Spot").transform;
                standing.SetParent(post.transform, false);
                // The horse stands with its head to the post.
                standing.SetPositionAndRotation(Ground(spot), Quaternion.Euler(0f, Mathf.Atan2(at.x - spot.x, at.y - spot.y) * Mathf.Rad2Deg, 0f));
            }
        }
    }
}
