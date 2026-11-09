namespace AshenRealm.Story
{
    /// <summary>Turns the bestiary's turn-based flavour into readable, different real-time fights.</summary>
    internal static class EnemyPatterns
    {
        static EnemyMove Strike(string name, float damage, int weight = 3, float windup = 0.55f) =>
            Technique(name, MoveKind.Melee, damage, weight, windup, 0.55f, 0f);
        static EnemyMove Shot(string name, float damage, BoltKind bolt, int weight = 3, float windup = 0.8f) =>
            Technique(name, MoveKind.Bolt, damage, weight, windup, 0.55f, 20f, 2.5f, bolt);
        static EnemyMove Technique(string name, MoveKind kind, float damage, int weight, float windup, float recovery,
            float range, float radius = 2.5f, BoltKind bolt = BoltKind.Arrow) => new EnemyMove
            { Name = name, Kind = kind, Damage = damage, Weight = weight, Windup = windup, Recovery = recovery, Range = range, Radius = radius, Bolt = bolt };
        static EnemyMove Mend(string name, float amount) => new EnemyMove
            { Name = name, Kind = MoveKind.Heal, Heal = amount, Windup = 1.7f, Recovery = 0.8f, Weight = 1, Cooldown = 11f };
        static EnemyMove Ward(string name) => new EnemyMove
            { Name = name, Kind = MoveKind.Ward, Windup = 0.9f, Recovery = 0.55f, Weight = 1, Cooldown = 12f };

        public static EnemyDefinition[] Configure(EnemyDefinition[] definitions)
        {
            foreach (EnemyDefinition enemy in definitions)
            {
                switch (enemy.Id)
                {
                    case "rat":
                        enemy.Moves = new[] { Strike("Mocskos Harapás", 6f).Poison(2f, 3f),
                            Technique("Patkányugrás", MoveKind.Pounce, 7f, 2, 0.7f, 0.8f, 5f).Reach(1.5f, 5f).Travel(3.4f, 0.38f).Rest(4f) };
                        break;
                    case "bat":
                        enemy.Strafe = -0.35f;
                        enemy.Moves = new[] { Technique("Szárnyas Zuhanás", MoveKind.Pounce, 7f, 3, 0.75f, 0.75f, 6f).Travel(4.2f, 0.45f),
                            Technique("Sírhang", MoveKind.Ring, 4f, 1, 1f, 0.7f, 4f, 3.5f).Weaken(2f).Rest(6f) };
                        break;
                    case "ghoul":
                        enemy.Moves = new[] { Technique("Karmok Íve", MoveKind.Sweep, 10f, 3, 0.8f, 0.7f, 2.9f, 2.6f),
                            Technique("Éhes Rohanás", MoveKind.Charge, 11f, 2, 1f, 0.9f, 7f).Reach(2f, 7f).Travel(5f, 0.6f).Rest(5f) };
                        break;
                    case "ooze":
                        enemy.Moves = new[] { Technique("Maró Tócsa", MoveKind.Ground, 7f, 3, 1.15f, 0.8f, 5f, 2.3f, BoltKind.Venom).Poison(3f, 3f),
                            Technique("Iszapmerülés", MoveKind.Burrow, 8f, 1, 1.1f, 0.9f, 8f, 2.5f, BoltKind.Venom).Travel(4f, 0.9f).Rest(7f) };
                        break;
                    case "wolf":
                        enemy.Strafe = -0.2f;
                        enemy.Moves = new[] { Strike("Tépő Agyar", 10f, 3, 0.5f),
                            Technique("Oldalazó Ugrás", MoveKind.Pounce, 10f, 2, 0.85f, 0.9f, 7f).Reach(1.4f, 7f).Travel(4.6f, 0.5f).Rest(4f) };
                        break;
                    case "skeleton":
                        enemy.Moves = new[] { Strike("Csontdöfés", 10f, 4, 0.65f),
                            Technique("Rozsdás Fedezék", MoveKind.Counter, 11f, 1, 0.5f, 0.95f, 2.5f).Rest(6f) };
                        break;
                    case "archer":
                        enemy.Strafe = 0.3f;
                        enemy.Moves = new[] { Shot("Leszegező Nyíl", 10f, BoltKind.Arrow).Stun(0.45f),
                            Technique("Három Vessző", MoveKind.Volley, 5f, 2, 1.1f, 0.8f, 20f).Volley(3).Rest(5f), Strike("Íjmarkolat", 7f, 2) };
                        break;
                    case "drownedthrall":
                        enemy.Moves = new[] { Strike("Vízzel Telt Ököl", 11f, 3, 0.9f),
                            Technique("Lehúzó Örvény", MoveKind.Ground, 9f, 2, 1.15f, 0.9f, 6f, 2.3f, BoltKind.Brine).Stun(0.6f).Rest(5f) };
                        break;
                    case "shade":
                        enemy.Strafe = -0.3f;
                        enemy.Moves = new[] { Shot("Lélekszívás", 9f, BoltKind.Shadow).Drain(0.6f),
                            Technique("Árnyékugrás", MoveKind.Teleport, 8f, 2, 1.05f, 0.8f, 15f, 2f, BoltKind.Shadow).Travel(3.4f, 0.3f).Rest(7f), Ward("Elhalványulás") };
                        break;
                    case "imp":
                        enemy.Strafe = 0.6f;
                        enemy.Moves = new[] { Technique("Parázseső", MoveKind.Volley, 5f, 3, 0.9f, 0.65f, 18f, 2f, BoltKind.Fire).Volley(3).Rest(4f),
                            Technique("Szikraugrás", MoveKind.Pounce, 8f, 2, 0.8f, 0.8f, 5f).Travel(3.5f, 0.4f) };
                        break;
                    case "cultist":
                        enemy.Moves = new[] { Shot("Sötét Nyíl", 11f, BoltKind.Shadow),
                            Technique("Vérpecsét", MoveKind.Ground, 8f, 2, 1.3f, 0.75f, 13f, 2.6f, BoltKind.Shadow).Weaken(4f).Rest(6f), Mend("Vérrítus", 16f) };
                        break;
                    case "rotpriest":
                        enemy.Moves = new[] { Shot("Rothadás Litániája", 8f, BoltKind.Venom).Poison(3f, 4f),
                            Technique("Rothadó Koszorú", MoveKind.Ring, 7f, 2, 1.4f, 0.9f, 6f, 5f, BoltKind.Venom).Poison(3f, 3f).Rest(6f), Mend("Ocsmány Áldás", 18f) };
                        break;
                    case "bonehound":
                        enemy.Moves = new[] { Strike("Roppantás", 13f),
                            Technique("Csontkopó Rohama", MoveKind.Charge, 12f, 2, 0.95f, 1f, 8f).Reach(2.2f, 8f).Travel(6.5f, 0.6f).Rest(4.5f) };
                        break;
                    case "gravemaw":
                        enemy.Moves = new[] { Technique("Pestisív", MoveKind.Sweep, 13f, 3, 0.85f, 0.8f, 3f, 2.8f).Poison(4f, 4f),
                            Technique("Síralagút", MoveKind.Burrow, 15f, 2, 1.25f, 1.05f, 11f, 3f).Travel(7f, 1f).Rest(7f),
                            Technique("Dögroham", MoveKind.Charge, 16f, 3, 0.95f, 1.15f, 11f).Reach(2.5f, 11f).Travel(8f, 0.7f).Phase(2).Rest(5f) };
                        break;
                    case "sporetyrant":
                        enemy.Moves = new[] { Technique("Zúzó Ág", MoveKind.Sweep, 15f, 3, 1f, 0.85f, 3.5f, 3.2f),
                            Technique("Gyökérfogás", MoveKind.Ground, 11f, 2, 1.4f, 0.9f, 12f, 3f, BoltKind.Venom).Stun(0.7f).Rest(5f),
                            Technique("Spórakorona", MoveKind.Ring, 10f, 3, 1.5f, 1.05f, 8f, 6f, BoltKind.Venom).Poison(4f, 4f).Phase(2).Rest(6f), Mend("Újranövés", 24f) };
                        break;
                    case "undertow":
                        enemy.Strafe = -0.45f;
                        enemy.Moves = new[] { Shot("Fekete Dagály", 13f, BoltKind.Brine),
                            Technique("Partravetett Árny", MoveKind.Teleport, 10f, 2, 1.1f, 0.85f, 16f, 2f, BoltKind.Brine).Travel(4f, 0.3f).Rest(6f),
                            Technique("Alsóáram Gyűrűje", MoveKind.Ring, 12f, 3, 1.5f, 1f, 9f, 7f, BoltKind.Brine).Drain(0.4f).Phase(2).Rest(6f) };
                        break;
                    case "cinderprophet":
                        enemy.Strafe = 0.25f;
                        enemy.Moves = new[] { Technique("Tűzprédikáció", MoveKind.Volley, 7f, 3, 1.2f, 0.85f, 20f, 2f, BoltKind.Fire).Volley(3),
                            Technique("Elhamvasztó Jel", MoveKind.Ground, 16f, 2, 1.45f, 1f, 15f, 3.4f, BoltKind.Fire).Phase(2).Rest(5f), Mend("Hamvas Rítus", 22f) };
                        break;
                    case "marrowcantor":
                        enemy.Moves = new[] { Strike("Velőtüske", 14f, 3, 0.9f),
                            Technique("Kántori Fedezék", MoveKind.Counter, 16f, 2, 0.65f, 1f, 3f).Rest(6f),
                            Technique("Gyászdal Koszorúja", MoveKind.Ring, 12f, 3, 1.4f, 1.1f, 9f, 7f, BoltKind.Shadow).Weaken(4f).Phase(2).Rest(6f), Ward("Csonthimnusz") };
                        break;
                    case "morr":
                        enemy.PhaseTwoAt = 0.65f;
                        enemy.Moves = new[] { Technique("Vámkasza", MoveKind.Sweep, 17f, 4, 1f, 1f, 4.7f, 4.4f),
                            Technique("Adós Láncainak Rohama", MoveKind.Charge, 16f, 3, 1.35f, 1.2f, 13f).Reach(3f, 13f).Travel(9f, 0.8f).Rest(6f),
                            Technique("Vámszedő Visszavágása", MoveKind.Counter, 19f, 2, 0.65f, 1.2f, 3.7f).Phase(2).Rest(6f),
                            Technique("A Holtak Három Érméje", MoveKind.Volley, 7f, 3, 1.25f, 0.85f, 18f, 2f, BoltKind.Shadow).Volley(3).Phase(2).Rest(5f) };
                        break;
                    case "mycel":
                        enemy.AttackInterval = 2.9f;
                        enemy.Moves = new[] { Technique("Gyökérzúzás", MoveKind.Ground, 16f, 4, 1.5f, 1.15f, 13f, 3.5f, BoltKind.Venom).Stun(0.65f),
                            Technique("A Kert Költözése", MoveKind.Burrow, 18f, 3, 1.3f, 1.15f, 16f, 3.8f, BoltKind.Venom).Travel(9f, 1.2f).Rest(8f),
                            Technique("Fojtó Virágzás", MoveKind.Ring, 12f, 4, 1.6f, 1.2f, 11f, 8.5f, BoltKind.Venom).Poison(4f, 4f).Phase(2).Rest(5f), Mend("A Kert Újranő", 34f).Phase(2) };
                        break;
                    case "brine":
                        enemy.PhaseTwoAt = 0.6f; enemy.Strafe = -0.3f;
                        enemy.Moves = new[] { Shot("Keresztelés", 15f, BoltKind.Brine, 3, 1.1f),
                            Technique("Vízbe Fúlt Kereszt", MoveKind.Ground, 15f, 3, 1.45f, 1.05f, 16f, 3.6f, BoltKind.Brine).Stun(0.6f).Rest(5f),
                            Technique("Az Apátnő Tükörlépése", MoveKind.Teleport, 12f, 2, 1.2f, 1f, 17f, 2f, BoltKind.Brine).Travel(5f, 0.3f).Rest(7f),
                            Technique("Dagály és Apály", MoveKind.Ring, 16f, 4, 1.7f, 1.2f, 12f, 9f, BoltKind.Brine).Phase(2).Rest(6f), Ward("Kútvédelem") };
                        break;
                    case "pyraxes":
                        enemy.PhaseTwoAt = 0.5f;
                        enemy.Moves = new[] { Technique("Kemencekalapács", MoveKind.Sweep, 18f, 4, 1.15f, 1.15f, 4.6f, 4.2f),
                            Technique("Kohóroham", MoveKind.Charge, 17f, 3, 1.3f, 1.25f, 14f).Reach(3f, 14f).Travel(10f, 0.85f).Rest(6f),
                            Technique("Salaktörés", MoveKind.Ground, 18f, 3, 1.6f, 1.2f, 15f, 4.2f, BoltKind.Fire).Rest(6f),
                            Technique("Háromszoros Kemencetűz", MoveKind.Volley, 8f, 3, 1.3f, 1f, 20f, 2f, BoltKind.Fire).Volley(3).Phase(2),
                            Technique("Perzselő Salakkoszorú", MoveKind.Ring, 16f, 2, 1.7f, 1.3f, 11f, 8f, BoltKind.Fire).Phase(2).Rest(7f) };
                        break;
                    case "gloamlord":
                        enemy.PhaseTwoAt = 0.67f; enemy.PhaseThreeAt = 0.3f;
                        enemy.Moves = new[] { Technique("Királyi Sírhasítás", MoveKind.Sweep, 20f, 4, 1.2f, 1f, 5.6f, 5.2f),
                            Shot("Lélektépés", 14f, BoltKind.Shadow, 2, 1.1f).Drain(0.4f).Rest(3f),
                            Technique("A Trón Fedezéke", MoveKind.Counter, 21f, 2, 0.75f, 1.2f, 4f).Rest(7f),
                            Technique("Árnytrón Lépése", MoveKind.Teleport, 14f, 3, 1.25f, 1.1f, 19f, 2f, BoltKind.Shadow).Travel(4.8f, 0.3f).Phase(2).Rest(6f),
                            Technique("Csontok Feltörése", MoveKind.Ground, 19f, 3, 1.6f, 1.15f, 17f, 3.8f).Phase(2).Rest(5f),
                            Technique("Nekrotikus Korona", MoveKind.Ring, 18f, 4, 1.85f, 1.2f, 13f, 10f, BoltKind.Shadow).Weaken(3f).Phase(3).Rest(6f),
                            Technique("Négyes Velővihar", MoveKind.Volley, 7f, 3, 1.4f, 1.05f, 20f).Volley(4).Phase(3).Rest(5f),
                            Mend("Lelkek Lakomája", 45f).Phase(3) };
                        break;
                }
            }
            return definitions;
        }
    }
}
