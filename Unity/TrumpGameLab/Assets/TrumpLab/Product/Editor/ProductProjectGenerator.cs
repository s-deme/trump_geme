#nullable enable

using System;
using System.Collections.Generic;
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
            GameObject libraryPrefab = CreateSessionLibraryPrefab(font);
            GameObject matchPrefab = CreateMatchPrefab(font);
            GameObject replayPrefab = CreateReplayPrefab(font);
            GameObject resultPrefab = CreateResultPrefab(font);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEventSystem();
            Canvas canvas = CreateCanvas();

            var title = Instantiate<TitleScreen>(titlePrefab, canvas.transform);
            var settings = Instantiate<GameSettingsScreen>(settingsPrefab, canvas.transform);
            var library = Instantiate<SessionLibraryScreen>(libraryPrefab, canvas.transform);
            var match = Instantiate<MatchScreen>(matchPrefab, canvas.transform);
            var replay = Instantiate<ReplayScreen>(replayPrefab, canvas.transform);
            var result = Instantiate<ResultScreen>(resultPrefab, canvas.transform);
            ProductErrorPanel errors = CreateErrorPanel(canvas.transform, font);

            var productRoot = new GameObject("ProductRoot");
            ScreenRouter router = productRoot.AddComponent<ScreenRouter>();
            ProductAppController controller = productRoot.AddComponent<ProductAppController>();
            router.Configure(new ProductScreen[] { title, settings, library, match, replay, result });
            controller.Configure(router, title, settings, library, match, replay, result, errors);
            RenderSampleMatch(match);
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
            Button play = CreateButton(root.transform, "PlayButton", "Play", font, new Vector2(0f, 35f));
            Button sessions = CreateButton(root.transform, "SessionsButton", "Saved sessions", font,
                new Vector2(0f, -65f));
            Button quit = CreateButton(root.transform, "QuitButton", "Quit", font, new Vector2(0f, -165f));
            root.GetComponent<TitleScreen>().Configure(play, sessions, quit);
            return SavePrefab(root, PrefabDirectory + "/TitleScreen.prefab");
        }

        private static GameObject CreateSettingsPrefab(Font font)
        {
            GameObject root = ScreenRoot<GameSettingsScreen>("GameSettingsScreen");
            CreateText(root.transform, "Title", "GAME SETTINGS", font, 52,
                new Vector2(0.2f, 0.78f), new Vector2(0.8f, 0.9f));
            Text summary = CreateText(root.transform, "Summary",
                "Crazy Eights  •  Human: Player 1  •  CPU: Player 2  •  Difficulty: Standard",
                font, 26, new Vector2(0.12f, 0.67f), new Vector2(0.88f, 0.76f));
            CreateText(root.transform, "SeedLabel", "Seed", font, 28,
                new Vector2(0.27f, 0.55f), new Vector2(0.46f, 0.62f));
            InputField seed = CreateInputField(root.transform, "SeedInput", "1", "Whole number", font,
                new Vector2(170f, 105f));
            CreateText(root.transform, "WildRankLabel", "Wild rank (1-13)", font, 28,
                new Vector2(0.23f, 0.44f), new Vector2(0.46f, 0.51f));
            InputField wildRank = CreateInputField(root.transform, "WildRankInput", "8", "1 to 13", font,
                new Vector2(170f, -15f));
            CreateText(root.transform, "DifficultyLabel", "CPU difficulty", font, 28,
                new Vector2(0.23f, 0.33f), new Vector2(0.46f, 0.4f));
            Dropdown difficulty = CreateDropdown(
                root.transform, "DifficultyDropdown", font, new Vector2(170f, -135f));
            difficulty.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 64f);
            Text validation = CreateText(root.transform, "Validation", "", font, 24,
                new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.25f));
            validation.color = new Color(1f, 0.55f, 0.45f, 1f);
            Button start = CreateButton(root.transform, "StartButton", "Start", font, new Vector2(130f, -300f));
            Button back = CreateButton(root.transform, "BackButton", "Back", font, new Vector2(-130f, -300f));
            root.GetComponent<GameSettingsScreen>().Configure(
                summary, seed, wildRank, difficulty, validation, start, back);
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
            RectTransform actionViewport = CreatePanel(root.transform, "ActionViewport",
                new Vector2(0.18f, 0.04f), new Vector2(0.82f, 0.18f));
            Image viewportImage = actionViewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            var mask = actionViewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var scroll = actionViewport.gameObject.AddComponent<ScrollRect>();
            RectTransform actions = CreatePanel(actionViewport, "ActionRoot", Vector2.zero, Vector2.one);
            actions.anchorMin = new Vector2(0f, 1f);
            actions.anchorMax = new Vector2(1f, 1f);
            actions.pivot = new Vector2(0.5f, 1f);
            actions.sizeDelta = Vector2.zero;
            GridLayoutGroup actionGrid = actions.gameObject.AddComponent<GridLayoutGroup>();
            actionGrid.cellSize = new Vector2(225f, 52f);
            actionGrid.spacing = new Vector2(10f, 8f);
            actionGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            actionGrid.constraintCount = 5;
            var fitter = actions.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = actions;
            scroll.viewport = actionViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            Text actionSummary = CreateText(root.transform, "ActionSummary", "Legal action buttons appear here", font, 22,
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.22f));
            Button actionTemplate = CreateButton(actions, "ActionButtonTemplate", "Action", font, Vector2.zero);
            actionTemplate.gameObject.SetActive(false);
            root.GetComponent<MatchScreen>().Configure(
                status, opponent, stock, discard, hand, actionSummary, actions, actionTemplate);
            return SavePrefab(root, PrefabDirectory + "/MatchScreen.prefab");
        }

        private static GameObject CreateSessionLibraryPrefab(Font font)
        {
            GameObject root = ScreenRoot<SessionLibraryScreen>("SessionLibraryScreen");
            CreateText(root.transform, "Title", "SAVED SESSIONS", font, 52,
                new Vector2(0.2f, 0.79f), new Vector2(0.8f, 0.91f));
            Dropdown dropdown = CreateDropdown(root.transform, "SlotDropdown", font,
                new Vector2(0f, 180f));
            Text detail = CreateText(root.transform, "Detail",
                "No saved sessions are available.", font, 25,
                new Vector2(0.16f, 0.43f), new Vector2(0.84f, 0.56f));
            Button resume = CreateButton(root.transform, "ResumeButton", "Resume", font,
                new Vector2(-270f, -30f));
            Button replay = CreateButton(root.transform, "ReplayButton", "Replay", font,
                new Vector2(0f, -30f));
            Button delete = CreateButton(root.transform, "DeleteButton", "Delete", font,
                new Vector2(270f, -30f));
            Button back = CreateButton(root.transform, "BackButton", "Back", font,
                new Vector2(0f, -160f));
            root.GetComponent<SessionLibraryScreen>().Configure(
                dropdown, detail, resume, replay, delete, back);
            return SavePrefab(root, PrefabDirectory + "/SessionLibraryScreen.prefab");
        }

        private static GameObject CreateReplayPrefab(Font font)
        {
            GameObject root = ScreenRoot<ReplayScreen>("ReplayScreen");
            CreateText(root.transform, "Title", "REPLAY", font, 52,
                new Vector2(0.2f, 0.8f), new Vector2(0.8f, 0.92f));
            Text status = CreateText(root.transform, "Status", "Replayed 0 actions", font, 30,
                new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.78f));
            Text table = CreateText(root.transform, "Table", "Saved table state", font, 28,
                new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.66f));
            Button back = CreateButton(root.transform, "BackButton", "Back to sessions", font,
                new Vector2(0f, -230f));
            back.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 72f);
            root.GetComponent<ReplayScreen>().Configure(status, table, back);
            return SavePrefab(root, PrefabDirectory + "/ReplayScreen.prefab");
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

        private static ProductErrorPanel CreateErrorPanel(Transform parent, Font font)
        {
            var root = new GameObject("ErrorPanel", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(ProductErrorPanel));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.08f, 0.025f, 0.025f, 0.96f);
            CreateText(root.transform, "Title", "MATCH STOPPED", font, 48,
                new Vector2(0.2f, 0.67f), new Vector2(0.8f, 0.8f));
            Text message = CreateText(root.transform, "Message", "The match stopped safely.", font, 28,
                new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.64f));
            Button dismiss = CreateButton(root.transform, "DismissButton", "Return to title", font,
                new Vector2(0f, -180f));
            dismiss.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 72f);
            ProductErrorPanel panel = root.GetComponent<ProductErrorPanel>();
            panel.Configure(message, dismiss);
            panel.Hide();
            return panel;
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

        private static InputField CreateInputField(Transform parent, string name, string value,
            string placeholderValue, Font font, Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 64f);
            rect.anchoredPosition = anchoredPosition;
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.94f, 0.93f, 0.86f, 1f);
            Text text = CreateText(root.transform, "Text", value, font, 28,
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.05f, 0.08f, 0.06f, 1f);
            Text placeholder = CreateText(root.transform, "Placeholder", placeholderValue, font, 28,
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.25f, 0.28f, 0.26f, 0.55f);
            InputField input = root.GetComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.text = value;
            return input;
        }

        private static Dropdown CreateDropdown(Transform parent, string name, Font font,
            Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Dropdown));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 72f);
            rect.anchoredPosition = anchoredPosition;
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.94f, 0.93f, 0.86f, 1f);
            Text caption = CreateText(root.transform, "Label", "No saved sessions", font, 26,
                new Vector2(0.04f, 0.08f), new Vector2(0.9f, 0.92f));
            caption.alignment = TextAnchor.MiddleLeft;
            caption.color = new Color(0.05f, 0.08f, 0.06f, 1f);
            Text arrow = CreateText(root.transform, "Arrow", "▼", font, 25,
                new Vector2(0.9f, 0.08f), new Vector2(0.98f, 0.92f));
            arrow.color = caption.color;

            var templateObject = new GameObject("Template", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            templateObject.transform.SetParent(root.transform, false);
            RectTransform template = templateObject.GetComponent<RectTransform>();
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -4f);
            template.sizeDelta = new Vector2(0f, 280f);
            templateObject.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.84f, 1f);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(template, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewportObject.GetComponent<Image>().color = Color.white;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var itemObject = new GameObject("Item", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Toggle), typeof(LayoutElement));
            itemObject.transform.SetParent(content, false);
            itemObject.GetComponent<LayoutElement>().preferredHeight = 60f;
            Image itemBackground = itemObject.GetComponent<Image>();
            itemBackground.color = new Color(0.18f, 0.38f, 0.27f, 1f);
            Toggle item = itemObject.GetComponent<Toggle>();
            item.targetGraphic = itemBackground;
            Text itemLabel = CreateText(itemObject.transform, "Item Label", "Session", font, 24,
                new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
            itemLabel.alignment = TextAnchor.MiddleLeft;

            ScrollRect scroll = templateObject.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            Dropdown dropdown = root.GetComponent<Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.options.Add(new Dropdown.OptionData("No saved sessions"));
            templateObject.SetActive(false);
            return dropdown;
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
                ("SessionLibraryScreen.prefab", typeof(SessionLibraryScreen)),
                ("MatchScreen.prefab", typeof(MatchScreen)),
                ("ReplayScreen.prefab", typeof(ReplayScreen)),
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
            if (controller.ErrorPanel == null || controller.ErrorPanel.gameObject.activeSelf)
                throw new InvalidOperationException("Bootstrap error panel is missing or initially visible.");
            if (controller.Router.Screens.Count != expectedPrefabs.Length ||
                controller.Router.Screens.Any(screen => screen == null))
                throw new InvalidOperationException("Bootstrap scene does not contain all product screens.");
            MatchScreen match = (MatchScreen)controller.Router.Get(ScreenId.Match);
            if (!match.HumanHandLabel.text.StartsWith("Your hand: ", StringComparison.Ordinal) ||
                !match.OpponentHandLabel.text.StartsWith("CPU hand: ", StringComparison.Ordinal) ||
                !match.StockLabel.text.StartsWith("Stock: ", StringComparison.Ordinal) ||
                !match.DiscardLabel.text.StartsWith("Discard: ", StringComparison.Ordinal))
                throw new InvalidOperationException("Match screen did not render structured sample state.");
            if (match.RenderedActionIds.Count == 0 ||
                match.RenderedActionIds.Distinct(StringComparer.Ordinal).Count() !=
                    match.RenderedActionIds.Count)
                throw new InvalidOperationException("Match screen did not preserve unique legal action IDs.");
            GameSettingsScreen settings = (GameSettingsScreen)controller.Router.Get(ScreenId.GameSettings);
            ValidateSettings(settings);
            ValidatePlayableSession();
            ValidateRepeatableRematch();
            if (roots.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount) != 0)
                throw new InvalidOperationException("Bootstrap scene has a missing root script.");
            if (EditorBuildSettings.scenes.Length != 1 ||
                EditorBuildSettings.scenes[0].path != ScenePath ||
                !EditorBuildSettings.scenes[0].enabled)
                throw new InvalidOperationException("Bootstrap scene is not the only enabled build scene.");
        }

        private static void RenderSampleMatch(MatchScreen match)
        {
            IGame game = BuiltInGames.Registry.Create(
                "crazy_eights",
                players: 2,
                seed: 1,
                options: new Dictionary<string, string> { ["wild_rank"] = "8" });
            if (!(game is IGamePresentationProvider provider))
                throw new InvalidOperationException("Crazy Eights does not provide structured presentation.");
            match.Render(CrazyEightsMatchPresenter.Create(
                provider.Present(viewer: 0), inputEnabled: true));
        }

        private static void ValidatePlayableSession()
        {
            var session = new GameSessionController(seed: 1);
            session.Begin();
            int humanActions = 0;
            int cpuActions = 0;
            for (int step = 0; step < 1000 && session.State != MatchSessionState.Finished; step++)
            {
                if (session.State == MatchSessionState.AwaitingHuman)
                {
                    string actionId = session.Snapshot.Actions[0].Id;
                    if (!session.TryApplyHumanAction(actionId))
                        throw new InvalidOperationException("A current human legal action was rejected.");
                    humanActions++;
                    if (session.TryApplyHumanAction(actionId))
                        throw new InvalidOperationException("A stale or double human input was accepted.");
                }
                else if (session.State == MatchSessionState.WaitingForCpu)
                {
                    if (!session.TryApplyCpuAction())
                        throw new InvalidOperationException("A CPU legal action was rejected.");
                    cpuActions++;
                }
                else
                {
                    throw new InvalidOperationException(
                        "Playable session entered an unexpected state: " + session.State);
                }
            }
            if (session.State != MatchSessionState.Finished || !session.Game.IsTerminal ||
                humanActions == 0 || cpuActions == 0)
                throw new InvalidOperationException(
                    "Crazy Eights did not complete with both human and CPU actions.");
        }

        private static void ValidateSettings(GameSettingsScreen settings)
        {
            if (GameSettingsScreen.TryCreateRequest(
                    "not-a-number", "8", CpuDifficulties.Standard, out _, out _))
                throw new InvalidOperationException("Settings accepted an invalid seed.");
            if (GameSettingsScreen.TryCreateRequest(
                    "42", "14", CpuDifficulties.Standard, out _, out _))
                throw new InvalidOperationException("Settings accepted an invalid wild rank.");
            if (GameSettingsScreen.TryCreateRequest("42", "8", 99, out _, out _))
                throw new InvalidOperationException("Settings accepted an invalid difficulty.");
            if (!GameSettingsScreen.TryCreateRequest(
                    "42", "8", CpuDifficulties.Hard,
                    out GameStartRequest? request, out _) || request == null ||
                request.Seed != 42 || request.WildRank != 8 ||
                request.Difficulty != CpuDifficulties.Hard)
                throw new InvalidOperationException("Settings did not create the requested game settings.");
            if (!settings.TryReadRequest(out GameStartRequest? defaults, out _) || defaults == null ||
                defaults.Seed != 1 || defaults.WildRank != 8 ||
                defaults.Difficulty != CpuDifficulties.Standard)
                throw new InvalidOperationException("Generated settings defaults are invalid.");
            if (!settings.DifficultyDropdown.options.Select(option => option.text).SequenceEqual(
                    new[] { "Easy", "Standard", "Hard" }))
                throw new InvalidOperationException("Generated difficulty order is invalid.");
        }

        private static void ValidateRepeatableRematch()
        {
            var first = new GameSessionController(seed: 23, wildRank: 8);
            var rematch = new GameSessionController(seed: 23, wildRank: 8);
            first.Begin();
            rematch.Begin();
            if (first.Snapshot.CurrentPlayer != rematch.Snapshot.CurrentPlayer ||
                !first.Snapshot.Actions.Select(action => action.Action)
                    .SequenceEqual(rematch.Snapshot.Actions.Select(action => action.Action)) ||
                !first.Snapshot.CardZones.SelectMany(zone => zone.Cards)
                    .SequenceEqual(rematch.Snapshot.CardZones.SelectMany(zone => zone.Cards)))
                throw new InvalidOperationException("Rematch did not reproduce the configured seed.");
        }
    }
}
