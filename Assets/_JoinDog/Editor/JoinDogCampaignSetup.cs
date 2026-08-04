using System.Collections.Generic;
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
            CampaignCatalog catalog = CreateCampaignAsset();
            ValidateCampaign(catalog);
            CreateBootScene();
            CreateMainMenuScene();
            CreateWorldMapScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JOIN DOG] Campaign structure generated: Boot, MainMenu, WorldMap and 30 levels.");
        }

        [MenuItem("JOIN DOG/Refresh Campaign Data")]
        public static void RefreshCampaignData()
        {
            Directory.CreateDirectory(CampaignFolder);
            CreateCampaignAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JOIN DOG] Campaign data refreshed: 30 designed levels.");
        }

        private static CampaignCatalog CreateCampaignAsset()
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
            return catalog;
        }

        private static void ValidateCampaign(CampaignCatalog catalog)
        {
            if (catalog == null || catalog.levels == null || catalog.levels.Count != CampaignCatalog.MaxLevel)
                throw new InvalidDataException("JoinDog campaign must contain exactly 30 levels.");

            HashSet<string> ids = new HashSet<string>();
            HashSet<int> numbers = new HashSet<int>();
            foreach (CampaignLevelEntry entry in catalog.levels)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id))
                    throw new InvalidDataException("JoinDog campaign contains an empty or duplicated level id.");
                if (!numbers.Add(entry.level) || entry.level < 1 || entry.level > CampaignCatalog.MaxLevel)
                    throw new InvalidDataException($"JoinDog campaign contains invalid level number {entry.level}.");
                if (entry.rows < 2 || entry.columns < 2 || entry.durationSeconds < 15 || entry.rewardTreats < 0)
                    throw new InvalidDataException($"JoinDog level {entry.level} has invalid gameplay values.");
            }
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
