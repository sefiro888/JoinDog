using System.Collections;
using JoinDog.App;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DogCrush.Tests.PlayMode
{
    public class GuideExperienceTests
    {
        [UnityTest]
        public IEnumerator Guide_AllPagesFitPortraitAndNavigationClosesCleanly()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            var canvas = GameObject.Find("MainMenuCanvas").GetComponent<Canvas>();
            var cameraObject = new GameObject("GuideReviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var frame = new RenderTexture(390, 844, 24);
            var pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = frame;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.cullingMask = 1 << 30;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                GameObject.Find("Help").GetComponent<Button>().onClick.Invoke();
                for (int page = 0; page < 6; page++)
                {
                    yield return null;
                    foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                    Canvas.ForceUpdateCanvases();
                    var body = GameObject.Find("MenuModal").transform.Find("Card/Body").GetComponent<TextMeshProUGUI>();
                    body.ForceMeshUpdate();
                    Assert.That(body.isTextOverflowing, Is.False, "Guide page " + page + " must fit a portrait phone.");
                    foreach (string name in new[] { "GuidePrevious", "GuideNext" })
                    {
                        var label = GameObject.Find(name).GetComponentInChildren<TextMeshProUGUI>();
                        label.ForceMeshUpdate();
                        Assert.That(label.isTextOverflowing, Is.False, name + " label must remain readable.");
                    }
                    var illustration = GameObject.Find("GuideIllustration").GetComponent<Image>();
                    Assert.That(illustration.sprite, Is.Not.Null);
                    Assert.That(illustration.raycastTarget, Is.False, "Artwork must not block navigation.");
                    Assert.That(GameObject.Find("GuidePrevious").GetComponent<Button>().interactable, Is.EqualTo(page > 0));
                    yield return null;
                    camera.Render();
                    RenderTexture.active = frame;
                    pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
                    pixels.Apply();
                    string directory = System.IO.Path.Combine(Application.dataPath, "../Builds/visual-qa");
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "guide-page-" + (page + 1) + ".png"), pixels.EncodeToPNG());
                    GameObject.Find("GuideNext").GetComponent<Button>().onClick.Invoke();
                }
                yield return null;
                Assert.That(GameObject.Find("MenuModal"), Is.Null, "Final action must dismiss the guide.");
                Assert.That(GameObject.Find("Play").GetComponent<Button>().interactable, Is.True);
            }
            finally
            {
                RenderTexture.active = previous;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                camera.targetTexture = null;
                frame.Release();
                Object.Destroy(frame);
                Object.Destroy(pixels);
                Object.Destroy(cameraObject);
            }
        }
    }
}
