using System.Collections;
using DogCrush.Board;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DogCrush.Tests.PlayMode
{
    public class PieceIdentityTests
    {
        [UnityTest]
        public IEnumerator WarmFrisbee_UsesCanonicalArtAndRendersBesideOtherSmallPieces()
        {
            var root = new GameObject("PieceIdentityReview");
            var camera = root.AddComponent<Camera>();
            var spawner = root.AddComponent<PieceSpawner>();
            var frame = new RenderTexture(390, 180, 24);
            var pixels = new Texture2D(390, 180, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Sprite frisbee = spawner.GetSpriteForType(PieceType.Frisbee);
                Assert.That(frisbee, Is.Not.Null);
                Assert.That(frisbee, Is.SameAs(Resources.Load<Sprite>("Magic/frisbee-coral-v2")));
                Assert.That(frisbee, Is.Not.SameAs(spawner.GetSpriteForType(PieceType.Ball)));
                camera.orthographic = true;
                camera.orthographicSize = 1.3f;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f,.055f,.13f);
                camera.targetTexture = frame;
                PieceType[] types = { PieceType.Ball, PieceType.Frisbee, PieceType.Food, PieceType.Duck, PieceType.Collar, PieceType.Dog };
                for (int y = 0; y < 2; y++)
                    for (int x = 0; x < types.Length; x++)
                    {
                        var piece = new GameObject("IdentityPiece").AddComponent<PieceView>();
                        piece.transform.SetParent(root.transform);
                        piece.gameObject.layer = 30;
                        piece.Initialize(types[x], x, y, spawner.GetSpriteForType(types[x]), Color.white);
                        piece.transform.position = new Vector3(-2.15f + x * .86f, y == 0 ? .45f : -.45f, 0);
                    }
                yield return null;
                camera.Render();
                RenderTexture.active = frame;
                pixels.ReadPixels(new Rect(0,0,390,180),0,0);
                pixels.Apply();
                string directory = System.IO.Path.Combine(Application.dataPath,"../Builds/visual-qa");
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"piece-identities-small.png"),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                frame.Release();
                Object.Destroy(frame);
                Object.Destroy(pixels);
                Object.Destroy(root);
            }
        }
    }
}
