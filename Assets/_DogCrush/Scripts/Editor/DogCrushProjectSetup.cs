using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using DogCrush.Board;
using DogCrush.Core;
using DogCrush.Gameplay;
using DogCrush.InputSystem;
using DogCrush.Presentation;
using DogCrush.UI;

namespace DogCrush.EditorTool
{
    public static class DogCrushProjectSetup
    {
        [MenuItem("DOGCRUSH/Build Playable Prototype")]
        public static void BuildPrototype()
        {
            Debug.Log("[DOGCRUSH] Building v0.4 Ultra-Perfect Mobile Edition...");

            EnsureDirectoriesExist();

            Sprite dogSprite = CreateIconTexture("dog_icon", 1024, IconShape.Dog);
            Sprite boneSprite = CreateIconTexture("bone_icon", 1024, IconShape.Bone);
            Sprite ballSprite = CreateIconTexture("ball_icon", 1024, IconShape.Ball);
            Sprite foodSprite = CreateIconTexture("food_icon", 1024, IconShape.Food);
            Sprite collarSprite = CreateIconTexture("collar_icon", 1024, IconShape.Collar);

            Sprite glowSprite = CreateGlowTexture("piece_glow", 256);
            Sprite pawParticleSprite = CreatePawParticleTexture("paw_particle", 128);
            Sprite bgSprite = CreateDogParkBackgroundTexture("dog_park_bg", 512, 1024);
            Sprite frameSprite = CreateBoardFrameTexture("board_frame", 512, 640);
            Sprite panelSprite = CreateBoardPanelTexture("board_panel", 512, 640);
            Sprite timerFillSprite = CreateBarFillTexture("timer_bar_fill", 256, 32);

            BoardConfig config = CreateOrLoadBoardConfig();
            PieceView piecePrefab = CreateOrUpdatePiecePrefab(glowSprite);

            SetupGameplayScene(config, piecePrefab, dogSprite, boneSprite, ballSprite, foodSprite, collarSprite,
                pawParticleSprite, bgSprite, frameSprite, panelSprite, timerFillSprite);

            AddSceneToBuildSettings("Assets/_DogCrush/Scenes/Gameplay.unity");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DOGCRUSH] v0.4 Ultra-Perfect Mobile Edition built successfully! Open Assets/_DogCrush/Scenes/Gameplay.unity and press Play.");
        }

        [MenuItem("DOGCRUSH/Build WebGL for GitHub Pages")]
        public static void BuildWebGL()
        {
            Debug.Log("[DOGCRUSH] Starting WebGL Build for GitHub Pages...");

            BuildPrototype();

            string outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "docs");
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Ensure active build target is set to WebGL
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            // Optimize for size & GitHub Pages limits (< 50MB file size limit)
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "PROJECT:DogCrushTemplate";
            PlayerSettings.SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Il2CppCompilerConfiguration.Master);
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);

            // Ensure scene spawner has all 5 piece sprites assigned and saved
            string scenePath = "Assets/_DogCrush/Scenes/Gameplay.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            PieceSpawner spawner = Object.FindAnyObjectByType<PieceSpawner>();
            if (spawner != null)
            {
                spawner.dogSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_DogCrush/Art/Pieces/dog_icon.png");
                spawner.boneSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_DogCrush/Art/Pieces/bone_icon.png");
                spawner.ballSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_DogCrush/Art/Pieces/ball_icon.png");
                spawner.foodSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_DogCrush/Art/Pieces/food_icon.png");
                spawner.collarSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_DogCrush/Art/Pieces/collar_icon.png");
                spawner.LoadSpritesIfNull();
                EditorUtility.SetDirty(spawner);
            }
            EditorSceneManager.SaveScene(scene);

            BuildPlayerOptions buildOptions = new BuildPlayerOptions();
            buildOptions.scenes = new string[] { scenePath };
            buildOptions.locationPathName = outputFolder;
            buildOptions.target = BuildTarget.WebGL;
            buildOptions.targetGroup = BuildTargetGroup.WebGL;
            buildOptions.options = BuildOptions.CleanBuildCache;

            var report = BuildPipeline.BuildPlayer(buildOptions);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                // Create .nojekyll to prevent GitHub Pages from ignoring Unity WebGL files
                File.WriteAllText(Path.Combine(outputFolder, ".nojekyll"), "");

                // Ensure custom anti-cache index.html is used instead of generic Unity template
                string antiCacheHtml = @"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>Dog Crush - Match 3 Puzzle</title>
    <meta http-equiv=""Cache-Control"" content=""no-cache, no-store, must-revalidate"">
    <meta http-equiv=""Pragma"" content=""no-cache"">
    <meta http-equiv=""Expires"" content=""0"">
    <link rel=""shortcut icon"" href=""TemplateData/favicon.ico"">
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Fredoka+One&family=Nunito:wght@700;900&display=swap');
        * { margin: 0; padding: 0; box-sizing: border-box; }
        html, body { width: 100%; height: 100%; overflow: hidden; background: #0e1626; font-family: 'Nunito', sans-serif; }
        body { display: flex; justify-content: center; align-items: center; background: radial-gradient(circle at center, #1b2838 0%, #0b101d 100%); }
        #unity-container { position: relative; width: 100vw; height: 100vh; display: flex; justify-content: center; align-items: center; }
        #unity-canvas { width: 100%; height: 100%; display: block; background: transparent; }
        #loading-overlay { position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0f172a 100%); display: flex; flex-direction: column; justify-content: center; align-items: center; z-index: 9999; transition: opacity 0.5s ease; }
        #loading-overlay.hidden { opacity: 0; pointer-events: none; }
        .loading-title { font-family: 'Fredoka One', cursive; font-size: 3.5rem; background: linear-gradient(135deg, #fbbf24, #f59e0b, #ef4444); -webkit-background-clip: text; -webkit-text-fill-color: transparent; margin-bottom: 0.5rem; letter-spacing: 1px; filter: drop-shadow(0 4px 12px rgba(245,158,11,0.4)); }
        .loading-subtitle { color: rgba(255,255,255,0.6); font-size: 1rem; font-weight: 700; margin-bottom: 2.5rem; letter-spacing: 3px; }
        .progress-box { width: 300px; height: 16px; background: rgba(255,255,255,0.08); border-radius: 12px; border: 2px solid rgba(255,255,255,0.15); overflow: hidden; padding: 2px; }
        .progress-fill { height: 100%; width: 0%; background: linear-gradient(90deg, #10b981, #34d399); border-radius: 8px; transition: width 0.25s ease; }
        .progress-text { color: rgba(255,255,255,0.7); font-size: 0.9rem; font-weight: 900; margin-top: 1rem; }
    </style>
</head>
<body>
    <div id=""loading-overlay"">
        <div class=""loading-title"">DOG CRUSH</div>
        <div class=""loading-subtitle"">MATCH 3 PUZZLE</div>
        <div class=""progress-box""><div class=""progress-fill"" id=""progress-fill""></div></div>
        <div class=""progress-text"" id=""progress-text"">Cargando... 0%</div>
    </div>
    <div id=""unity-container""><canvas id=""unity-canvas"" tabindex=""-1""></canvas></div>
    <script>
        var canvas = document.querySelector(""#unity-canvas"");
        var progressFill = document.getElementById(""progress-fill"");
        var progressText = document.getElementById(""progress-text"");
        var loadingOverlay = document.getElementById(""loading-overlay"");
        var vToken = ""?v="" + new Date().getTime();
        var buildUrl = ""Build"";
        var config = {
            arguments: [],
            dataUrl: buildUrl + ""/docs.data"" + vToken,
            frameworkUrl: buildUrl + ""/docs.framework.js"" + vToken,
            codeUrl: buildUrl + ""/docs.wasm"" + vToken,
            streamingAssetsUrl: ""StreamingAssets"",
            companyName: ""DogCrush"",
            productName: ""DOGCRUSH"",
            productVersion: ""1.0"",
            devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),
        };
        var script = document.createElement(""script"");
        script.src = buildUrl + ""/docs.loader.js"" + vToken;
        script.onload = function() {
            createUnityInstance(canvas, config, function(progress) {
                var pct = Math.round(progress * 100);
                progressFill.style.width = pct + ""%"";
                progressText.textContent = ""Cargando... "" + pct + ""%"";
            }).then(function(unityInstance) {
                loadingOverlay.classList.add(""hidden"");
                setTimeout(function() { loadingOverlay.style.display = ""none""; }, 600);
            }).catch(function(message) {
                progressText.textContent = ""Error: "" + message;
                progressText.style.color = ""#ef4444"";
            });
        };
        document.body.appendChild(script);
    </script>
</body>
</html>";
                File.WriteAllText(Path.Combine(outputFolder, "index.html"), antiCacheHtml);
                Debug.Log("[DOGCRUSH] WebGL Build Succeeded! Custom anti-cache index.html saved in 'docs/' folder.");
            }
            else
            {
                Debug.LogError($"[DOGCRUSH] WebGL Build Failed: {report.summary.totalErrors} errors.");
            }
        }

        [MenuItem("DOGCRUSH/Import TMP Essential Resources")]
        public static void ImportTMPEssentialResources()
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DOGCRUSH] TMP Essential Resources imported.");
        }

        public static void BuildWebGLAudit()
        {
            string scenePath = "Assets/_DogCrush/Scenes/Gameplay.unity";
            string outputFolder = "Builds/CodexPreview";
            Directory.CreateDirectory(outputFolder);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputFolder,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[DOGCRUSH] WebGL audit build result: {report.summary.result}, errors: {report.summary.totalErrors}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException("WebGL audit build failed.");
            }
        }

        /// <summary>
        /// Builds the already-validated scene for GitHub Pages without calling
        /// BuildPrototype. BuildPrototype regenerates legacy art assets and
        /// must never run as part of the release pipeline.
        /// </summary>
        public static void BuildWebGLRelease()
        {
            string scenePath = "Assets/_DogCrush/Scenes/Gameplay.unity";
            string outputFolder = "docs";
            Directory.CreateDirectory(outputFolder);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "PROJECT:DogCrushTemplate";

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputFolder,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            File.WriteAllText(Path.Combine(outputFolder, ".nojekyll"), string.Empty);
            Debug.Log($"[DOGCRUSH] WebGL release build result: {report.summary.result}, errors: {report.summary.totalErrors}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException("WebGL release build failed.");
            }
        }

        private static void EnsureDirectoriesExist()
        {
            string[] dirs = new string[]
            {
                "Assets/_DogCrush/Art/Pieces",
                "Assets/_DogCrush/Art/Backgrounds",
                "Assets/_DogCrush/Art/UI",
                "Assets/_DogCrush/Art/Effects",
                "Assets/_DogCrush/Data",
                "Assets/_DogCrush/Prefabs/Pieces",
                "Assets/_DogCrush/Prefabs/UI",
                "Assets/_DogCrush/Scenes"
            };

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        private enum IconShape { Dog, Bone, Ball, Food, Collar }

        private static Sprite CreateIconTexture(string name, int size, IconShape shape)
        {
            string path = $"Assets/_DogCrush/Art/Pieces/{name}.png";
            if (File.Exists(path))
            {
                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null) return existing;
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            float center = size / 2f;
            float radius = size * 0.40f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    bool outerBody = false;
                    bool whiteBorder = false;
                    bool shadow = false;
                    Color featureColor = Color.clear;
                    bool hasFeature = false;

                    // Drop Shadow offset (downwards right)
                    float sdx = dx + 18f;
                    float sdy = dy + 24f;
                    float sdist = Mathf.Sqrt(sdx * sdx + sdy * sdy);

                    switch (shape)
                    {
                        case IconShape.Dog:
                            // Puppy face: Rounded Head + Floppy ears + Snout + Eyes + Nose + Tongue + Blush
                            bool head = dist <= radius;
                            bool earL = Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.72f, radius * 0.42f)) <= radius * 0.46f;
                            bool earR = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius * 0.72f, radius * 0.42f)) <= radius * 0.46f;

                            bool shearL = Vector2.Distance(new Vector2(sdx, sdy), new Vector2(-radius * 0.72f, radius * 0.42f)) <= radius * 0.46f;
                            bool shearR = Vector2.Distance(new Vector2(sdx, sdy), new Vector2(radius * 0.72f, radius * 0.42f)) <= radius * 0.46f;

                            outerBody = head || earL || earR;
                            shadow = (sdist <= radius || shearL || shearR) && !outerBody;

                            // White Sticker Border
                            whiteBorder = (dist <= radius + 18f && dist >= radius - 6f) ||
                                          (Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.72f, radius * 0.42f)) <= radius * 0.46f + 14f &&
                                           Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.72f, radius * 0.42f)) >= radius * 0.46f - 6f) ||
                                          (Vector2.Distance(new Vector2(dx, dy), new Vector2(radius * 0.72f, radius * 0.42f)) <= radius * 0.46f + 14f &&
                                           Vector2.Distance(new Vector2(dx, dy), new Vector2(radius * 0.72f, radius * 0.42f)) >= radius * 0.46f - 6f);

                            // Inner Features
                            bool snout = Vector2.Distance(new Vector2(dx, dy), new Vector2(0f, -radius * 0.24f)) <= radius * 0.38f;
                            bool nose = Vector2.Distance(new Vector2(dx, dy), new Vector2(0f, -radius * 0.12f)) <= radius * 0.14f;
                            bool eyeL = Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.34f, radius * 0.18f)) <= radius * 0.11f;
                            bool eyeR = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius * 0.34f, radius * 0.18f)) <= radius * 0.11f;
                            bool specL = Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.31f, radius * 0.22f)) <= radius * 0.04f;
                            bool specR = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius * 0.31f, radius * 0.22f)) <= radius * 0.04f;
                            bool tongue = dy < -radius * 0.35f && dy > -radius * 0.58f && Mathf.Abs(dx) <= radius * 0.14f;
                            bool blushL = Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.50f, -radius * 0.05f)) <= radius * 0.12f;
                            bool blushR = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius * 0.50f, -radius * 0.05f)) <= radius * 0.12f;

                            if (specL || specR) { hasFeature = true; featureColor = Color.white; }
                            else if (eyeL || eyeR || nose) { hasFeature = true; featureColor = new Color(0.12f, 0.10f, 0.14f); }
                            else if (tongue) { hasFeature = true; featureColor = new Color(0.98f, 0.42f, 0.55f); }
                            else if (blushL || blushR) { hasFeature = true; featureColor = new Color(1.0f, 0.65f, 0.70f, 0.6f); }
                            else if (snout) { hasFeature = true; featureColor = new Color(0.98f, 0.94f, 0.88f); }
                            break;

                        case IconShape.Bone:
                            // Dog Bone: rounded bar + 4 corner knobs
                            bool bar = Mathf.Abs(dy) <= radius * 0.35f && Mathf.Abs(dx) <= radius * 0.76f;
                            bool c1 = Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.74f, radius * 0.44f)) <= radius * 0.38f;
                            bool c2 = Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.74f, -radius * 0.44f)) <= radius * 0.38f;
                            bool c3 = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius * 0.74f, radius * 0.44f)) <= radius * 0.38f;
                            bool c4 = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius * 0.74f, -radius * 0.44f)) <= radius * 0.38f;
                            outerBody = bar || c1 || c2 || c3 || c4;

                            bool sbar = Mathf.Abs(sdy) <= radius * 0.35f && Mathf.Abs(sdx) <= radius * 0.76f;
                            bool sc1 = Vector2.Distance(new Vector2(sdx, sdy), new Vector2(-radius * 0.74f, radius * 0.44f)) <= radius * 0.38f;
                            bool sc2 = Vector2.Distance(new Vector2(sdx, sdy), new Vector2(-radius * 0.74f, -radius * 0.44f)) <= radius * 0.38f;
                            bool sc3 = Vector2.Distance(new Vector2(sdx, sdy), new Vector2(radius * 0.74f, radius * 0.44f)) <= radius * 0.38f;
                            bool sc4 = Vector2.Distance(new Vector2(sdx, sdy), new Vector2(radius * 0.74f, -radius * 0.44f)) <= radius * 0.38f;
                            shadow = (sbar || sc1 || sc2 || sc3 || sc4) && !outerBody;

                            float boneHighlight = Mathf.Clamp01(1f - (Mathf.Abs(dy) / (radius * 0.35f)));
                            if (outerBody && dy > 0) { hasFeature = true; featureColor = Color.Lerp(new Color(0.96f, 0.96f, 0.98f), Color.white, boneHighlight * 0.4f); }
                            break;

                        case IconShape.Ball:
                            // Tennis Play Ball: Sphere + Curved White Seams
                            outerBody = dist <= radius;
                            shadow = (sdist <= radius) && !outerBody;

                            float curveY = Mathf.Sin(dx * 0.015f) * radius * 0.35f;
                            bool seam1 = Mathf.Abs(dy - curveY) <= radius * 0.12f;
                            if (seam1 && outerBody) { hasFeature = true; featureColor = Color.white; }
                            break;

                        case IconShape.Food:
                            // Kibble Bowl: Bowl + Kibble Heap + Bone Badge
                            bool bowl = dy <= radius * 0.1f && dy >= -radius * 0.72f && Mathf.Abs(dx) <= (radius * 0.88f - (dy * 0.22f));
                            bool heap = dy > radius * 0.05f && dist <= radius * 0.85f;
                            outerBody = bowl || heap;

                            bool sbowl = sdy <= radius * 0.1f && sdy >= -radius * 0.72f && Mathf.Abs(sdx) <= (radius * 0.88f - (sdy * 0.22f));
                            bool sheap = sdy > radius * 0.05f && sdist <= radius * 0.85f;
                            shadow = (sbowl || sheap) && !outerBody;

                            bool heapKibble = heap && (Mathf.Sin(dx * 0.08f) * Mathf.Cos(dy * 0.08f) > 0.1f);
                            bool boneBadge = Vector2.Distance(new Vector2(dx, dy), new Vector2(0f, -radius * 0.32f)) <= radius * 0.22f;

                            if (boneBadge) { hasFeature = true; featureColor = Color.white; }
                            else if (heapKibble) { hasFeature = true; featureColor = new Color(0.72f, 0.45f, 0.18f); }
                            break;

                        case IconShape.Collar:
                            // Dog Collar: Leather Ring + Golden Medal Tag
                            bool ring = dist <= radius && dist >= radius * 0.48f;
                            bool tag = Vector2.Distance(new Vector2(dx, dy), new Vector2(0f, -radius * 0.62f)) <= radius * 0.34f;
                            outerBody = ring || tag;

                            bool sring = sdist <= radius && sdist >= radius * 0.48f;
                            bool stag = Vector2.Distance(new Vector2(sdx, sdy), new Vector2(0f, -radius * 0.62f)) <= radius * 0.34f;
                            shadow = (sring || stag) && !outerBody;

                            bool tagCenter = Vector2.Distance(new Vector2(dx, dy), new Vector2(0f, -radius * 0.62f)) <= radius * 0.26f;
                            bool starInTag = tagCenter && Mathf.Abs(dx) * Mathf.Abs(dy + radius * 0.62f) < 120f;

                            if (starInTag) { hasFeature = true; featureColor = new Color(0.98f, 0.82f, 0.2f); }
                            else if (tagCenter) { hasFeature = true; featureColor = new Color(1.0f, 0.92f, 0.4f); }
                            break;
                    }

                    int idx = y * size + x;

                    if (hasFeature)
                    {
                        pixels[idx] = featureColor;
                    }
                    else if (outerBody)
                    {
                        // 3D Sphere/Radial Gradient Lighting
                        float radialLighting = Mathf.Clamp01(1f - (dist / radius));
                        float specularSheen = Mathf.Pow(Mathf.Clamp01(1f - (Vector2.Distance(new Vector2(dx, dy), new Vector2(-radius * 0.3f, radius * 0.3f)) / (radius * 0.8f))), 3f) * 0.35f;

                        Color baseColor = Color.white;
                        pixels[idx] = new Color(baseColor.r + specularSheen, baseColor.g + specularSheen, baseColor.b + specularSheen, 1f);
                    }
                    else if (whiteBorder)
                    {
                        pixels[idx] = Color.white;
                    }
                    else if (shadow)
                    {
                        pixels[idx] = new Color(0f, 0f, 0f, 0.35f);
                    }
                    else
                    {
                        pixels[idx] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateGlowTexture(string name, int size)
        {
            string path = $"Assets/_DogCrush/Art/Backgrounds/{name}.png";
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            float center = size / 2f;
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist / maxDist));
                    alpha = Mathf.Pow(alpha, 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreatePawParticleTexture(string name, int size)
        {
            string path = $"Assets/_DogCrush/Art/Effects/{name}.png";
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            float center = size / 2f;
            float radius = size * 0.28f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;

                    bool mainPad = Vector2.Distance(new Vector2(dx, dy), new Vector2(0f, -size * 0.1f)) <= radius;
                    bool toe1 = Vector2.Distance(new Vector2(dx, dy), new Vector2(-size * 0.28f, size * 0.22f)) <= radius * 0.38f;
                    bool toe2 = Vector2.Distance(new Vector2(dx, dy), new Vector2(-size * 0.1f, size * 0.35f)) <= radius * 0.38f;
                    bool toe3 = Vector2.Distance(new Vector2(dx, dy), new Vector2(size * 0.1f, size * 0.35f)) <= radius * 0.38f;
                    bool toe4 = Vector2.Distance(new Vector2(dx, dy), new Vector2(size * 0.28f, size * 0.22f)) <= radius * 0.38f;

                    if (mainPad || toe1 || toe2 || toe3 || toe4)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateDogParkBackgroundTexture(string name, int width, int height)
        {
            string path = $"Assets/_DogCrush/Art/Backgrounds/{name}.png";
            if (File.Exists(path))
            {
                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null) return existing;
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            Color skyTop = new Color(0.20f, 0.50f, 0.88f); // Deep Sky Blue
            Color skyBottom = new Color(0.62f, 0.85f, 0.98f);
            Color grassTop = new Color(0.30f, 0.78f, 0.40f); // Vibrant Mint Grass
            Color grassBottom = new Color(0.16f, 0.50f, 0.26f);

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / height;
                Color rowColor;

                if (t > 0.38f)
                {
                    float skyT = (t - 0.38f) / 0.62f;
                    rowColor = Color.Lerp(skyBottom, skyTop, skyT);
                }
                else
                {
                    float grassT = t / 0.38f;
                    rowColor = Color.Lerp(grassBottom, grassTop, grassT);
                }

                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = rowColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateBoardFrameTexture(string name, int width, int height)
        {
            string path = $"Assets/_DogCrush/Art/UI/{name}.png";
            if (File.Exists(path))
            {
                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null) return existing;
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            Color woodOuter = new Color(0.55f, 0.35f, 0.18f); // Rich Warm Mahogany Wood
            Color woodInner = new Color(0.75f, 0.48f, 0.26f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool border = (x < 22 || x >= width - 22 || y < 22 || y >= height - 22);
                    if (border)
                    {
                        pixels[y * width + x] = Color.Lerp(woodOuter, woodInner, (float)x / width);
                    }
                    else
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateBoardPanelTexture(string name, int width, int height)
        {
            string path = $"Assets/_DogCrush/Art/UI/{name}.png";
            if (File.Exists(path))
            {
                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null) return existing;
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            Color panelBg = new Color(0.10f, 0.14f, 0.20f, 0.88f); // Translucent Dark Slate Panel

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = panelBg;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateBarFillTexture(string name, int width, int height)
        {
            string path = $"Assets/_DogCrush/Art/UI/{name}.png";
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static BoardConfig CreateOrLoadBoardConfig()
        {
            string path = "Assets/_DogCrush/Data/DefaultBoardConfig.asset";
            BoardConfig config = AssetDatabase.LoadAssetAtPath<BoardConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardConfig>();
                config.columns = 8;
                config.rows = 10;
                config.typeCount = 5;
                config.pieceSpacing = 1.15f;
                config.fallSpeed = 16.0f;
                config.minChainLength = 3;
                config.gameDurationSeconds = 60.0f;
                AssetDatabase.CreateAsset(config, path);
            }
            return config;
        }

        private static PieceView CreateOrUpdatePiecePrefab(Sprite glowSprite)
        {
            string prefabPath = "Assets/_DogCrush/Prefabs/Pieces/PiecePrefab.prefab";

            GameObject go = new GameObject("PiecePrefab");
            SpriteRenderer mainSr = go.AddComponent<SpriteRenderer>();
            mainSr.sortingOrder = 10;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.52f;

            GameObject glowGo = new GameObject("SelectionGlow");
            glowGo.transform.SetParent(go.transform, false);
            SpriteRenderer glowSr = glowGo.AddComponent<SpriteRenderer>();
            glowSr.sprite = glowSprite;
            glowSr.color = new Color(1f, 0.88f, 0.15f, 0.85f);
            glowSr.sortingOrder = 9;
            glowGo.transform.localScale = Vector3.one * 1.40f;
            glowGo.SetActive(false);

            PieceView view = go.AddComponent<PieceView>();
            view.mainRenderer = mainSr;
            view.selectionGlow = glowSr;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            return prefab.GetComponent<PieceView>();
        }

        private static void SetupGameplayScene(BoardConfig config, PieceView piecePrefab,
            Sprite dog, Sprite bone, Sprite ball, Sprite food, Sprite collar,
            Sprite pawParticle, Sprite dogParkBg, Sprite boardFrame, Sprite boardPanel, Sprite timerFill)
        {
            string scenePath = "Assets/_DogCrush/Scenes/Gameplay.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera Setup
            GameObject camGo = new GameObject("Main Camera");
            camGo.transform.position = new Vector3(0f, -0.4f, -10f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7.2f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.backgroundColor = new Color(0.12f, 0.16f, 0.22f);
            cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // 2. Dog Park Background Backdrop
            GameObject bgGo = new GameObject("DogParkBackground");
            bgGo.transform.position = new Vector3(0f, 0f, 10f);
            SpriteRenderer bgSr = bgGo.AddComponent<SpriteRenderer>();
            bgSr.sprite = dogParkBg;
            bgSr.sortingOrder = -100;
            bgGo.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            // 3. Board Frame & Panel Backdrop
            GameObject frameGo = new GameObject("BoardFrame");
            frameGo.transform.position = new Vector3(0f, -0.3f, 1f);
            SpriteRenderer frameSr = frameGo.AddComponent<SpriteRenderer>();
            frameSr.sprite = boardFrame;
            frameSr.sortingOrder = 1;
            frameGo.transform.localScale = new Vector3(1.68f, 1.72f, 1f);

            GameObject panelGo = new GameObject("BoardPanel");
            panelGo.transform.position = new Vector3(0f, -0.3f, 2f);
            panelGo.SetActive(false);

            // 4. Game Managers
            GameObject managerGo = new GameObject("[GameManager]");
            GameStateController stateController = managerGo.AddComponent<GameStateController>();
            BoardController boardController = managerGo.AddComponent<BoardController>();
            BoardGravityController gravityController = managerGo.AddComponent<BoardGravityController>();
            ChainSelectionController selectionController = managerGo.AddComponent<ChainSelectionController>();
            ChainInputHandler inputHandler = managerGo.AddComponent<ChainInputHandler>();
            ScoreController scoreController = managerGo.AddComponent<ScoreController>();
            GameTimer gameTimer = managerGo.AddComponent<GameTimer>();
            FeedbackController feedbackController = managerGo.AddComponent<FeedbackController>();
            AudioPlaceholderController audioController = managerGo.AddComponent<AudioPlaceholderController>();
            HapticFeedbackController hapticController = managerGo.AddComponent<HapticFeedbackController>();
            ParticleEffectController particleController = managerGo.AddComponent<ParticleEffectController>();
            particleController.pawSprite = pawParticle;
            GameBootstrap bootstrap = managerGo.AddComponent<GameBootstrap>();

            // 5. Piece Spawner
            GameObject spawnerGo = new GameObject("[PieceSpawner]");
            PieceSpawner spawner = spawnerGo.AddComponent<PieceSpawner>();
            spawner.piecePrefab = piecePrefab;
            spawner.piecesContainer = new GameObject("[BoardContainer]").transform;

            spawner.dogSprite = dog;
            spawner.boneSprite = bone;
            spawner.ballSprite = ball;
            spawner.foodSprite = food;
            spawner.collarSprite = collar;

            spawner.dogColor = new Color(1.0f, 0.60f, 0.12f);  // Warm Orange
            spawner.boneColor = new Color(0.96f, 0.96f, 0.98f); // Soft Bone White
            spawner.ballColor = new Color(0.18f, 0.78f, 0.95f); // Bright Cyan
            spawner.foodColor = new Color(0.96f, 0.32f, 0.38f); // Coral Red
            spawner.collarColor = new Color(0.28f, 0.85f, 0.48f); // Mint Green

            // 6. Line View
            GameObject lineGo = new GameObject("[ChainLineView]");
            LineRenderer lr = lineGo.AddComponent<LineRenderer>();
            ChainLineView lineView = lineGo.AddComponent<ChainLineView>();
            lineView.lineRenderer = lr;
            lr.startColor = new Color(1f, 0.85f, 0.15f, 0.95f);
            lr.endColor = new Color(1f, 0.48f, 0.1f, 0.95f);
            lr.startWidth = 0.13f;
            lr.endWidth = 0.13f;

            // Wire Controller Dependencies
            boardController.config = config;
            boardController.spawner = spawner;

            gravityController.boardController = boardController;
            gravityController.spawner = spawner;

            selectionController.boardController = boardController;
            selectionController.inputHandler = inputHandler;
            selectionController.lineView = lineView;

            bootstrap.stateController = stateController;
            bootstrap.boardController = boardController;
            bootstrap.gravityController = gravityController;
            bootstrap.selectionController = selectionController;
            bootstrap.scoreController = scoreController;
            bootstrap.gameTimer = gameTimer;
            bootstrap.feedbackController = feedbackController;
            bootstrap.particleController = particleController;
            bootstrap.audioController = audioController;
            bootstrap.hapticController = hapticController;

            // 7. Canvas UI Setup
            Canvas canvas = CreateGameplayCanvas(bootstrap, feedbackController, timerFill);
            feedbackController.uiCanvas = canvas;
            feedbackController.mainCamera = cam;

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static Canvas CreateGameplayCanvas(GameBootstrap bootstrap, FeedbackController feedbackController, Sprite timerFill)
        {
            GameObject canvasGo = new GameObject("[Canvas]");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // GameplayUIController will construct clean capsule UI dynamically at runtime
            GameplayUIController uiCtrl = canvasGo.AddComponent<GameplayUIController>();
            bootstrap.uiController = uiCtrl;

            // Event System
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("[EventSystem]");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }

            return canvas;
        }

        private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string content, float size, TextAlignmentOptions align, Vector2 minAnchor, Vector2 maxAnchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = minAnchor;
            rect.anchorMax = maxAnchor;
            rect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;

            return tmp;
        }

        private static GameObject CreateSimpleButton(string name, Transform parent, string label, Vector2 minAnchor, Vector2 maxAnchor, Color btnColor)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            RectTransform rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = minAnchor;
            rect.anchorMax = maxAnchor;
            rect.sizeDelta = Vector2.zero;

            Image img = btnGo.GetComponent<Image>();
            img.color = btnColor;

            CreateTMPText("Label", btnGo.transform, label, 30, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);

            return btnGo;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
            foreach (var s in original)
            {
                if (s.path == scenePath) return;
            }

            EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[original.Length + 1];
            System.Array.Copy(original, newScenes, original.Length);
            newScenes[original.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
        }
    }
}
