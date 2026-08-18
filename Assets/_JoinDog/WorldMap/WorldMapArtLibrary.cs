using System.Collections.Generic;
using UnityEngine;

namespace JoinDog.App
{
    /// <summary>
    /// Resolves optional world artwork without coupling campaign data to a
    /// particular scene. The map remains fully functional when an art layer is
    /// missing, which lets worlds be upgraded independently and safely.
    /// </summary>
    internal static class WorldMapArtLibrary
    {
        private static readonly Dictionary<string, string> BackgroundPaths =
            new Dictionary<string, string>
            {
                { "bosque_aventura", "Worlds/Forest/forest_world_background_v1" },
                { "festival_canino", "Worlds/Festival/festival_world_background_v1" },
                { "costa_dorada", "Worlds/Coast/coast_world_background_v1" },
                { "cumbres_nevadas", "Worlds/Mountain/mountain_world_background_v1" },
                { "valle_aurora", "Worlds/Aurora/aurora_world_background_v2" },
                { "cumbre_luminosa", "Worlds/Summit/summit_world_background_v2" }
            };

        private static readonly Dictionary<string, string> EntrancePaths =
            new Dictionary<string, string>
            {
                { "bosque_aventura", "Worlds/Forest/forest_entrance_arch_v1" },
                { "festival_canino", "Worlds/Festival/festival_entrance_arch_v1" },
                { "costa_dorada", "Worlds/Coast/coast_entrance_arch_v1" },
                { "cumbres_nevadas", "Worlds/Mountain/mountain_entrance_arch_v1" },
                { "valle_aurora", "Worlds/Aurora/aurora_entrance_arch_v2" },
                { "cumbre_luminosa", "Worlds/Summit/summit_entrance_arch_v2" }
            };

        private static readonly Dictionary<string, Sprite> BackgroundCache =
            new Dictionary<string, Sprite>();

        private static readonly Dictionary<string, Sprite> EntranceCache =
            new Dictionary<string, Sprite>();

        public static Sprite LoadBackground(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId) || !BackgroundPaths.TryGetValue(zoneId, out string path))
                return null;

            if (BackgroundCache.TryGetValue(zoneId, out Sprite cached))
                return cached;

            Sprite sprite = Resources.Load<Sprite>(path);
            BackgroundCache[zoneId] = sprite;
            return sprite;
        }

        public static Sprite LoadEntrance(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId) || !EntrancePaths.TryGetValue(zoneId, out string path))
                return null;

            if (EntranceCache.TryGetValue(zoneId, out Sprite cached))
                return cached;

            Sprite sprite = Resources.Load<Sprite>(path);
            EntranceCache[zoneId] = sprite;
            return sprite;
        }
    }
}
