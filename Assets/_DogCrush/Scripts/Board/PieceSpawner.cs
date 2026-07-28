using System.Collections.Generic;
using UnityEngine;

namespace DogCrush.Board
{
    public class PieceSpawner : MonoBehaviour
    {
        [Header("Prefab & Parent")]
        public PieceView piecePrefab;
        public Transform piecesContainer;

        [Header("Piece Sprites & Colors")]
        public Sprite dogSprite;
        public Sprite boneSprite;
        public Sprite ballSprite;
        public Sprite foodSprite;
        public Sprite collarSprite;

        public Color dogColor = new Color(0.95f, 0.65f, 0.25f);
        public Color boneColor = new Color(0.9f, 0.9f, 0.95f);
        public Color ballColor = new Color(0.3f, 0.75f, 0.95f);
        public Color foodColor = new Color(0.95f, 0.35f, 0.4f);
        public Color collarColor = new Color(0.45f, 0.85f, 0.45f);

        private readonly Queue<PieceView> pool = new Queue<PieceView>();

        private void Awake()
        {
            LoadSpritesIfNull();
        }

        public void LoadSpritesIfNull()
        {
            // Prefer the Resources copies at runtime. This avoids relying on a
            // serialized Sprite sub-asset reference that can be stripped or
            // become invalid in a WebGL build.
            dogSprite = LoadResourceSprite("Pieces/piece-dog-v2") ?? dogSprite;
            boneSprite = LoadResourceSprite("Pieces/piece-bone-v2") ?? boneSprite;
            ballSprite = LoadResourceSprite("Pieces/piece-ball-v2") ?? ballSprite;
            foodSprite = LoadResourceSprite("Pieces/piece-food-v2") ?? foodSprite;
            collarSprite = LoadResourceSprite("Pieces/piece-collar-v2") ?? collarSprite;
        }

        private static Sprite LoadResourceSprite(string path)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(path);
            if (sprites != null && sprites.Length > 0)
                return sprites[0];

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;

            // Last-resort WebGL-safe fallback: load the texture and create the
            // Sprite explicitly when Unity did not expose the PNG sub-asset.
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        public PieceView SpawnPiece(PieceType type, int gridX, int gridY, Vector3 spawnWorldPos)
        {
            PieceView piece;
            if (pool.Count > 0)
            {
                piece = pool.Dequeue();
                piece.gameObject.SetActive(true);
            }
            else
            {
                piece = Instantiate(piecePrefab, piecesContainer != null ? piecesContainer : transform);
            }

            piece.transform.position = spawnWorldPos;
            Sprite icon = GetSpriteForType(type);
            Color color = GetColorForType(type);

            piece.Initialize(type, gridX, gridY, icon, color);
            return piece;
        }

        public void RecyclePiece(PieceView piece)
        {
            if (piece == null) return;
            piece.SetSelected(false);
            piece.gameObject.SetActive(false);
            pool.Enqueue(piece);
        }

        public Sprite GetSpriteForType(PieceType type)
        {
            switch (type)
            {
                case PieceType.Dog: return dogSprite;
                case PieceType.Bone: return boneSprite;
                case PieceType.Ball: return ballSprite;
                case PieceType.Food: return foodSprite;
                case PieceType.Collar: return collarSprite;
                default: return null;
            }
        }

        public Color GetColorForType(PieceType type)
        {
            // The canonical v2 sprites are final-color illustrations. Tinting
            // them here made the dog orange, the bone grey and the collar
            // fluorescent in WebGL.
            return Color.white;
        }
    }
}
