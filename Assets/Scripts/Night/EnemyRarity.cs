namespace Undelivered.Night
{
    /// <summary>The same enemy can appear at different rarities, which scale its stats and ability.</summary>
    public enum EnemyRarity
    {
        Comun,
        Rara,
        Epica
    }

    public static class EnemyRarities
    {
        /// <summary>Multiplier applied to an enemy's health, shield and ability magnitude at this rarity.</summary>
        public static float Multiplier(EnemyRarity rarity)
        {
            switch (rarity)
            {
                case EnemyRarity.Rara: return 1.5f;
                case EnemyRarity.Epica: return 2f;
                default: return 1f;
            }
        }
    }
}
