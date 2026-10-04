using DogCrush.Core;
using JoinDog.App;
using UnityEngine;

namespace DogCrush.Presentation
{
    /// <summary>World identity and low-cost ambient motion, kept outside playable cells.</summary>
    public sealed class GameplayWorldAtmosphere : MonoBehaviour
    {
        private SpriteRenderer background;
        private SpriteRenderer[] motes;
        private Camera view;
        private Color accent;
        private int chapter;
        private Sprite originalSprite;
        private SpriteRenderer originalBackground;
        private Vector3 originalScale;
        public string ZoneId { get; private set; }
        public int AmbientCount => motes != null ? motes.Length : 0;

        public void ApplyLevel(int level)
        {
            view = Camera.main;
            var zone = CampaignCatalog.LoadOrCreateRuntime().GetZoneForLevel(level);
            if (zone == null) return;
            ZoneId = zone.id;
            chapter = Mathf.Clamp((level - 1) / 10, 0, 9);
            accent = zone.accentColor;
            if (originalBackground == null)
            {
                var original = GameObject.Find("DogParkBackground");
                originalBackground = original != null ? original.GetComponent<SpriteRenderer>() : null;
                if (originalBackground != null)
                {
                    originalSprite = originalBackground.sprite;
                    originalScale = originalBackground.transform.localScale;
                }
            }
            if (background == null)
            {
                var go = new GameObject("GameplayWorldBackdrop", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                background = go.GetComponent<SpriteRenderer>();
                background.sortingOrder = -94;
            }
            background.sprite = WorldMapArtLibrary.LoadBackground(zone.id) ?? originalSprite;
            background.color = new Color(.78f, .78f, .82f, 1f);
            if (originalBackground != null) originalBackground.enabled = background.sprite == null;
            if (motes == null)
            {
                Sprite sparkle = Resources.Load<Sprite>("UI/icon-score-star");
                motes = new SpriteRenderer[12];
                for (int i = 0; i < motes.Length; i++)
                {
                    var go = new GameObject("WorldMote_" + i, typeof(SpriteRenderer));
                    go.transform.SetParent(transform, false);
                    motes[i] = go.GetComponent<SpriteRenderer>();
                    motes[i].sprite = sparkle;
                    motes[i].sortingOrder = -90;
                }
            }
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (view == null || background == null || background.sprite == null) return;
            float height = view.orthographicSize * 2f;
            float width = height * view.aspect;
            Vector2 size = background.sprite.bounds.size;
            float scale = Mathf.Max(width / size.x, height / size.y);
            background.transform.position = new Vector3(view.transform.position.x, view.transform.position.y, 8f);
            background.transform.localScale = Vector3.one * scale;
        }

        private void LateUpdate()
        {
            if (view == null || motes == null) return;
            UpdateLayout();
            bool motion = !AccessibilitySettings.ReducedMotion;
            float t = motion ? Time.time * .16f : 0f;
            for (int i = 0; i < motes.Length; i++)
            {
                SpriteRenderer mote = motes[i];
                mote.enabled = motion && mote.sprite != null;
                if (!mote.enabled) continue;
                // Thin edge lanes leave the board and all touch targets still.
                float x = i % 2 == 0 ? .015f : .985f;
                float y = .08f + Mathf.Repeat(i * .137f + t * (chapter == 4 ? -.18f : .12f), .84f);
                Vector3 position = view.ViewportToWorldPoint(new Vector3(x, y, 8f - view.transform.position.z));
                position.z = 7f;
                mote.transform.position = position;
                float diameter = .05f + .025f * (i % 3);
                mote.transform.localScale = Vector3.one * diameter / Mathf.Max(.01f, mote.sprite.bounds.size.x);
                mote.transform.rotation = Quaternion.Euler(0, 0, i * 33f + t * 18f);
                Color tint = Color.Lerp(accent, Color.white, .55f);
                tint.a = .20f + .14f * Mathf.Sin(t * 2f + i);
                mote.color = tint;
            }
        }

        private void OnDestroy()
        {
            if (originalBackground == null) return;
            originalBackground.enabled = true;
            originalBackground.sprite = originalSprite;
            originalBackground.transform.localScale = originalScale;
        }
    }
}
