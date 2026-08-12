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
        LongMatch,
        ClearObstacles,
        Cascades
    }

    public enum CampaignObstacleKind
    {
        None,
        Vine,
        Lantern,
        Sand,
        Ice
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
        public CampaignObstacleKind obstacleType;
        public int obstacleCount;
        public int obstacleDurability = 1;
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
        public const int MaxLevel = 50;
        public const float ThinkingTimeScale = 0.68f;
        // Temporary QA switch. It exposes every campaign node without changing
        // the player's saved completion, stars, rewards or natural unlock point.
        public const bool UnlockAllLevelsForTesting = true;
        public string campaignId = "parque_central";
        public string displayName = "AVENTURA JOIN DOG";
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
                foreach (CampaignLevelEntry entry in asset.levels)
                    if (entry != null) ApplyLevelDesign(entry);
                return asset;
            }

            CampaignCatalog fallback = CreateInstance<CampaignCatalog>();
            PopulateDefaults(fallback);
            return fallback;
        }

        public static void PopulateDefaults(CampaignCatalog catalog)
        {
            catalog.campaignId = "parque_central";
            catalog.displayName = "AVENTURA JOIN DOG";
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
                "FIESTA DE HUESOS", "REGALO DE LA PRADERA", "CACHORROS INQUIETOS", "SPRINT VERDE", "GUARDIAN DE LA PRADERA",
                "ENTRADA AL BOSQUE", "CAMINO DE HOJAS", "MERIENDA ESCONDIDA", "TESORO DEL ROBLE", "RETO ENTRE ARBOLES",
                "RASTRO DE COLLARES", "COFRE DEL BOSQUE", "NOCHE DE PELOTAS", "ULTIMA SENDA", "GUARDIAN DEL BOSQUE",
                "LUCES DEL FESTIVAL", "DESFILE CANINO", "BANQUETE DE PREMIOS", "CARRERA DE COLORES", "RETO DE CAMPEONES",
                "LLUVIA DE HUESOS", "REGALO ESTRELLA", "GRAN COMBINACION", "RECTA FINAL", "GRAN FINAL DEL FESTIVAL",
                "BRISA MARINA", "HUELLAS EN LA ARENA", "TESORO DE CONCHAS", "OLAS DE COLORES", "RETO DEL EMBARCADERO",
                "CASCADA TROPICAL", "COFRE DE LA COSTA", "MAREA DE PELOTAS", "ULTIMA OLA", "GUARDIAN DE LA COSTA",
                "PRIMERA NEVADA", "SENDERO HELADO", "REFUGIO DE CACHORROS", "CUMBRE DE CRISTAL", "RETO DE LA VENTISCA",
                "AVALANCHA DE COMBOS", "TESORO DE LA CUMBRE", "LUCES POLARES", "ASCENSO FINAL", "GRAN CUMBRE JOIN DOG"
            };

            int level = entry.level;
            entry.title = titles[Mathf.Clamp(level - 1, 0, titles.Length - 1)];
            // Ten-level chapters now form clean difficulty bands.  The old
            // eight-level step made a few ordinary stages harder than the
            // chapter finale that followed them.
            entry.difficulty = Mathf.Clamp(1 + (level - 1) / 10, 1, 5);
            entry.rows = level <= 4 ? 8 : level <= 14 ? 9 : 10;
            entry.columns = level <= 9 ? 8 : 9;
            int rawSeconds = level <= 5 ? 90 : level <= 10 ? 95 : level <= 20 ? 100 :
                level <= 30 ? 105 : level <= 40 ? 110 : 115;
            entry.durationSeconds = Mathf.RoundToInt(rawSeconds * ThinkingTimeScale);
            entry.targetPiece = (CampaignPieceKind)((level + level / 3) % 5);
            entry.objectiveKind = level >= 21 && level % 6 == 0 ? CampaignObjectiveKind.Cascades :
                level % 4 == 1 ? CampaignObjectiveKind.Collect :
                level % 4 == 2 ? CampaignObjectiveKind.LongMatch : CampaignObjectiveKind.Score;
            if (level <= 2) entry.objectiveKind = CampaignObjectiveKind.Score;

            entry.diamondBoard = (level >= 21 && level <= 30 &&
                (level % 3 == 0 || entry.nodeKind == MapNodeKind.Finale)) ||
                (level >= 41 && (level % 4 == 1 || entry.nodeKind == MapNodeKind.Finale));
            entry.roundedBoard = (level >= 11 && level <= 20) ||
                (level >= 31 && level <= 40);
            entry.obstacleType = level >= 41 ? CampaignObstacleKind.Ice :
                level >= 31 ? CampaignObstacleKind.Sand :
                level >= 21 ? CampaignObstacleKind.Lantern :
                level >= 11 ? CampaignObstacleKind.Vine : CampaignObstacleKind.None;
            entry.obstacleCount = level >= 41 ? 14 + entry.difficulty * 2 :
                level >= 31 ? 12 + entry.difficulty * 2 :
                level >= 21 ? 10 + entry.difficulty * 2 :
                level >= 11 ? 7 + entry.difficulty * 2 : 0;
            entry.obstacleDurability = level >= 41 ? 3 : level >= 21 ? 2 : 1;

            if (level == 10)
            {
                entry.objectiveKind = CampaignObjectiveKind.LongMatch;
                entry.targetAmount = 4;
            }
            else if (level == 20)
            {
                ConfigureFinale(entry, CampaignObstacleKind.Vine, 18, 2);
            }
            else if (level == 30)
            {
                ConfigureFinale(entry, CampaignObstacleKind.Lantern, 22, 2);
            }
            else if (level == 40)
            {
                ConfigureFinale(entry, CampaignObstacleKind.Sand, 24, 2);
            }
            else if (level == 50)
            {
                ConfigureFinale(entry, CampaignObstacleKind.Ice, 26, 3);
            }

            entry.rewardTreats = 20 + entry.difficulty * 10 +
                (entry.nodeKind == MapNodeKind.Reward ? 60 : entry.nodeKind == MapNodeKind.Finale ? 100 : 0);
            entry.pawBoosters = entry.nodeKind == MapNodeKind.Reward ? 2 : 1;
            entry.boneBoosters = entry.nodeKind == MapNodeKind.Hard || entry.nodeKind == MapNodeKind.Finale ? 2 : 1;
            entry.foodBoosters = entry.nodeKind == MapNodeKind.Reward || entry.nodeKind == MapNodeKind.Finale ? 2 : 1;

            entry.targetScore = BalancedTargetScore(entry);
            entry.targetAmount = BalancedTargetAmount(entry);
            entry.objectivePreview = BuildObjectivePreview(entry);
        }

        private static void ConfigureFinale(CampaignLevelEntry entry, CampaignObstacleKind obstacle,
            int count, int durability)
        {
            entry.objectiveKind = CampaignObjectiveKind.ClearObstacles;
            entry.obstacleType = obstacle;
            entry.obstacleCount = count;
            entry.obstacleDurability = durability;
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
                case CampaignObjectiveKind.ClearObstacles:
                    return $"LIMPIA {Mathf.Max(1, entry.obstacleCount)} OBSTACULOS";
                case CampaignObjectiveKind.Cascades:
                    return $"PROVOCA {balancedAmount} CASCADAS";
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
            int challengeBonus = entry.nodeKind == MapNodeKind.Hard ? 9000 :
                entry.nodeKind == MapNodeKind.Finale ? 16000 : 0;
            int chapter = Mathf.Max(0, (entry.level - 1) / 10);
            int boardBonus = Mathf.Max(0, entry.rows * entry.columns - 64) * 170;
            int obstaclePressure = entry.obstacleCount * Mathf.Max(1, entry.obstacleDurability) * 180;
            // A score stage should require a sequence of deliberate moves,
            // not end after the first large match.  The curve remains gentle
            // in chapter one and grows more strongly in the expert worlds.
            return 12000 + entry.level * 2400 + chapter * chapter * 3500 +
                boardBonus + obstaclePressure + challengeBonus;
        }

        public static int BalancedTargetAmount(CampaignLevelEntry entry)
        {
            if (entry == null) return 12;
            int challengeBonus = entry.nodeKind == MapNodeKind.Hard ? 3 :
                entry.nodeKind == MapNodeKind.Finale ? 5 : 0;
            if (entry.objectiveKind == CampaignObjectiveKind.LongMatch)
                return Mathf.Clamp(3 + entry.difficulty + entry.level / 15 + challengeBonus / 2, 4, 12);
            if (entry.objectiveKind == CampaignObjectiveKind.Cascades)
                return Mathf.Clamp(3 + entry.difficulty + entry.level / 18 + challengeBonus / 2, 4, 12);
            if (entry.objectiveKind == CampaignObjectiveKind.ClearObstacles)
                return Mathf.Max(1, entry.obstacleCount);
            return 14 + Mathf.CeilToInt(entry.level * 0.9f) + challengeBonus;
        }

        private static void EnsureZones(CampaignCatalog catalog)
        {
            if (catalog.zones == null || catalog.zones.Count < 5)
                PopulateZones(catalog);
        }

        private static void PopulateZones(CampaignCatalog catalog)
        {
            catalog.zones = new List<CampaignZoneEntry>
            {
                Zone("pradera_feliz", "PRADERA FELIZ", "Primeros pasos", 1, 10,
                    new Color(0.40f, 0.82f, 0.96f, 1f), new Color(0.34f, 0.72f, 0.20f, 1f),
                    new Color(1f, 0.72f, 0.15f, 1f)),
                Zone("bosque_aventura", "BOSQUE AVENTURA", "Nuevos retos", 11, 20,
                    new Color(0.24f, 0.64f, 0.62f, 1f), new Color(0.12f, 0.46f, 0.22f, 1f),
                    new Color(0.98f, 0.50f, 0.16f, 1f)),
                Zone("festival_canino", "FESTIVAL CANINO", "La gran celebracion", 21, 30,
                    new Color(0.48f, 0.34f, 0.72f, 1f), new Color(0.18f, 0.28f, 0.52f, 1f),
                    new Color(1f, 0.78f, 0.18f, 1f)),
                Zone("costa_dorada", "COSTA DORADA", "Aventura junto al mar", 31, 40,
                    new Color(0.24f, 0.82f, 0.98f, 1f), new Color(0.92f, 0.67f, 0.25f, 1f),
                    new Color(0.10f, 0.82f, 0.78f, 1f)),
                Zone("cumbres_nevadas", "CUMBRES NEVADAS", "El reto definitivo", 41, 50,
                    new Color(0.36f, 0.52f, 0.82f, 1f), new Color(0.66f, 0.82f, 0.91f, 1f),
                    new Color(0.42f, 0.92f, 1f, 1f))
            };
        }

        private static CampaignZoneEntry Zone(string id, string name, string subtitle, int first, int last,
            Color sky, Color ground, Color accent)
        {
            return new CampaignZoneEntry
            {
                id = id,
                displayName = name,
                subtitle = subtitle,
                firstLevel = first,
                lastLevel = last,
                skyColor = sky,
                groundColor = ground,
                accentColor = accent
            };
        }
    }
}
