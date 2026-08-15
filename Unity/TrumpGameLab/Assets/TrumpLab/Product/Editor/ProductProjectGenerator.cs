#nullable enable

using System;
using System.IO;
using System.Linq;
using TrumpLab.Product;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrumpLab.Product.Editor
{
    public static class ProductProjectGenerator
    {
        private const string Root = "Assets/TrumpLab/Product";
        private const string PrefabDirectory = Root + "/Prefabs/Screens";
        private const string SceneDirectory = Root + "/Scenes";
        private const string ScenePath = SceneDirectory + "/Bootstrap.unity";

        [MenuItem("Trump Lab/Regenerate Product Scaffold")]
        public static void GenerateFromMenu() => Generate();

        public static void GenerateCommandLine()
        {
            Generate();
            Debug.Log("Product scaffold generated: " + ScenePath);
        }

        private static void Generate()
        {
            Directory.CreateDirectory(PrefabDirectory);
            Directory.CreateDirectory(SceneDirectory);
            AssetDatabase.Refresh();

            Font font = BuiltInFont();
            GameObject titlePrefab = CreateTitlePrefab(font);
            GameObject settingsPrefab = CreateSettingsPrefab(font);
            GameObject matchPrefab = CreateMatchPrefab(font);
            GameObject resultPrefab = CreateResultPrefab(font);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEventSystem();
            Canvas canvas = CreateCanvas();

            var title = Instantiate<TitleScreen>(titlePrefab, canvas.transform);
            var settings = Instantiate<GameSettingsScreen>(settingsPrefab, canvas.transform);
            var match = Instantiate<MatchScreen>(matchPrefab, canvas.transform);
            var result = Instantiate<ResultScreen>(resultPrefab, canvas.transform);

            var productRoot = new GameObject("ProductRoot");
            ScreenRouter router = productRoot.AddComponent<ScreenRouter>();
            ProductAppController controller = productRoot.AddComponent<ProductAppController>();
            router.Configure(new ProductScreen[] { title, settings, match, result });
            controller.Configure(router, title, settings, result);
            router.Show(ScreenId.Title);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            ValidateGeneratedAssets();
        }

        private static GameObject CreateTitlePrefab(Font font)
        {
            GameObject root = ScreenRoot<TitleScreen>("TitleScreen");
            CreateText(root.transform, "Title", "TRUMP GAME LAB", font, 64,
                new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.84f));
            CreateText(root.transform, "Subtitle", "Crazy Eights vertical slice", font, 28,
                new Vector2(0.2f, 0.58f), new Vector2(0.8f, 0.68f));
            Button play = CreateButton(root.transform, "PlayButton", "Play", font, new Vector2(0f, 10f));
            Button quit = CreateButton(root.transform, "QuitButton", "Quit", font, new Vector2(0f, -95f));
            root.GetComponent<TitleScreen>().Configure(play, quit);
            return SavePrefab(root, PrefabDirectory + "/TitleScreen.prefab");
        }

        private static GameObject CreateSettingsPrefab(Font font)
        {
            GameObject root = ScreenRoot<GameSettingsScreen>("GameSettingsScreen");
            CreateText(root.transform, "Title", "GAME SETTINGS", font, 52,
                new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.84f));
            Text summary = CreateText(root.transform, "Summary",
                "Crazy Eights\nHuman: Player 1  /  CPU: Player 2\nSeed: 1  /  Wild rank: 8",
                font, 28, new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.68f));
            Button start = CreateButton(root.transform, "StartButton", "Start", font, new Vector2(130f, -170f));
            Button back = CreateButton(root.transform, "BackButton", "Back", font, new Vector2(-130f, -170f));
            root.GetComponent<GameSettingsScreen>().Configure(summary, start, back);
            return SavePrefab(root, PrefabDirectory + "/GameSettingsScreen.prefab");
        }

        private static GameObject CreateMatchPrefab(Font font)
        {
            GameObject root = ScreenRoot<MatchScreen>("MatchScreen");
            Text status = CreateText(root.transform, "Status", "Player 1 turn", font, 34,
                new Vector2(0.22f, 0.88f), new Vector2(0.78f, 0.97f));
            Text opponent = CreateText(root.transform, "OpponentHand", "CPU hand: ■ ■ ■ ■ ■ ■ ■", font, 30,
                new Vector2(0.18f, 0.72f), new Vector2(0.82f, 0.84f));
            Text stock = CreateText(root.transform, "Stock", "Stock: 37", font, 30,
                new Vector2(0.2f, 0.48f), new Vector2(0.4f, 0.62f));
            Text discard = CreateText(root.transform, "Discard", "Discard: 7H", font, 30,
                new Vector2(0.6f, 0.48f), new Vector2(0.8f, 0.62f));
            Text hand = CreateText(root.transform, "HumanHand", "Your hand: AC 3D 4H 7S 8C 10D KH", font, 30,
                new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.36f));
            RectTransform actions = CreatePanel(root.transform, "ActionRoot",
                new Vector2(0.18f, 0.04f), new Vector2(0.82f, 0.18f));
            CreateText(actions, "Placeholder", "Legal action buttons appear here", font, 24,
                Vector2.zero, Vector2.one);
            root.GetComponent<MatchScreen>().Configure(status, opponent, stock, discard, hand, actions);
            return SavePrefab(root, PrefabDirectory + "/MatchScreen.prefab");
        }

        private static GameObject CreateResultPrefab(Font font)
        {
            GameObject root = ScreenRoot<ResultScreen>("ResultScreen");
            CreateText(root.transform, "Title", "RESULT", font, 56,
                new Vector2(0.2f, 0.7f), new Vector2(0.8f, 0.84f));
            Text summary = CreateText(root.transform, "Summary", "Player 1 wins\nReason: empty hand", font, 32,
                new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.68f));
            Button rematch = CreateButton(root.transform, "RematchButton", "Rematch", font, new Vector2(140f, -165f));
            Button title = CreateButton(root.transform, "TitleButton", "Title", font, new Vector2(-140f, -165f));
            root.GetComponent<ResultScreen>().Configure(summary, rematch, title);
            return SavePrefab(root, PrefabDirectory + "/ResultScreen.prefab");
        }

        private static GameObject ScreenRoot<T>(string name) where T : ProductScreen
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(T));
            RectTransform rect = root.GetComponent<RectTransform>();
            Stretch(rect);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.035f, 0.12f, 0.09f, 1f);
            background.raycastTarget = false;
            return root;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static T Instantiate<T>(GameObject prefab, Transform parent) where T : Component
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = prefab.name;
            return instance.GetComponent<T>();
        }

        private static Canvas CreateCanvas()
        {
            var root = new GameObject("ProductCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Text CreateText(Transform parent, string name, string value, Font font,
            int size, Vector2 anchorMin, Vector2 anchorMax)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = root.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.96f, 0.94f, 0.82f, 1f);
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Font font,
            Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 72f);
            rect.anchoredPosition = anchoredPosition;
            Image image = root.GetComponent<Image>();
            image.color = new Color(0.16f, 0.42f, 0.29f, 1f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            CreateText(root.transform, "Label", label, font, 28, Vector2.zero, Vector2.one);
            return button;
        }

        private static RectTransform CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font BuiltInFont()
        {
#if UNITY_6000_0_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Trump Game Lab";
            PlayerSettings.productName = "Trump Game Lab";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
        }

        private static void ValidateGeneratedAssets()
        {
            if (typeof(IGame).Assembly == typeof(ProductAppController).Assembly)
                throw new InvalidOperationException("Product assembly must reference, not duplicate, TrumpLab.Core.");

            var expectedPrefabs = new[]
            {
                ("TitleScreen.prefab", typeof(TitleScreen)),
                ("GameSettingsScreen.prefab", typeof(GameSettingsScreen)),
                ("MatchScreen.prefab", typeof(MatchScreen)),
                ("ResultScreen.prefab", typeof(ResultScreen))
            };
            foreach ((string fileName, Type screenType) in expectedPrefabs)
            {
                string path = PrefabDirectory + "/" + fileName;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent(screenType) == null)
                    throw new InvalidOperationException("Screen prefab is invalid: " + path);
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) != 0)
                    throw new InvalidOperationException("Screen prefab has a missing script: " + path);
            }

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            ProductAppController? controller = roots.Select(root =>
                root.GetComponent<ProductAppController>()).FirstOrDefault(component => component != null);
            Canvas? canvas = roots.Select(root => root.GetComponent<Canvas>())
                .FirstOrDefault(component => component != null);
            EventSystem? eventSystem = roots.Select(root => root.GetComponent<EventSystem>())
                .FirstOrDefault(component => component != null);
            if (controller == null || canvas == null || eventSystem == null)
                throw new InvalidOperationException("Bootstrap scene is missing a required root component.");
            if (controller.Router.Screens.Count != expectedPrefabs.Length ||
                controller.Router.Screens.Any(screen => screen == null))
                throw new InvalidOperationException("Bootstrap scene does not contain all product screens.");
            if (roots.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount) != 0)
                throw new InvalidOperationException("Bootstrap scene has a missing root script.");
            if (EditorBuildSettings.scenes.Length != 1 ||
                EditorBuildSettings.scenes[0].path != ScenePath ||
                !EditorBuildSettings.scenes[0].enabled)
                throw new InvalidOperationException("Bootstrap scene is not the only enabled build scene.");
        }
    }
}
