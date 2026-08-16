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
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrumpLab.Product.Editor
{
    public static class ProductProjectGenerator
    {
        private const string Root = "Assets/TrumpLab/Product";
        private const string PrefabDirectory = Root + "/Prefabs/Screens";
        private const string AudioDirectory = Root + "/Audio/Generated";
        private const string SceneDirectory = Root + "/Scenes";
        private const string ScenePath = SceneDirectory + "/Bootstrap.unity";
        private const int AudioSampleRate = 44100;

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
            Directory.CreateDirectory(AudioDirectory);
            Directory.CreateDirectory(SceneDirectory);
            AssetDatabase.Refresh();

            GeneratedAudioAssets audioAssets = GenerateAudioAssets();
            Font font = BuiltInFont();
            GameObject titlePrefab = CreateTitlePrefab(font);
            GameObject settingsPrefab = CreateSettingsPrefab(font);
            GameObject productSettingsPrefab = CreateProductSettingsPrefab(font);
            GameObject libraryPrefab = CreateSessionLibraryPrefab(font);
            GameObject matchPrefab = CreateMatchPrefab(font);
            GameObject replayPrefab = CreateReplayPrefab(font);
            GameObject resultPrefab = CreateResultPrefab(font);
            GameObject howToPlayPrefab = CreateHowToPlayPrefab(font);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            InputSystemUIInputModule inputModule = CreateEventSystem();
            Canvas canvas = CreateCanvas();
            ProductPresentationController presentation =
                canvas.gameObject.AddComponent<ProductPresentationController>();

            var title = Instantiate<TitleScreen>(titlePrefab, canvas.transform);
            var settings = Instantiate<GameSettingsScreen>(settingsPrefab, canvas.transform);
            var productSettings = Instantiate<ProductSettingsScreen>(
                productSettingsPrefab, canvas.transform);
            var library = Instantiate<SessionLibraryScreen>(libraryPrefab, canvas.transform);
            var match = Instantiate<MatchScreen>(matchPrefab, canvas.transform);
            var replay = Instantiate<ReplayScreen>(replayPrefab, canvas.transform);
            var result = Instantiate<ResultScreen>(resultPrefab, canvas.transform);
            var howToPlay = Instantiate<HowToPlayScreen>(howToPlayPrefab, canvas.transform);
            ProductPresentationSurfaces presentationSurfaces =
                CreatePresentationSurfaces(canvas.transform, font);
            ProductErrorPanel errors = CreateErrorPanel(canvas.transform, font);

            var productRoot = new GameObject("ProductRoot", typeof(AudioListener));
            ScreenRouter router = productRoot.AddComponent<ScreenRouter>();
            ProductInputController input = productRoot.AddComponent<ProductInputController>();
            ProductAudioController audio = CreateAudioController(productRoot, audioAssets);
            ProductAppController controller = productRoot.AddComponent<ProductAppController>();
            presentation.Configure(
                presentationSurfaces.Banner,
                presentationSurfaces.BannerImage,
                presentationSurfaces.BannerText,
                presentationSurfaces.Transition,
                audio);
            input.Configure(inputModule);
            router.Configure(new ProductScreen[]
                { title, settings, productSettings, library, match, replay, result, howToPlay });
            controller.Configure(
                router, title, settings, productSettings, library, match, replay, result,
                howToPlay, input, presentation, errors);
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
            Button tutorial = CreateButton(root.transform, "TutorialButton", "Tutorial", font,
                new Vector2(0f, 110f));
            Button play = CreateButton(root.transform, "PlayButton", "Play", font,
                new Vector2(0f, 20f));
            Button sessions = CreateButton(root.transform, "SessionsButton", "Saved sessions", font,
                new Vector2(0f, -70f));
            Button settings = CreateButton(root.transform, "SettingsButton", "Settings", font,
                new Vector2(0f, -160f));
            Button quit = CreateButton(root.transform, "QuitButton", "Quit", font,
                new Vector2(0f, -250f));
            root.GetComponent<TitleScreen>().Configure(
                tutorial, play, sessions, settings, quit);
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
            Button start = CreateButton(root.transform, "StartButton", "Start", font,
                new Vector2(270f, -300f));
            Button howToPlay = CreateButton(root.transform, "HowToPlayButton", "How to play", font,
                new Vector2(0f, -300f));
            Button back = CreateButton(root.transform, "BackButton", "Back", font,
                new Vector2(-270f, -300f));
            root.GetComponent<GameSettingsScreen>().Configure(
                summary, seed, wildRank, difficulty, validation, start, howToPlay, back);
            return SavePrefab(root, PrefabDirectory + "/GameSettingsScreen.prefab");
        }

        private static GameObject CreateProductSettingsPrefab(Font font)
        {
            GameObject root = ScreenRoot<ProductSettingsScreen>("ProductSettingsScreen");
            CreateText(root.transform, "Title", "PRODUCT SETTINGS", font, 52,
                new Vector2(0.2f, 0.84f), new Vector2(0.8f, 0.94f));
            Button generalPage = CreateButton(root.transform, "GeneralPageButton", "General", font,
                new Vector2(-145f, 360f));
            Button bindingsPage = CreateButton(root.transform, "BindingsPageButton", "Bindings", font,
                new Vector2(145f, 360f));

            RectTransform general = CreatePanel(root.transform, "GeneralPanel",
                Vector2.zero, Vector2.one);
            CreateText(general, "DisplayModeLabel", "Display mode", font, 25,
                new Vector2(0.18f, 0.67f), new Vector2(0.43f, 0.74f));
            Dropdown displayMode = CreateDropdown(
                general, "DisplayModeDropdown", font, new Vector2(260f, 230f));
            displayMode.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 64f);
            CreateText(general, "ResolutionLabel", "Resolution", font, 25,
                new Vector2(0.18f, 0.58f), new Vector2(0.43f, 0.65f));
            Dropdown resolution = CreateDropdown(
                general, "ResolutionDropdown", font, new Vector2(260f, 130f));
            resolution.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 64f);
            Toggle vSync = CreateToggle(general, "VSyncToggle", "VSync (60 Hz)", font,
                new Vector2(180f, 30f));
            CreateText(general, "PresentationSpeedLabel", "Presentation speed", font, 25,
                new Vector2(0.18f, 0.39f), new Vector2(0.43f, 0.46f));
            Dropdown presentationSpeed = CreateDropdown(
                general, "PresentationSpeedDropdown", font, new Vector2(260f, -70f));
            presentationSpeed.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 64f);
            Text masterLabel = CreateText(general, "MasterVolumeLabel", "Master 80%", font, 24,
                new Vector2(0.18f, 0.27f), new Vector2(0.4f, 0.34f));
            Slider master = CreateSlider(general, "MasterVolumeSlider", new Vector2(250f, -180f));
            Text musicLabel = CreateText(general, "MusicVolumeLabel", "Music 60%", font, 24,
                new Vector2(0.18f, 0.19f), new Vector2(0.4f, 0.26f));
            Slider music = CreateSlider(general, "MusicVolumeSlider", new Vector2(250f, -265f));
            Text sfxLabel = CreateText(general, "SfxVolumeLabel", "SFX 80%", font, 24,
                new Vector2(0.18f, 0.11f), new Vector2(0.4f, 0.18f));
            Slider sfx = CreateSlider(general, "SfxVolumeSlider", new Vector2(250f, -350f));

            RectTransform bindings = CreatePanel(root.transform, "BindingsPanel",
                Vector2.zero, Vector2.one);
            CreateText(bindings, "KeyboardHeader", "KEYBOARD", font, 25,
                new Vector2(0.2f, 0.7f), new Vector2(0.46f, 0.77f));
            CreateText(bindings, "GamepadHeader", "GAMEPAD", font, 25,
                new Vector2(0.54f, 0.7f), new Vector2(0.8f, 0.77f));
            string[] commands = { "Up", "Down", "Left", "Right", "Submit", "Back", "Help" };
            var keyboardButtons = new Button[commands.Length];
            var gamepadButtons = new Button[commands.Length];
            for (int index = 0; index < commands.Length; index++)
            {
                float y = 235f - index * 70f;
                keyboardButtons[index] = CreateButton(bindings,
                    "Keyboard" + commands[index] + "Button", commands[index], font,
                    new Vector2(-300f, y));
                gamepadButtons[index] = CreateButton(bindings,
                    "Gamepad" + commands[index] + "Button", commands[index], font,
                    new Vector2(300f, y));
                keyboardButtons[index].GetComponent<RectTransform>().sizeDelta =
                    new Vector2(520f, 58f);
                gamepadButtons[index].GetComponent<RectTransform>().sizeDelta =
                    new Vector2(520f, 58f);
                keyboardButtons[index].GetComponentInChildren<Text>(true).fontSize = 20;
                gamepadButtons[index].GetComponentInChildren<Text>(true).fontSize = 20;
            }
            Button cancelRebind = CreateButton(bindings, "CancelRebindButton", "Cancel rebind", font,
                new Vector2(0f, -285f));
            cancelRebind.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 60f);

            Text feedback = CreateText(root.transform, "Feedback", "", font, 22,
                new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.16f));
            Button back = CreateButton(root.transform, "BackButton", "Back", font,
                new Vector2(-290f, -460f));
            Button reset = CreateButton(root.transform, "ResetButton", "Reset defaults", font,
                new Vector2(0f, -460f));
            reset.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 72f);
            Button apply = CreateButton(root.transform, "ApplyButton", "Apply", font,
                new Vector2(290f, -460f));

            root.GetComponent<ProductSettingsScreen>().Configure(
                general.gameObject, bindings.gameObject, generalPage, bindingsPage,
                displayMode, resolution, vSync, master, music, sfx,
                masterLabel, musicLabel, sfxLabel, presentationSpeed,
                keyboardButtons, gamepadButtons, cancelRebind, feedback, apply, reset, back);
            bindings.gameObject.SetActive(false);
            cancelRebind.gameObject.SetActive(false);
            return SavePrefab(root, PrefabDirectory + "/ProductSettingsScreen.prefab");
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
                new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.37f));
            RectTransform actionViewport = CreatePanel(root.transform, "ActionViewport",
                new Vector2(0.18f, 0.04f), new Vector2(0.82f, 0.2f));
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
            actionGrid.cellSize = new Vector2(225f, 72f);
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
                new Vector2(0.18f, 0.2f), new Vector2(0.82f, 0.25f));
            Button actionTemplate = CreateButton(actions, "ActionButtonTemplate", "Action", font, Vector2.zero);
            actionTemplate.GetComponent<ProductUiFeedbackEmitter>()
                .SetSubmitFeedbackEnabled(false);
            actionTemplate.gameObject.SetActive(false);
            Button help = CreateButton(root.transform, "HelpButton", "Help", font,
                new Vector2(790f, 455f));
            help.GetComponent<RectTransform>().sizeDelta = new Vector2(170f, 56f);
            Button rules = CreateButton(root.transform, "RulesButton", "Rules", font,
                new Vector2(790f, 385f));
            rules.GetComponent<RectTransform>().sizeDelta = new Vector2(170f, 56f);
            RectTransform helpPanel = CreatePanel(root.transform, "ContextHelpPanel",
                new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.86f));
            Image helpBackground = helpPanel.gameObject.AddComponent<Image>();
            helpBackground.color = new Color(0.035f, 0.12f, 0.09f, 0.98f);
            Text helpText = CreateText(helpPanel, "ContextHelpText",
                "Current legal actions and their reasons appear here.", font, 24,
                new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.92f));
            helpText.alignment = TextAnchor.UpperLeft;
            helpText.verticalOverflow = VerticalWrapMode.Overflow;
            Button closeHelp = CreateButton(helpPanel, "CloseHelpButton", "Close", font,
                new Vector2(0f, -330f));
            RectTransform tutorialPanel = CreatePanel(root.transform, "TutorialPanel",
                new Vector2(0.12f, 0.55f), new Vector2(0.88f, 0.86f));
            Image tutorialBackground = tutorialPanel.gameObject.AddComponent<Image>();
            tutorialBackground.color = new Color(0.08f, 0.18f, 0.14f, 0.98f);
            Text tutorialProgress = CreateText(tutorialPanel, "TutorialProgress", "Step 1 / 6",
                font, 22, new Vector2(0.04f, 0.78f), new Vector2(0.2f, 0.96f));
            tutorialProgress.alignment = TextAnchor.MiddleLeft;
            Text tutorialHeading = CreateText(tutorialPanel, "TutorialHeading", "Meet the table",
                font, 30, new Vector2(0.2f, 0.72f), new Vector2(0.78f, 0.98f));
            Text tutorialInstruction = CreateText(tutorialPanel, "TutorialInstruction",
                "Find your hand, the CPU count, stock, discard, and turn label.", font, 22,
                new Vector2(0.04f, 0.43f), new Vector2(0.96f, 0.74f));
            tutorialInstruction.alignment = TextAnchor.UpperLeft;
            Text tutorialGuidance = CreateText(tutorialPanel, "TutorialGuidance",
                "This guide uses a normal Crazy Eights game with a fixed seed.", font, 20,
                new Vector2(0.04f, 0.08f), new Vector2(0.65f, 0.43f));
            tutorialGuidance.alignment = TextAnchor.UpperLeft;
            Button tutorialContinue = CreateButton(tutorialPanel, "TutorialContinueButton",
                "Start guided match", font, new Vector2(315f, -95f));
            tutorialContinue.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 64f);
            Button tutorialExit = CreateButton(tutorialPanel, "TutorialExitButton", "Exit tutorial",
                font, new Vector2(545f, -95f));
            tutorialExit.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 64f);
            root.GetComponent<MatchScreen>().Configure(
                status, opponent, stock, discard, hand, actionSummary, actions, actionTemplate,
                help, rules, helpPanel.gameObject, helpText, closeHelp,
                tutorialPanel.gameObject, tutorialProgress, tutorialHeading,
                tutorialInstruction, tutorialGuidance, tutorialContinue, tutorialExit);
            helpPanel.gameObject.SetActive(false);
            tutorialPanel.gameObject.SetActive(false);
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
            Button details = CreateButton(root.transform, "DetailsButton", "Result details", font,
                new Vector2(0f, -85f));
            details.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 72f);
            Button rematch = CreateButton(root.transform, "RematchButton", "Rematch", font,
                new Vector2(140f, -190f));
            Button title = CreateButton(root.transform, "TitleButton", "Title", font,
                new Vector2(-140f, -190f));
            root.GetComponent<ResultScreen>().Configure(summary, details, rematch, title);
            return SavePrefab(root, PrefabDirectory + "/ResultScreen.prefab");
        }

        private static GameObject CreateHowToPlayPrefab(Font font)
        {
            GameObject root = ScreenRoot<HowToPlayScreen>("HowToPlayScreen");
            CreateText(root.transform, "ScreenTitle", "HOW TO PLAY", font, 48,
                new Vector2(0.18f, 0.84f), new Vector2(0.82f, 0.94f));
            Text context = CreateText(root.transform, "Context",
                "Crazy Eights rules · Read-only guide", font, 23,
                new Vector2(0.16f, 0.76f), new Vector2(0.84f, 0.83f));
            Text indicator = CreateText(root.transform, "PageIndicator", "Page 1 / 5", font, 24,
                new Vector2(0.17f, 0.68f), new Vector2(0.33f, 0.75f));
            Text pageTitle = CreateText(root.transform, "PageTitle", "Objective", font, 38,
                new Vector2(0.25f, 0.58f), new Vector2(0.75f, 0.68f));
            Text body = CreateText(root.transform, "PageBody",
                "Be the first player to empty your hand.", font, 28,
                new Vector2(0.16f, 0.27f), new Vector2(0.84f, 0.57f));
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            Button startTutorial = CreateButton(root.transform, "StartTutorialButton",
                "Start tutorial", font, new Vector2(0f, -245f));
            startTutorial.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 72f);
            Button previous = CreateButton(root.transform, "PreviousButton", "Previous", font,
                new Vector2(-300f, -350f));
            Button next = CreateButton(root.transform, "NextButton", "Next", font,
                new Vector2(0f, -350f));
            Button back = CreateButton(root.transform, "BackButton", "Back", font,
                new Vector2(300f, -350f));
            root.GetComponent<HowToPlayScreen>().Configure(
                indicator, pageTitle, body, context, startTutorial, previous, next, back);
            root.GetComponent<HowToPlayScreen>().Render(CrazyEightsHowToPlayPresenter.Create());
            return SavePrefab(root, PrefabDirectory + "/HowToPlayScreen.prefab");
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
            Navigation navigation = dismiss.navigation;
            navigation.mode = Navigation.Mode.None;
            dismiss.navigation = navigation;
            ProductErrorPanel panel = root.GetComponent<ProductErrorPanel>();
            panel.Configure(message, dismiss);
            panel.Hide();
            return panel;
        }

        private static ProductPresentationSurfaces CreatePresentationSurfaces(
            Transform parent, Font font)
        {
            var transitionObject = new GameObject("ScreenTransition",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(CanvasGroup));
            transitionObject.transform.SetParent(parent, false);
            Stretch(transitionObject.GetComponent<RectTransform>());
            Image transitionImage = transitionObject.GetComponent<Image>();
            transitionImage.color = new Color(0.015f, 0.03f, 0.025f, 0.82f);
            transitionImage.raycastTarget = false;
            CanvasGroup transition = transitionObject.GetComponent<CanvasGroup>();
            transition.alpha = 0f;
            transition.interactable = false;
            transition.blocksRaycasts = false;

            var bannerObject = new GameObject("FeedbackBanner",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(CanvasGroup));
            bannerObject.transform.SetParent(parent, false);
            RectTransform bannerRect = bannerObject.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.3f, 0.84f);
            bannerRect.anchorMax = new Vector2(0.7f, 0.94f);
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;
            Image bannerImage = bannerObject.GetComponent<Image>();
            bannerImage.color = new Color(0.1f, 0.4f, 0.58f, 0.97f);
            bannerImage.raycastTarget = false;
            CanvasGroup banner = bannerObject.GetComponent<CanvasGroup>();
            banner.alpha = 0f;
            banner.interactable = false;
            banner.blocksRaycasts = false;
            Text bannerText = CreateText(bannerObject.transform, "FeedbackText",
                "◇  Focus moved", font, 30,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));

            return new ProductPresentationSurfaces(
                banner, bannerImage, bannerText, transition);
        }

        private static ProductAudioController CreateAudioController(
            GameObject productRoot, GeneratedAudioAssets assets)
        {
            if (productRoot == null) throw new ArgumentNullException(nameof(productRoot));
            if (assets == null) throw new ArgumentNullException(nameof(assets));

            var musicObject = new GameObject("MusicAudio", typeof(AudioSource));
            musicObject.transform.SetParent(productRoot.transform, false);
            var sfxObject = new GameObject("SfxAudio", typeof(AudioSource));
            sfxObject.transform.SetParent(productRoot.transform, false);
            AudioSource music = musicObject.GetComponent<AudioSource>();
            AudioSource sfx = sfxObject.GetComponent<AudioSource>();
            ProductAudioClipBinding[] cues = Enum.GetValues(typeof(ProductFeedbackKind))
                .Cast<ProductFeedbackKind>()
                .Select(kind => new ProductAudioClipBinding(kind,
                    assets.Cues.TryGetValue(kind, out AudioClip? clip)
                        ? clip
                        : throw new InvalidOperationException(
                            "Generated audio cue is missing: " + kind)))
                .ToArray();

            ProductAudioController controller =
                productRoot.AddComponent<ProductAudioController>();
            controller.Configure(music, sfx, assets.Music, cues);
            return controller;
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

        private static InputSystemUIInputModule CreateEventSystem()
        {
            var root = new GameObject("EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            InputSystemUIInputModule module = root.GetComponent<InputSystemUIInputModule>();
            module.UnassignActions();
            return module;
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
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(ProductUiFeedbackEmitter), typeof(Button));
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

        private static Toggle CreateToggle(Transform parent, string name, string label, Font font,
            Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform),
                typeof(ProductUiFeedbackEmitter), typeof(Toggle));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 64f);
            rect.anchoredPosition = anchoredPosition;

            var backgroundObject = new GameObject("Background", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(root.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(52f, 52f);
            Image background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.94f, 0.93f, 0.86f, 1f);

            var checkmarkObject = new GameObject("Checkmark", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.18f, 0.18f);
            checkmarkRect.anchorMax = new Vector2(0.82f, 0.82f);
            checkmarkRect.offsetMin = checkmarkRect.offsetMax = Vector2.zero;
            Image checkmark = checkmarkObject.GetComponent<Image>();
            checkmark.color = new Color(0.12f, 0.46f, 0.25f, 1f);

            Text text = CreateText(root.transform, "Label", label, font, 25,
                new Vector2(0.16f, 0f), Vector2.one);
            text.alignment = TextAnchor.MiddleLeft;
            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = true;
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, string name,
            Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform),
                typeof(ProductUiFeedbackEmitter), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(440f, 52f);
            rect.anchoredPosition = anchoredPosition;

            var backgroundObject = new GameObject("Background", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(root.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.38f);
            backgroundRect.anchorMax = new Vector2(1f, 0.62f);
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            Image background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.22f, 0.3f, 0.25f, 1f);

            RectTransform fillArea = CreatePanel(root.transform, "Fill Area",
                new Vector2(0f, 0.28f), new Vector2(1f, 0.72f));
            fillArea.offsetMin = new Vector2(8f, 0f);
            fillArea.offsetMax = new Vector2(-8f, 0f);
            var fillObject = new GameObject("Fill", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(fillArea, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            Stretch(fillRect);
            Image fill = fillObject.GetComponent<Image>();
            fill.color = new Color(0.2f, 0.68f, 0.38f, 1f);

            RectTransform handleArea = CreatePanel(root.transform, "Handle Slide Area",
                Vector2.zero, Vector2.one);
            handleArea.offsetMin = new Vector2(16f, 0f);
            handleArea.offsetMax = new Vector2(-16f, 0f);
            var handleObject = new GameObject("Handle", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            handleObject.transform.SetParent(handleArea, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(38f, 52f);
            Image handle = handleObject.GetComponent<Image>();
            handle.color = new Color(0.94f, 0.93f, 0.86f, 1f);

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = 80f;
            return slider;
        }

        private static InputField CreateInputField(Transform parent, string name, string value,
            string placeholderValue, Font font, Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(ProductUiFeedbackEmitter), typeof(InputField));
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
                typeof(Image), typeof(ProductUiFeedbackEmitter), typeof(Dropdown));
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
                typeof(Image), typeof(ProductUiFeedbackEmitter), typeof(Toggle),
                typeof(LayoutElement));
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

        private static GeneratedAudioAssets GenerateAudioAssets()
        {
            string musicPath = AudioDirectory + "/music-loop.wav";
            WriteWaveAsset(musicPath, RenderMusicLoop());

            var definitions = new[]
            {
                new CueWave(ProductFeedbackKind.Navigation, "navigation.wav",
                    new[] { 880d }, 0.045d),
                new CueWave(ProductFeedbackKind.Submit, "submit.wav",
                    new[] { 660d, 880d }, 0.055d),
                new CueWave(ProductFeedbackKind.Reject, "reject.wav",
                    new[] { 180d, 145d }, 0.080d),
                new CueWave(ProductFeedbackKind.CardPlay, "card-play.wav",
                    new[] { 1040d, 720d }, 0.040d),
                new CueWave(ProductFeedbackKind.Draw, "draw.wav",
                    new[] { 430d, 315d }, 0.070d),
                new CueWave(ProductFeedbackKind.WildSuit, "wild-suit.wav",
                    new[] { 523.25d, 659.25d, 783.99d }, 0.065d),
                new CueWave(ProductFeedbackKind.CpuTurn, "cpu-turn.wav",
                    new[] { 220d, 330d }, 0.090d),
                new CueWave(ProductFeedbackKind.Win, "win.wav",
                    new[] { 523.25d, 659.25d, 783.99d, 1046.5d }, 0.080d),
                new CueWave(ProductFeedbackKind.Lose, "lose.wav",
                    new[] { 440d, 349.23d, 261.63d, 196d }, 0.090d),
                new CueWave(ProductFeedbackKind.Error, "error.wav",
                    new[] { 130d, 105d, 130d }, 0.110d)
            };

            var clips = new Dictionary<ProductFeedbackKind, AudioClip>();
            foreach (CueWave definition in definitions)
            {
                string path = AudioDirectory + "/" + definition.FileName;
                WriteWaveAsset(path, RenderToneSequence(
                    definition.Frequencies, definition.NoteSeconds));
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                    throw new InvalidOperationException("Generated audio clip is missing: " + path);
                clips.Add(definition.Kind, clip);
            }

            AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
            if (music == null)
                throw new InvalidOperationException("Generated music loop is missing: " + musicPath);
            return new GeneratedAudioAssets(music, clips);
        }

        private static float[] RenderMusicLoop()
        {
            const int seconds = 4;
            int sampleCount = AudioSampleRate * seconds;
            var samples = new float[sampleCount];
            double[][] chords =
            {
                new[] { 220d, 277.18d, 329.63d },
                new[] { 196d, 246.94d, 293.66d },
                new[] { 174.61d, 220d, 261.63d },
                new[] { 196d, 246.94d, 329.63d }
            };
            for (int index = 0; index < sampleCount; index++)
            {
                double time = (double)index / AudioSampleRate;
                int segment = Math.Min(seconds - 1, (int)time);
                double local = time - segment;
                double edge = Math.Min(1d, Math.Min(local / 0.025d,
                    (1d - local) / 0.025d));
                double value = 0d;
                foreach (double frequency in chords[segment])
                    value += Math.Sin(2d * Math.PI * frequency * local);
                samples[index] = (float)(value / chords[segment].Length * 0.16d * edge);
            }
            return samples;
        }

        private static float[] RenderToneSequence(IReadOnlyList<double> frequencies,
            double noteSeconds)
        {
            if (frequencies == null || frequencies.Count == 0)
                throw new ArgumentException("A generated cue requires at least one tone.",
                    nameof(frequencies));
            int noteSamples = Math.Max(1, (int)Math.Round(noteSeconds * AudioSampleRate));
            int gapSamples = (int)Math.Round(0.008d * AudioSampleRate);
            int sampleCount = frequencies.Count * noteSamples +
                Math.Max(0, frequencies.Count - 1) * gapSamples;
            var samples = new float[sampleCount];
            int output = 0;
            for (int toneIndex = 0; toneIndex < frequencies.Count; toneIndex++)
            {
                double frequency = frequencies[toneIndex];
                for (int index = 0; index < noteSamples; index++)
                {
                    double position = noteSamples == 1 ? 1d :
                        (double)index / (noteSamples - 1);
                    double envelope = Math.Min(1d, position / 0.12d) *
                        Math.Min(1d, (1d - position) / 0.28d);
                    double time = (double)index / AudioSampleRate;
                    double fundamental = Math.Sin(2d * Math.PI * frequency * time);
                    double harmonic = Math.Sin(4d * Math.PI * frequency * time) * 0.18d;
                    samples[output++] = (float)((fundamental + harmonic) *
                        0.34d * envelope);
                }
                if (toneIndex < frequencies.Count - 1) output += gapSamples;
            }
            return samples;
        }

        private static void WriteWaveAsset(string path, float[] samples)
        {
            byte[] bytes = EncodePcm16Wave(samples);
            bool unchanged = File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes);
            if (!unchanged) File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Generated WAV has no AudioImporter: " + path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        private static byte[] EncodePcm16Wave(IReadOnlyList<float> samples)
        {
            if (samples == null || samples.Count == 0)
                throw new ArgumentException("Generated WAV samples cannot be empty.",
                    nameof(samples));
            using var stream = new MemoryStream(44 + samples.Count * sizeof(short));
            using var writer = new BinaryWriter(stream);
            WriteAscii(writer, "RIFF");
            writer.Write(36 + samples.Count * sizeof(short));
            WriteAscii(writer, "WAVE");
            WriteAscii(writer, "fmt ");
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(AudioSampleRate);
            writer.Write(AudioSampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            WriteAscii(writer, "data");
            writer.Write(samples.Count * sizeof(short));
            foreach (float sample in samples)
            {
                float clamped = Mathf.Clamp(sample, -1f, 1f);
                writer.Write((short)Math.Round(clamped * short.MaxValue,
                    MidpointRounding.AwayFromZero));
            }
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteAscii(BinaryWriter writer, string value)
        {
            foreach (char character in value) writer.Write((byte)character);
        }

        private sealed class GeneratedAudioAssets
        {
            public GeneratedAudioAssets(AudioClip music,
                IReadOnlyDictionary<ProductFeedbackKind, AudioClip> cues)
            {
                Music = music;
                Cues = cues;
            }

            public AudioClip Music { get; }
            public IReadOnlyDictionary<ProductFeedbackKind, AudioClip> Cues { get; }
        }

        private sealed class ProductPresentationSurfaces
        {
            public ProductPresentationSurfaces(CanvasGroup banner, Image bannerImage,
                Text bannerText, CanvasGroup transition)
            {
                Banner = banner ?? throw new ArgumentNullException(nameof(banner));
                BannerImage = bannerImage ??
                    throw new ArgumentNullException(nameof(bannerImage));
                BannerText = bannerText ??
                    throw new ArgumentNullException(nameof(bannerText));
                Transition = transition ??
                    throw new ArgumentNullException(nameof(transition));
            }

            public CanvasGroup Banner { get; }
            public Image BannerImage { get; }
            public Text BannerText { get; }
            public CanvasGroup Transition { get; }
        }

        private readonly struct CueWave
        {
            public CueWave(ProductFeedbackKind kind, string fileName,
                IReadOnlyList<double> frequencies, double noteSeconds)
            {
                Kind = kind;
                FileName = fileName;
                Frequencies = frequencies;
                NoteSeconds = noteSeconds;
            }

            public ProductFeedbackKind Kind { get; }
            public string FileName { get; }
            public IReadOnlyList<double> Frequencies { get; }
            public double NoteSeconds { get; }
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Trump Game Lab";
            PlayerSettings.productName = "Trump Game Lab";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            QualitySettings.vSyncCount = 1;
        }

        private static void ValidateGeneratedAssets()
        {
            if (typeof(IGame).Assembly == typeof(ProductAppController).Assembly)
                throw new InvalidOperationException("Product assembly must reference, not duplicate, TrumpLab.Core.");

            var expectedPrefabs = new[]
            {
                ("TitleScreen.prefab", typeof(TitleScreen)),
                ("GameSettingsScreen.prefab", typeof(GameSettingsScreen)),
                ("ProductSettingsScreen.prefab", typeof(ProductSettingsScreen)),
                ("SessionLibraryScreen.prefab", typeof(SessionLibraryScreen)),
                ("MatchScreen.prefab", typeof(MatchScreen)),
                ("ReplayScreen.prefab", typeof(ReplayScreen)),
                ("ResultScreen.prefab", typeof(ResultScreen)),
                ("HowToPlayScreen.prefab", typeof(HowToPlayScreen))
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
            ProductInputController? input = roots.Select(root =>
                root.GetComponent<ProductInputController>()).FirstOrDefault(component => component != null);
            if (controller == null || canvas == null || eventSystem == null || input == null)
                throw new InvalidOperationException("Bootstrap scene is missing a required root component.");
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null ||
                eventSystem.GetComponent<StandaloneInputModule>() != null)
                throw new InvalidOperationException(
                    "Bootstrap scene must use only the Input System UI module.");
            ValidateGeneratedAudioContract(roots, controller);
            ValidatePresentationContract(roots, controller, canvas);
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

        private static void ValidateGeneratedAudioContract(GameObject[] roots,
            ProductAppController controller)
        {
            AudioListener[] listeners = roots.SelectMany(root =>
                root.GetComponentsInChildren<AudioListener>(true)).ToArray();
            AudioSource[] sources = roots.SelectMany(root =>
                root.GetComponentsInChildren<AudioSource>(true)).ToArray();
            ProductAudioController[] audioControllers = roots.SelectMany(root =>
                root.GetComponentsInChildren<ProductAudioController>(true)).ToArray();
            if (listeners.Length != 1 || listeners[0].gameObject != controller.gameObject ||
                !listeners[0].enabled)
                throw new InvalidOperationException(
                    "Bootstrap scene requires exactly one enabled ProductRoot AudioListener.");
            if (sources.Length != 2 || audioControllers.Length != 1 ||
                audioControllers[0].gameObject != controller.gameObject)
                throw new InvalidOperationException(
                    "Bootstrap scene requires one product audio controller and two audio sources.");

            ProductAudioController audio = audioControllers[0];
            audio.Initialize();
            if (!audio.IsInitialized || audio.MusicSource == audio.SfxSource ||
                !sources.Contains(audio.MusicSource) || !sources.Contains(audio.SfxSource))
                throw new InvalidOperationException(
                    "Product audio sources are not wired to separate music and SFX channels.");
            if (audio.MusicSource.gameObject.name != "MusicAudio" ||
                audio.SfxSource.gameObject.name != "SfxAudio" ||
                audio.MusicSource.transform.parent != controller.transform ||
                audio.SfxSource.transform.parent != controller.transform)
                throw new InvalidOperationException(
                    "Product audio channels are not owned by ProductRoot.");
            if (!audio.MusicSource.loop || audio.MusicSource.playOnAwake ||
                audio.MusicSource.spatialBlend != 0f ||
                audio.MusicSource.clip != audio.MusicLoop ||
                audio.SfxSource.loop || audio.SfxSource.playOnAwake ||
                audio.SfxSource.spatialBlend != 0f || audio.SfxSource.clip != null)
                throw new InvalidOperationException(
                    "Product music and SFX AudioSource settings are invalid.");
            foreach (ProductFeedbackKind kind in Enum.GetValues(typeof(ProductFeedbackKind)))
                audio.Play(kind);

            string[] expectedFiles =
            {
                "card-play.wav", "cpu-turn.wav", "draw.wav", "error.wav", "lose.wav",
                "music-loop.wav", "navigation.wav", "reject.wav", "submit.wav",
                "wild-suit.wav", "win.wav"
            };
            string[] paths = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (!paths.Select(Path.GetFileName).SequenceEqual(expectedFiles))
                throw new InvalidOperationException(
                    "Generated audio asset set does not match the product cue contract.");
            foreach (string path in paths)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                AudioImporterSampleSettings settings = importer == null
                    ? default
                    : importer.defaultSampleSettings;
                if (clip == null || clip.channels != 1 || clip.frequency != AudioSampleRate ||
                    importer == null || !importer.forceToMono || importer.loadInBackground ||
                    !settings.preloadAudioData)
                    throw new InvalidOperationException(
                        "Generated audio format is invalid: " + path);
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                    settings.compressionFormat != AudioCompressionFormat.PCM ||
                    settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate)
                    throw new InvalidOperationException(
                        "Generated audio import settings are invalid: " + path);
            }
        }

        private static void ValidatePresentationContract(GameObject[] roots,
            ProductAppController controller, Canvas canvas)
        {
            ProductPresentationController[] presentations = roots.SelectMany(root =>
                root.GetComponentsInChildren<ProductPresentationController>(true)).ToArray();
            if (presentations.Length != 1 || presentations[0].gameObject != canvas.gameObject ||
                controller.PresentationController != presentations[0])
                throw new InvalidOperationException(
                    "Product presentation must be the single feedback sink on ProductCanvas.");

            ProductPresentationController presentation = presentations[0];
            if (presentation.Banner.transform.parent != canvas.transform ||
                presentation.Transition.transform.parent != canvas.transform ||
                presentation.Banner.alpha != 0f || presentation.Banner.interactable ||
                presentation.Banner.blocksRaycasts || presentation.Transition.alpha != 0f ||
                presentation.Transition.interactable || presentation.Transition.blocksRaycasts)
                throw new InvalidOperationException(
                    "Product presentation surfaces are not initialized as non-interactive overlays.");
            Graphic[] presentationGraphics = presentation.Banner
                .GetComponentsInChildren<Graphic>(true)
                .Concat(presentation.Transition.GetComponentsInChildren<Graphic>(true))
                .ToArray();
            if (presentationGraphics.Length < 3 ||
                presentationGraphics.Any(graphic => graphic.raycastTarget))
                throw new InvalidOperationException(
                    "Product presentation surfaces must never be UI raycast targets.");

            Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
            ProductUiFeedbackEmitter[] emitters =
                canvas.GetComponentsInChildren<ProductUiFeedbackEmitter>(true);
            if (selectables.Length == 0 || emitters.Length != selectables.Length ||
                selectables.Any(selectable =>
                    selectable.GetComponent<ProductUiFeedbackEmitter>() == null ||
                    !FeedbackPrecedesSelectable(selectable)) ||
                emitters.Any(emitter => emitter.GetComponent<Selectable>() == null ||
                    emitter.GetComponentInParent<ProductPresentationController>() != presentation))
                throw new InvalidOperationException(
                    "Every generated Selectable requires one canvas-routed feedback emitter.");
        }

        private static bool FeedbackPrecedesSelectable(Selectable selectable)
        {
            Component[] components = selectable.GetComponents<Component>();
            int selectableIndex = Array.IndexOf(components, selectable);
            int feedbackIndex = Array.FindIndex(components,
                component => component is ProductUiFeedbackEmitter);
            return feedbackIndex >= 0 && selectableIndex > feedbackIndex;
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
