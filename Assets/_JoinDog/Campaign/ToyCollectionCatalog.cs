using UnityEngine;

namespace JoinDog.App
{
    public static class ToyCollectionCatalog
    {
        public readonly struct Figure
        {
            public readonly string Name, Resource;
            public readonly int Level;
            public Figure(string name, string resource, int level = 1)
            { Name = name; Resource = "Pieces/piece-" + resource; Level = level; }
        }

        public static readonly Figure[] Figures = {
            new Figure("PERRITO", "dog-v2"), new Figure("HUESO", "bone-v2"),
            new Figure("PELOTA", "ball-v2"), new Figure("COMEDERO", "food-v2"),
            new Figure("COLLAR", "collar-v2"), new Figure("PATITO", "duck-v1", 11)
        };

        public static int DiscoveredCount(int earnedLevel)
        {
            int count = 0;
            foreach (Figure figure in Figures) if (earnedLevel >= figure.Level) count++;
            return count;
        }

        public static string NextHint(int earnedLevel)
        {
            foreach (Figure figure in Figures)
                if (figure.Level > earnedLevel)
                {
                    int remaining = figure.Level - Mathf.Max(1, earnedLevel);
                    return $"{figure.Name}: supera {remaining} " +
                        (remaining == 1 ? "nivel más" : "niveles más") + $" · NIVEL {figure.Level}";
                }
            return "¡HAS DESCUBIERTO LAS 6 FIGURAS ACTUALES!";
        }
    }
}
