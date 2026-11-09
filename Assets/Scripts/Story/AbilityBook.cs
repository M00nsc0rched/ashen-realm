namespace AshenRealm.Story
{
    /// <summary>Stable indices keep learned tiers and the two equipped slots compatible with older souls.</summary>
    public static class AbilityBook
    {
        public const int KnownCount = 4;
        public static readonly AbilityDefinition[][] Additional =
        {
            new[] {
                Skill(AbilityEffect.Whirlwind, "Hamukör", "Körbecsapsz: minden közeli ellenfél sebződik.", 4, 11f),
                Skill(AbilityEffect.IronSkin, "Vasfogadalom", "Felezett sebzés, visszatérő kitartás és rövid harci düh.", 3, 17f) },
            new[] {
                Skill(AbilityEffect.FanOfKnives, "Tőrlegyező", "Három tőrt hajítasz szétterülő ívben. Fejlesztve több pengét vetsz.", 3, 7f),
                Skill(AbilityEffect.Caltrops, "Szöges csapda", "Szögeket szórsz a lábadhoz: mérgezik és megtorpasztják az üldözőket.", 3, 13f) },
            new[] {
                Skill(AbilityEffect.FrostLance, "Dérlándzsa", "Messzire ható jégdöfés, amely kábít és gyengít.", 4, 8f),
                Skill(AbilityEffect.ChainLightning, "Láncvillám", "Villám ugrik egymás közelében álló ellenfelek között.", 6, 12f) },
            new[] {
                Skill(AbilityEffect.Sanctuary, "Virrasztók köre", "A megidézett körben állva gyógyulsz és védelmet kapsz.", 4, 19f),
                Skill(AbilityEffect.Consecration, "Felszentelt föld", "Szent kör perzseli a benne álló ellenfeleket, az élőholtakat erősebben.", 5, 17f) },
            new[] {
                Skill(AbilityEffect.BoneNova, "Csontrobbanás", "Csontszilánkok törnek ki körülötted, megszakítva a közeli támadásokat.", 5, 10f),
                Skill(AbilityEffect.SoulHarvest, "Lélekaratás", "A közeli ellenfelek erejéből életerőt és fókuszt nyersz vissza.", 5, 14f) },
            new[] {
                Skill(AbilityEffect.FireBomb, "Tűzüveg", "Robbanó elegy, amely rövid ideig égő foltot hagy a földön.", 4, 11f),
                Skill(AbilityEffect.FlashPowder, "Vakító por", "Előtted fény lobban: a közeli támadók elkábulnak és gyengülnek.", 3, 14f) },
        };

        static AbilityDefinition Skill(AbilityEffect effect, string name, string text, int cost, float cooldown) =>
            new AbilityDefinition { Effect = effect, Name = name, Description = text, Cost = cost, Cooldown = cooldown };

        public static AbilityDefinition At(ClassDefinition definition, int index)
        {
            if (definition == null || index < 0 || index >= KnownCount) return null;
            if (index == 0) return definition.Ability1;
            if (index == 1) return definition.Ability2;
            return Additional[(int)definition.Id][index - 2];
        }

        public static AbilityDefinition[] All(ClassDefinition definition) =>
            new[] { At(definition, 0), At(definition, 1), At(definition, 2), At(definition, 3) };
    }
}
