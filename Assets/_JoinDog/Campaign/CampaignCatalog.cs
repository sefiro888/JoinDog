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
        public List<CampaignLevelEntry> levels = new List<CampaignLevelEntry>();

        public CampaignLevelEntry GetLevel(int number)
        {
            return levels.Find(entry => entry != null && entry.level == number);
        }

        public static CampaignCatalog LoadOrCreateRuntime()
        {
            CampaignCatalog asset = Resources.Load<CampaignCatalog>("Campaign/ParqueCentral");
            if (asset != null && asset.levels != null && asset.levels.Count >= MaxLevel)
                return asset;

            CampaignCatalog fallback = CreateInstance<CampaignCatalog>();
            PopulateDefaults(fallback);
            return fallback;
        }

        public static void PopulateDefaults(CampaignCatalog catalog)
        {
            catalog.campaignId = "parque_central";
            catalog.displayName = "PARQUE CENTRAL";
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
    }
}
