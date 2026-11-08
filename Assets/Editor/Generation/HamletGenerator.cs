using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Three lived-in valley cottages: the weaver, the provisioner and the letter-writer.</summary>
    internal sealed class HamletGenerator : RoomGenerator
    {
        readonly int household;
        readonly float halfWidth, halfDepth;
        const float Ceiling = 3.7f;

        public HamletGenerator(Material material, Vector3 origin, int seed, int household) : base(material, origin, seed)
        {
            this.household = household;
            halfWidth = household == 2 ? 4f : household == 1 ? 3.1f : 3.5f;
            halfDepth = household == 2 ? 3.4f : 3f;
        }

        protected override Color Dark => new Color(0.26f, 0.2f, 0.16f);

        public RoomLayout Generate(Transform parent)
        {
            Begin(parent);
            Shell();
            StreetDoor(-halfDepth + 0.06f);
            Hearth();
            Bed();
            Table();
            Shelves();
            Window();
            if (household == 0) Loom();
            else if (household == 1) Provisions();
            else WritingDesk();
            Finish("Hamlet_Home_" + household);
            Light("Cottage Fire", new Vector3(-halfWidth + 0.9f, 1.2f, halfDepth - 0.8f), new Color(1f, 0.51f, 0.24f), 7f, 1.05f, 0.22f);
            return new RoomLayout
            {
                Entry = origin + new Vector3(0f, 0.05f, -halfDepth + 1.45f),
                DoorOut = origin + new Vector3(0f, 0f, -halfDepth + 0.25f),
            };
        }

        void Shell()
        {
            Solid("Floor", new Vector3(0f, -0.3f, 0f), new Vector3(halfWidth * 2f + 1f, 0.6f, halfDepth * 2f + 1f));
            for (float x = -halfWidth; x < halfWidth; x += 0.48f)
                Timber(new Vector3(Mathf.Min(x + 0.24f, halfWidth), -0.065f, 0f), new Vector3(Mathf.Min(0.46f, halfWidth - x), 0.12f, halfDepth * 2f), 0.39f);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (halfWidth + 0.15f), z = side * (halfDepth + 0.15f);
                StoneWall(new Vector3(x, 0f, -halfDepth), new Vector3(x, 0f, halfDepth), 0.8f, 0.3f, 0.25f, 0.4f);
                room.ShadedBox(new Vector3(x, 2.25f, 0f), new Vector3(0.3f, 2.9f, halfDepth * 2f), Pal.Plaster, rng, 0.42f, 0.55f);
                room.ShadedBox(new Vector3(0f, Ceiling * 0.5f, z), new Vector3(halfWidth * 2f, Ceiling, 0.3f), Pal.Plaster, rng, 0.4f, 0.54f);
                Solid(side < 0f ? "West Wall" : "East Wall", new Vector3(x, Ceiling * 0.5f, 0f), new Vector3(0.35f, Ceiling, halfDepth * 2f + 0.6f));
                Solid(side < 0f ? "South Wall" : "North Wall", new Vector3(0f, Ceiling * 0.5f, z), new Vector3(halfWidth * 2f + 0.6f, Ceiling, 0.35f));
                foreach (float end in new[] { -1f, 1f })
                    Timber(new Vector3(side * (halfWidth - 0.05f), Ceiling * 0.5f, end * (halfDepth - 0.05f)), new Vector3(0.2f, Ceiling, 0.2f), 0.32f);
            }
            for (float z = -halfDepth; z <= halfDepth; z += 1.4f)
                Timber(new Vector3(0f, Ceiling - 0.18f, z), new Vector3(halfWidth * 2f, 0.24f, 0.22f), 0.34f);
            room.SetColor(Pal.WoodDark, 0.28f);
            room.Box(new Vector3(0f, Ceiling + 0.08f, 0f), new Vector3(halfWidth * 2f + 0.4f, 0.16f, halfDepth * 2f + 0.4f));
            Solid("Ceiling", new Vector3(0f, Ceiling + 0.2f, 0f), new Vector3(halfWidth * 2f + 0.6f, 0.4f, halfDepth * 2f + 0.6f));
            room.SetColor(household == 1 ? Pal.ClothDark : Pal.ClothRed, 0.37f);
            room.Box(new Vector3(0f, 0.012f, 0.2f), new Vector3(1.5f, 0.025f, 2.4f));
        }

        void Hearth()
        {
            Vector3 at = new Vector3(-halfWidth + 0.85f, 0f, halfDepth - 0.7f);
            foreach (float side in new[] { -1f, 1f })
                room.ShadedBox(at + new Vector3(side * 0.63f, 0.7f, 0f), new Vector3(0.3f, 1.4f, 1f), Pal.Stone, rng, 0.26f, 0.42f);
            room.ShadedBox(at + new Vector3(0f, 1.5f, 0f), new Vector3(1.65f, 0.3f, 1.2f), Pal.Stone, rng, 0.3f, 0.45f);
            room.ShadedBox(at + new Vector3(0f, 2.6f, 0.25f), new Vector3(1.2f, 2f, 0.65f), Pal.Stone, rng, 0.25f, 0.38f);
            room.SetColor(Pal.Black, 0.12f);
            room.Box(at + new Vector3(0f, 0.6f, 0.4f), new Vector3(1f, 1.2f, 0.08f));
            room.SetColor(Pal.WoodDark, 0.3f);
            room.Box(at + new Vector3(0f, 0.2f, -0.07f), new Vector3(0.9f, 0.14f, 0.25f));
            for (int i = 0; i < 7; i++)
            {
                room.SetColor(i % 2 == 0 ? Pal.FireYellow : Pal.FireOrange, 0.75f);
                room.Prism(at + new Vector3(-0.35f + i * 0.12f, 0.22f, -0.08f), 0.1f, 0f, R(0.28f, 0.6f), 5);
            }
            Solid("Hearth", at + Vector3.up * 0.7f, new Vector3(1.65f, 1.4f, 1.2f));
            AddGlow(at + new Vector3(0f, 1.05f, -0.75f), FireLight, 8f);
            Iron(at + new Vector3(0f, 1.05f, -0.15f), new Vector3(0.7f, 0.05f, 0.05f));
            room.SetColor(Pal.SteelDark, 0.3f);
            room.Prism(at + new Vector3(0f, 0.54f, -0.12f), 0.24f, 0.3f, 0.3f, 9);
        }

        void Bed()
        {
            Vector3 at = new Vector3(halfWidth - 0.9f, 0f, halfDepth - 1.55f);
            Timber(at + Vector3.up * 0.37f, new Vector3(1.45f, 0.24f, 2.45f), 0.35f);
            foreach (float x in new[] { -0.63f, 0.63f })
            foreach (float z in new[] { -1.08f, 1.08f })
                Timber(at + new Vector3(x, 0.36f, z), new Vector3(0.12f, 0.72f, 0.12f), 0.34f);
            Timber(at + new Vector3(0f, 0.8f, 1.1f), new Vector3(1.45f, 0.55f, 0.12f), 0.39f);
            room.SetColor(Pal.Thatch, 0.55f);
            room.Box(at + Vector3.up * 0.59f, new Vector3(1.32f, 0.2f, 2.25f));
            room.SetColor(household == 0 ? Pal.ClothRed : Pal.ClothDark, 0.47f);
            room.Box(at + new Vector3(0f, 0.72f, -0.25f), new Vector3(1.35f, 0.07f, 1.65f));
            room.SetColor(Pal.Plaster, 0.65f);
            room.Box(at + new Vector3(0f, 0.77f, 0.78f), new Vector3(0.95f, 0.2f, 0.4f));
            Solid("Bed", at + Vector3.up * 0.45f, new Vector3(1.5f, 0.9f, 2.5f));
        }

        void Table()
        {
            Vector3 at = new Vector3(-halfWidth + 1.15f, 0f, -0.6f);
            Timber(at + Vector3.up * 0.95f, new Vector3(1.45f, 0.12f, 1.2f), 0.43f);
            foreach (float x in new[] { -0.55f, 0.55f })
            foreach (float z in new[] { -0.45f, 0.45f })
                Timber(at + new Vector3(x, 0.45f, z), new Vector3(0.12f, 0.9f, 0.12f), 0.36f);
            Solid("Table", at + Vector3.up * 0.5f, new Vector3(1.45f, 1f, 1.2f));
            for (int i = 0; i < 2; i++)
            {
                Vector3 stool = at + new Vector3(i == 0 ? 0.85f : -0.65f, 0f, i == 0 ? -0.15f : -0.95f);
                Timber(stool + Vector3.up * 0.5f, new Vector3(0.45f, 0.1f, 0.45f), 0.42f);
                foreach (float side in new[] { -1f, 1f })
                    Timber(stool + new Vector3(side * 0.14f, 0.25f, 0f), new Vector3(0.08f, 0.5f, 0.3f), 0.34f);
                Solid("Stool", stool + Vector3.up * 0.27f, new Vector3(0.45f, 0.54f, 0.45f));
            }
            room.SetColor(Pal.Wood, 0.52f);
            room.Prism(at + new Vector3(0.25f, 1.02f, 0.05f), 0.08f, 0.19f, 0.09f, 9);
            room.SetColor(Pal.Brass, 0.5f);
            room.Prism(at + new Vector3(-0.35f, 1.02f, -0.25f), 0.07f, 0.08f, 0.17f, 8);
            Candle(at + new Vector3(-0.4f, 1.02f, 0.35f), 0.2f);
        }

        void Shelves()
        {
            Vector3 at = new Vector3(-halfWidth + 0.24f, 0f, 0.8f);
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = 0.75f + shelf * 0.55f;
                Timber(at + new Vector3(0f, y, 0f), new Vector3(0.45f, 0.08f, 1.25f), 0.4f);
                for (int i = 0; i < 4; i++)
                {
                    room.SetColor(i % 2 == 0 ? Pal.Plaster : Pal.Leather, R(0.4f, 0.6f));
                    room.Prism(at + new Vector3(0.02f, y + 0.05f, -0.44f + i * 0.28f), 0.09f, 0.055f, R(0.2f, 0.32f), 7);
                }
            }
            Solid("Shelves", at + Vector3.up * 1.1f, new Vector3(0.5f, 2.2f, 1.3f));
            Prop(new Vector3(halfWidth - 0.6f, 0f, -halfDepth + 0.7f), 0f, b => PropMeshes.Crate(b, rng, 0.85f));
            Solid("Storage Chest", new Vector3(halfWidth - 0.6f, 0.43f, -halfDepth + 0.7f), Vector3.one * 0.88f);
        }

        void Window()
        {
            float z = halfDepth - 0.025f;
            room.SetColor(Pal.PaintBlue, 0.52f);
            room.Current.Emission = 0.28f;
            room.Box(new Vector3(0f, 2.25f, z), new Vector3(1.25f, 1.15f, 0.06f));
            foreach (float x in new[] { -0.65f, 0f, 0.65f })
                Timber(new Vector3(x, 2.25f, z - 0.07f), new Vector3(0.08f, 1.3f, 0.12f), 0.34f);
            foreach (float y in new[] { 1.65f, 2.25f, 2.85f })
                Timber(new Vector3(0f, y, z - 0.07f), new Vector3(1.4f, 0.09f, 0.12f), 0.34f);
            AddGlow(new Vector3(0f, 2.3f, halfDepth - 0.7f), StreetLight, 6f);
        }

        void Loom()
        {
            Vector3 at = new Vector3(halfWidth - 0.6f, 0f, -0.9f);
            foreach (float z in new[] { -0.5f, 0.5f })
                Timber(at + new Vector3(0f, 0.9f, z), new Vector3(0.12f, 1.8f, 0.12f), 0.42f);
            foreach (float y in new[] { 0.65f, 1.7f })
                Timber(at + new Vector3(0f, y, 0f), new Vector3(0.16f, 0.14f, 1.15f), 0.38f);
            for (int i = 0; i < 12; i++)
            {
                room.SetColor(i % 3 == 0 ? Pal.ClothRed : Pal.Thatch, 0.55f);
                room.Box(at + new Vector3(-0.05f, 1.13f, -0.46f + i * 0.085f), new Vector3(0.025f, 1.05f, 0.018f));
            }
            Solid("Weaving Loom", at + Vector3.up * 0.9f, new Vector3(0.65f, 1.8f, 1.2f));
        }

        void Provisions()
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = new Vector3(halfWidth - 0.45f, 0f, -0.6f - i * 0.7f);
                room.SetColor(Pal.Thatch, 0.42f + i * 0.04f);
                room.Prism(at, 0.32f, 0.2f, 0.7f, 8);
                room.Prism(at + Vector3.up * 0.7f, 0.2f, 0.07f, 0.15f, 8);
                Solid("Grain Sack", at + Vector3.up * 0.42f, new Vector3(0.66f, 0.84f, 0.66f));
            }
        }

        void WritingDesk()
        {
            Vector3 at = new Vector3(halfWidth - 0.5f, 0f, -0.7f);
            Timber(at + Vector3.up * 0.98f, new Vector3(0.8f, 0.1f, 1.6f), 0.4f);
            foreach (float z in new[] { -0.65f, 0.65f })
                Timber(at + new Vector3(0f, 0.45f, z), new Vector3(0.65f, 0.9f, 0.1f), 0.33f);
            room.SetColor(Pal.Plaster, 0.68f);
            room.Box(at + new Vector3(-0.1f, 1.05f, -0.2f), new Vector3(0.45f, 0.015f, 0.55f));
            for (int i = 0; i < 5; i++)
            {
                room.SetColor(i % 2 == 0 ? Pal.Leather : Pal.ClothRed, 0.36f + i * 0.04f);
                room.Box(at + new Vector3(0f, 1.08f + i * 0.065f, 0.45f), new Vector3(0.46f, 0.06f, 0.32f));
            }
            Candle(at + new Vector3(0f, 1.04f, -0.6f), 0.25f);
            Solid("Writing Desk", at + Vector3.up * 0.53f, new Vector3(0.82f, 1.06f, 1.65f));
        }
    }
}
