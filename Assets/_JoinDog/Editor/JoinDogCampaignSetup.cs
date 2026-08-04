using System.IO;
using JoinDog.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace JoinDog.EditorTools
{
    public static class JoinDogCampaignSetup
    {
        private const string Root = "Assets/_JoinDog";
        private const string SceneFolder = Root + "/Scenes";
        private const string CampaignFolder = Root + "/Resources/Campaign";
        private const string BackgroundPath = "Assets/_DogCrush/Art/Backgrounds/dogcrush-park-background-v1.png";
        private const string DogPath = "Assets/_DogCrush/Resources/Pieces/piece-dog-v2.png";

        [MenuItem("JOIN DOG/Generate Campaign Structure")]
        public static void GenerateCampaignStructure()
        {
            Directory.CreateDirectory(SceneFolder);
            Directory.CreateDirectory(CampaignFolder);
            CreateCampaignAsset();
            CreateBootScene();
            CreateMainMenuScene();
            CreateWorldMapScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JOIN DOG] Campaign structure generated: Boot, MainMenu, WorldMap and 30 levels.");
        }

        private static void CreateCampaignAsset()
        {
            string path = CampaignFolder + "/ParqueCentral.asset";
            CampaignCatalog catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            CampaignCatalog.PopulateDefaults(catalog);
            EditorUtility.SetDirty(catalog);
        }

        private static void CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BootController").AddComponent<BootController>();
            EditorSceneManager.SaveScene(scene, SceneFolder + "/Boot.unity");
        }

        private static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            AddEventSystem();
            MainMenuScreenController controller = new GameObject("MainMenuScreen").AddComponent<MainMenuScreenController>();
            controller.backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            controller.dogSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DogPath);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene, SceneFolder + "/MainMenu.unity");
        }

        private static void CreateWorldMapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            AddEventSystem();
            WorldMapScreenController controller = new GameObject("WorldMapScreen").AddComponent<WorldMapScreenController>();
            controller.backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            controller.dogSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DogPath);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(scene, SceneFolder + "/WorldMap.unity");
        }

        private static void AddCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.18f, 0.24f);
            camera.orthographic = true;
        }

        private static void AddEventSystem()
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.GetComponent<InputSystemUIInputModule>();
        }

        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneFolder + "/Boot.unity", true),
                new EditorBuildSettingsScene(SceneFolder + "/MainMenu.unity", true),
                new EditorBuildSettingsScene(SceneFolder + "/WorldMap.unity", true),
                new EditorBuildSettingsScene("Assets/_DogCrush/Scenes/Gameplay.unity", true)
            };
        }
    }
}
