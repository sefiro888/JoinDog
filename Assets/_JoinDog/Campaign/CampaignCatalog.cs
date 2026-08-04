using System;
using System.Collections.Generic;
using UnityEngine;

namespace JoinDog.App
{
    public enum MapNodeKind
    {
        Normal,
        Hard,
        Reward,
        Finale
    }

    [Serializable]
    public sealed class CampaignZoneEntry
    {
        public string id;
        public string displayName;
        public string subtitle;
        public int firstLevel;
        public int lastLevel;
        public Color skyColor;
        public Color groundColor;
        public Color accentColor;
    }

    [Serializable]
    public sealed class CampaignLevelEntry
    {
        public string id;
        public int level;
        public string title;
        public string objectivePreview;
        public MapNodeKind nodeKind;
        [Range(0f, 1f)] public float mapX = 0.5f;
        public float mapY;
    }

    [CreateAssetMenu(menuName = "JoinDog/Campaign Catalog", fileName = "ParqueCentral")]
    public sealed class CampaignCatalog : ScriptableObject
    {
        public const int MaxLevel = 30;
        public string campaignId = "parque_central";
        public string displayName = "PARQUE CENTRAL";
        public List<CampaignZoneEntry> zones = new List<CampaignZoneEntry>();
        public List<CampaignLevelEntry> levels = new List<CampaignLevelEntry>();

        public CampaignLevelEntry GetLevel(int number)
        {
            return levels.Find(entry => entry != null && entry.level == number);
        }

        public CampaignZoneEntry GetZoneForLevel(int number)
        {
            EnsureZones(this);
            return zones.Find(zone => zone != null && number >= zone.firstLevel && number <= zone.lastLevel);
        }

        public static CampaignCatalog LoadOrCreateRuntime()
        {
            CampaignCatalog asset = Resources.Load<CampaignCatalog>("Campaign/ParqueCentral");
            if (asset != null && asset.levels != null && asset.levels.Count >= MaxLevel)
            {
                EnsureZones(asset);
                return asset;
            }

            CampaignCatalog fallback = CreateInstance<CampaignCatalog>();
            PopulateDefaults(fallback);
            return fallback;
        }

        public static void PopulateDefaults(CampaignCatalog catalog)
        {
            catalog.campaignId = "parque_central";
            catalog.displayName = "PARQUE CENTRAL";
            PopulateZones(catalog);
            catalog.levels = new List<CampaignLevelEntry>();
            float[] xPattern = { 0.24f, 0.46f, 0.73f, 0.66f, 0.38f, 0.22f, 0.48f, 0.76f };
            for (int level = 1; level <= MaxLevel; level++)
            {
                MapNodeKind kind = level % 10 == 0 ? MapNodeKind.Finale :
                    level % 7 == 0 ? MapNodeKind.Reward :
                    level % 5 == 0 ? MapNodeKind.Hard : MapNodeKind.Normal;
                catalog.levels.Add(new CampaignLevelEntry
                {
                    id = $"park_{level:000}",
                    level = level,
                    title = kind == MapNodeKind.Finale ? $"GRAN RETO {level}" : $"NIVEL {level}",
                    objectivePreview = level <= 3 ? "Consigue la puntuación objetivo" :
                        level % 3 == 1 ? "Recoge las fichas indicadas" :
                        level % 3 == 2 ? "Completa una cadena larga" : "Supera la puntuación",
                    nodeKind = kind,
                    mapX = xPattern[(level - 1) % xPattern.Length],
                    mapY = 260f + (level - 1) * 215f
                });
            }
        }

        private static void EnsureZones(CampaignCatalog catalog)
        {
            if (catalog.zones == null || catalog.zones.Count < 3)
                PopulateZones(catalog);
        }

        private static void PopulateZones(CampaignCatalog catalog)
        {
            catalog.zones = new List<CampaignZoneEntry>
            {
                new CampaignZoneEntry
                {
                    id = "pradera_feliz",
                    displayName = "PRADERA FELIZ",
                    subtitle = "Primeros pasos",
                    firstLevel = 1,
                    lastLevel = 10,
                    skyColor = new Color(0.40f, 0.82f, 0.96f, 1f),
                    groundColor = new Color(0.34f, 0.72f, 0.20f, 1f),
                    accentColor = new Color(1f, 0.72f, 0.15f, 1f)
                },
                new CampaignZoneEntry
                {
                    id = "bosque_aventura",
                    displayName = "BOSQUE AVENTURA",
                    subtitle = "Nuevos retos",
                    firstLevel = 11,
                    lastLevel = 20,
                    skyColor = new Color(0.24f, 0.64f, 0.62f, 1f),
                    groundColor = new Color(0.12f, 0.46f, 0.22f, 1f),
                    accentColor = new Color(0.98f, 0.50f, 0.16f, 1f)
                },
                new CampaignZoneEntry
                {
                    id = "festival_canino",
                    displayName = "FESTIVAL CANINO",
                    subtitle = "Camino a la gran final",
                    firstLevel = 21,
                    lastLevel = 30,
                    skyColor = new Color(0.48f, 0.34f, 0.72f, 1f),
                    groundColor = new Color(0.18f, 0.28f, 0.52f, 1f),
                    accentColor = new Color(1f, 0.78f, 0.18f, 1f)
                }
            };
        }
    }
}
