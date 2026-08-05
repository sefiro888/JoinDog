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

    public enum CampaignObjectiveKind
    {
        Score,
        Collect,
        LongMatch
    }

    public enum CampaignPieceKind
    {
        Dog,
        Bone,
        Ball,
        Food,
        Collar
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
        public CampaignObjectiveKind objectiveKind;
        public CampaignPieceKind targetPiece;
        [Range(1, 5)] public int difficulty = 1;
        public int targetScore = 3000;
        public int targetAmount = 3;
        public int rows = 8;
        public int columns = 8;
        public int durationSeconds = 75;
        public bool diamondBoard;
        public bool roundedBoard;
        public int pawBoosters = 1;
        public int boneBoosters = 1;
        public int foodBoosters = 1;
        public int rewardTreats = 25;
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
                CampaignLevelEntry entry = new CampaignLevelEntry
                {
                    id = $"park_{level:000}",
                    level = level,
                    nodeKind = kind,
                    mapX = xPattern[(level - 1) % xPattern.Length],
                    mapY = 260f + (level - 1) * 215f
                };
                ApplyLevelDesign(entry);
                catalog.levels.Add(entry);
            }
        }

        private static void ApplyLevelDesign(CampaignLevelEntry entry)
        {
            string[] titles =
            {
                "PRIMERAS HUELLAS", "HORA DE COMER", "PELOTAS AL AIRE", "COLLAR NUEVO", "RETO DEL SENDERO",
                "FIESTA DE HUESOS", "REGALO DE LA PRADERA", "CACHORROS INQUIETOS", "SPRINT VERDE", "GUARDIÁN DE LA PRADERA",
                "ENTRADA AL BOSQUE", "CAMINO DE HOJAS", "MERIENDA ESCONDIDA", "TESORO DEL ROBLE", "RETO ENTRE ÁRBOLES",
                "RASTRO DE COLLARES", "COFRE DEL BOSQUE", "NOCHE DE PELOTAS", "ÚLTIMA SENDA", "GUARDIÁN DEL BOSQUE",
                "LUCES DEL FESTIVAL", "DESFILE CANINO", "BANQUETE DE PREMIOS", "CARRERA DE COLORES", "RETO DE CAMPEONES",
                "LLUVIA DE HUESOS", "REGALO ESTRELLA", "GRAN COMBINACIÓN", "RECTA FINAL", "GRAN FINAL JOIN DOG"
            };

            int level = entry.level;
            entry.title = titles[Mathf.Clamp(level - 1, 0, titles.Length - 1)];
            entry.difficulty = Mathf.Clamp(1 + (level - 1) / 7, 1, 5);
            entry.rows = level <= 4 ? 8 : level <= 14 ? 9 : 10;
            entry.columns = level <= 9 ? 8 : 9;
            entry.durationSeconds = level <= 5 ? 85 : level <= 10 ? 90 : level <= 20 ? 95 : 100;
            entry.targetPiece = (CampaignPieceKind)((level + level / 3) % 5);
            entry.objectiveKind = level % 3 == 1 ? CampaignObjectiveKind.Collect :
                level % 3 == 2 ? CampaignObjectiveKind.LongMatch : CampaignObjectiveKind.Score;
            if (level <= 2) entry.objectiveKind = CampaignObjectiveKind.Score;
            // Each part of the campaign has its own board silhouette. Forest
            // levels use clipped corners while the Festival introduces the
            // narrower diamond layout on selected challenges and finales.
            entry.diamondBoard = level >= 21 && (level % 3 == 0 || entry.nodeKind == MapNodeKind.Finale);
            entry.roundedBoard = level >= 11 && level <= 20;
            entry.rewardTreats = 20 + entry.difficulty * 10 +
                (entry.nodeKind == MapNodeKind.Reward ? 60 : entry.nodeKind == MapNodeKind.Finale ? 100 : 0);
            entry.pawBoosters = entry.nodeKind == MapNodeKind.Reward ? 2 : 1;
            entry.boneBoosters = entry.nodeKind == MapNodeKind.Hard || entry.nodeKind == MapNodeKind.Finale ? 2 : 1;
            entry.foodBoosters = entry.nodeKind == MapNodeKind.Reward || entry.nodeKind == MapNodeKind.Finale ? 2 : 1;

            entry.targetScore = BalancedTargetScore(entry);
            entry.targetAmount = BalancedTargetAmount(entry);

            entry.objectivePreview = BuildObjectivePreview(entry);
        }

        public static string BuildObjectivePreview(CampaignLevelEntry entry)
        {
            if (entry == null) return string.Empty;
            int balancedAmount = BalancedTargetAmount(entry);
            int balancedScore = BalancedTargetScore(entry);
            switch (entry.objectiveKind)
            {
                case CampaignObjectiveKind.Collect:
                    return $"RECOGE {balancedAmount} {PieceLabel(entry.targetPiece)}";
                case CampaignObjectiveKind.LongMatch:
                    return $"CREA {balancedAmount} FICHAS ESPECIALES";
                default:
                    return $"CONSIGUE {balancedScore:N0} PUNTOS";
            }
        }

        public static string PieceLabel(CampaignPieceKind piece)
        {
            switch (piece)
            {
                case CampaignPieceKind.Dog: return "CACHORROS";
                case CampaignPieceKind.Bone: return "HUESOS";
                case CampaignPieceKind.Ball: return "PELOTAS";
                case CampaignPieceKind.Food: return "COMEDEROS";
                case CampaignPieceKind.Collar: return "COLLARES";
                default: return "FICHAS";
            }
        }

        public static int BalancedTargetScore(CampaignLevelEntry entry)
        {
            if (entry == null) return 10000;
            int challengeBonus = entry.nodeKind == MapNodeKind.Hard ? 5500 :
                entry.nodeKind == MapNodeKind.Finale ? 9000 : 0;
            int worldBonus = Mathf.Max(0, (entry.level - 1) / 10) * 4000;
            return 8000 + entry.level * 1800 + worldBonus + challengeBonus;
        }

        public static int BalancedTargetAmount(CampaignLevelEntry entry)
        {
            if (entry == null) return 12;
            int challengeBonus = entry.nodeKind == MapNodeKind.Hard ? 3 :
                entry.nodeKind == MapNodeKind.Finale ? 5 : 0;
            if (entry.objectiveKind == CampaignObjectiveKind.LongMatch)
                return Mathf.Clamp(2 + entry.difficulty + challengeBonus / 2, 3, 8);
            return 12 + Mathf.CeilToInt(entry.level * 0.75f) + challengeBonus;
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
