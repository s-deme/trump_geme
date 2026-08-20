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
using UnityEngine.Rendering;
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
            ProductSafeFrame safeFrame = CreateSafeFrame(canvas);
            ProductPresentationController presentation =
                canvas.gameObject.AddComponent<ProductPresentationController>();

            var title = Instantiate<TitleScreen>(titlePrefab, safeFrame.transform);
            var settings = Instantiate<GameSettingsScreen>(settingsPrefab, safeFrame.transform);
            var productSettings = Instantiate<ProductSettingsScreen>(
                productSettingsPrefab, safeFrame.transform);
            var library = Instantiate<SessionLibraryScreen>(libraryPrefab, safeFrame.transform);
            var match = Instantiate<MatchScreen>(matchPrefab, safeFrame.transform);
            var replay = Instantiate<ReplayScreen>(replayPrefab, safeFrame.transform);
            var result = Instantiate<ResultScreen>(resultPrefab, safeFrame.transform);
            var howToPlay = Instantiate<HowToPlayScreen>(howToPlayPrefab, safeFrame.transform);
            ProductPresentationSurfaces presentationSurfaces =
                CreatePresentationSurfaces(safeFrame.transform, font);
            ProductErrorPanel errors = CreateErrorPanel(safeFrame.transform, font);

            var productRoot = new GameObject("ProductRoot", typeof(AudioListener));
            ScreenRouter router = productRoot.AddComponent<ScreenRouter>();
            ProductInputController input = productRoot.AddComponent<ProductInputController>();
            ProductAudioController audio = CreateAudioController(productRoot, audioAssets);
            ProductLocalizationController localization =
                productRoot.AddComponent<ProductLocalizationController>();
            ProductAccessibilityController accessibility =
                productRoot.AddComponent<ProductAccessibilityController>();
            ProductAppController controller = productRoot.AddComponent<ProductAppController>();
            presentation.Configure(
                presentationSurfaces.Banner,
                presentationSurfaces.BannerImage,
                presentationSurfaces.BannerText,
                presentationSurfaces.Transition,
                audio);
            input.Configure(inputModule);
            localization.Configure(canvas.transform, font);
            accessibility.Configure((RectTransform)canvas.transform, safeFrame, localization);
            router.Configure(new ProductScreen[]
                { title, settings, productSettings, library, match, replay, result, howToPlay });
            controller.Configure(
                router, title, settings, productSettings, library, match, replay, result,
                howToPlay, input, presentation, errors, localization, accessibility);
            RenderSampleMatch(match);
            router.Show(ScreenId.Title);
            ProductSettings defaultSettings = ProductSettings.CreateDefaults(
                ProductTextCatalog.EnglishLocale);
            accessibility.Apply(defaultSettings);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            ValidateGeneratedAssets();
        }

        private static GameObject CreateTitlePrefab(Font font)
        {
            GameObject root = ScreenRoot<TitleScreen>("TitleScreen");
            CreateText(root.transform, "Title", "app.title", font, 64,
                new Vector2(0.15f, 0.75f), new Vector2(0.85f, 0.88f));
            CreateText(root.transform, "Subtitle", "app.subtitle", font, 28,
                new Vector2(0.2f, 0.67f), new Vector2(0.8f, 0.74f));
            Button tutorial = CreateButton(root.transform, "TutorialButton", "title.tutorial",
                font, new Vector2(0f, 110f), ProductTextContentMode.Dynamic);
            tutorial.GetComponent<RectTransform>().sizeDelta = new Vector2(380f, 72f);
            Button play = CreateButton(root.transform, "PlayButton", "title.play", font,
                new Vector2(0f, 20f));
            Button sessions = CreateButton(root.transform, "SessionsButton",
                "title.saved_sessions", font, new Vector2(0f, -70f));
            sessions.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 72f);
            Button settings = CreateButton(root.transform, "SettingsButton", "title.settings",
                font, new Vector2(0f, -160f));
            Button quit = CreateButton(root.transform, "QuitButton", "title.quit", font,
                new Vector2(0f, -250f));
            root.GetComponent<TitleScreen>().Configure(
                tutorial, play, sessions, settings, quit);
            return SavePrefab(root, PrefabDirectory + "/TitleScreen.prefab");
        }

        private static GameObject CreateSettingsPrefab(Font font)
        {
            GameObject root = ScreenRoot<GameSettingsScreen>("GameSettingsScreen");
            CreateText(root.transform, "Title", "game_settings.title", font, 52,
                new Vector2(0.2f, 0.78f), new Vector2(0.8f, 0.9f));
            Text summary = CreateText(root.transform, "Summary",
                "game_settings.summary", font, 26,
                new Vector2(0.12f, 0.665f), new Vector2(0.88f, 0.765f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("game_settings.summary",
                    ProductTextCatalog.English.Get("game_settings.difficulty_standard")));
            CreateText(root.transform, "SeedLabel", "game_settings.seed", font, 28,
                new Vector2(0.27f, 0.55f), new Vector2(0.46f, 0.62f));
            InputField seed = CreateInputField(root.transform, "SeedInput", "1",
                "game_settings.seed_placeholder", font,
                new Vector2(260f, 105f), "game_settings.seed");
            CreateText(root.transform, "WildRankLabel", "game_settings.wild_rank", font, 28,
                new Vector2(0.08f, 0.44f), new Vector2(0.46f, 0.51f));
            InputField wildRank = CreateInputField(root.transform, "WildRankInput", "8",
                "game_settings.wild_rank_placeholder", font,
                new Vector2(260f, -15f), "game_settings.wild_rank");
            CreateText(root.transform, "DifficultyLabel", "game_settings.difficulty", font, 28,
                new Vector2(0.23f, 0.33f), new Vector2(0.46f, 0.4f));
            Dropdown difficulty = CreateDropdown(
                root.transform, "DifficultyDropdown", font, new Vector2(170f, -135f),
                "game_settings.difficulty", "game_settings.difficulty_standard");
            difficulty.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 64f);
            Text validation = CreateText(root.transform, "Validation",
                "game_settings.validation", font, 24,
                new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.25f),
                ProductTextContentMode.Dynamic, string.Empty,
                ProductGraphicRole.ErrorText);
            Button start = CreateButton(root.transform, "StartButton", "game_settings.start", font,
                new Vector2(270f, -300f));
            Button howToPlay = CreateButton(root.transform, "HowToPlayButton",
                "game_settings.how_to_play", font, new Vector2(0f, -300f));
            Button back = CreateButton(root.transform, "BackButton", "common.back", font,
                new Vector2(-270f, -300f));
            root.GetComponent<GameSettingsScreen>().Configure(
                summary, seed, wildRank, difficulty, validation, start, howToPlay, back);
            return SavePrefab(root, PrefabDirectory + "/GameSettingsScreen.prefab");
        }

        private static GameObject CreateProductSettingsPrefab(Font font)
        {
            GameObject root = ScreenRoot<ProductSettingsScreen>("ProductSettingsScreen");
            CreateText(root.transform, "Title", "settings.title", font, 52,
                new Vector2(0.2f, 0.9f), new Vector2(0.8f, 1f));
            Button generalPage = CreateButton(root.transform, "GeneralPageButton",
                "settings.tab_general", font, new Vector2(-300f, 335f));
            Button bindingsPage = CreateButton(root.transform, "BindingsPageButton",
                "settings.tab_bindings", font, new Vector2(0f, 335f));
            bindingsPage.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 72f);
            Button accessibilityPage = CreateButton(root.transform,
                "AccessibilityPageButton", "settings.tab_accessibility", font,
                new Vector2(380f, 335f));
            accessibilityPage.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 72f);

            RectTransform general = CreatePanel(root.transform, "GeneralPanel",
                Vector2.zero, Vector2.one);
            Text displayModeLabel = CreateText(general, "DisplayModeLabel",
                "settings.display_mode", font, 25,
                new Vector2(0.18f, 0.67f), new Vector2(0.43f, 0.74f));
            SetFixedRect(displayModeLabel.rectTransform,
                new Vector2(-350f, 180f), new Vector2(420f, 60f));
            Dropdown displayMode = CreateDropdown(
                general, "DisplayModeDropdown", font, new Vector2(260f, 180f),
                "settings.display_mode", "settings.display_windowed");
            displayMode.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 64f);
            Text resolutionLabel = CreateText(general, "ResolutionLabel",
                "settings.resolution", font, 25,
                new Vector2(0.18f, 0.58f), new Vector2(0.43f, 0.65f));
            SetFixedRect(resolutionLabel.rectTransform,
                new Vector2(-350f, 100f), new Vector2(420f, 60f));
            Dropdown resolution = CreateDropdown(
                general, "ResolutionDropdown", font, new Vector2(260f, 100f),
                "settings.resolution");
            resolution.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 64f);
            Toggle vSync = CreateToggle(general, "VSyncToggle", "settings.vsync", font,
                new Vector2(180f, 20f));
            Text presentationSpeedLabel = CreateText(general, "PresentationSpeedLabel",
                "settings.presentation_speed", font, 25,
                new Vector2(0.18f, 0.39f), new Vector2(0.43f, 0.46f));
            SetFixedRect(presentationSpeedLabel.rectTransform,
                new Vector2(-350f, -60f), new Vector2(420f, 60f));
            Dropdown presentationSpeed = CreateDropdown(
                general, "PresentationSpeedDropdown", font, new Vector2(260f, -60f),
                "settings.presentation_speed", "settings.speed_normal");
            presentationSpeed.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 64f);
            Text masterLabel = CreateText(general, "MasterVolumeLabel",
                "settings.master_volume_value", font, 24,
                new Vector2(0.18f, 0.31f), new Vector2(0.4f, 0.37f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("settings.master_volume_value", 80));
            SetFixedRect(masterLabel.rectTransform,
                new Vector2(-350f, -150f), new Vector2(420f, 60f));
            Slider master = CreateSlider(general, "MasterVolumeSlider",
                new Vector2(250f, -150f), "settings.master_volume");
            Text musicLabel = CreateText(general, "MusicVolumeLabel",
                "settings.music_volume_value", font, 24,
                new Vector2(0.18f, 0.23f), new Vector2(0.4f, 0.29f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("settings.music_volume_value", 60));
            SetFixedRect(musicLabel.rectTransform,
                new Vector2(-350f, -235f), new Vector2(420f, 60f));
            Slider music = CreateSlider(general, "MusicVolumeSlider",
                new Vector2(250f, -235f), "settings.music_volume");
            Text sfxLabel = CreateText(general, "SfxVolumeLabel",
                "settings.sfx_volume_value", font, 24,
                new Vector2(0.18f, 0.15f), new Vector2(0.4f, 0.21f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("settings.sfx_volume_value", 80));
            SetFixedRect(sfxLabel.rectTransform,
                new Vector2(-350f, -320f), new Vector2(420f, 60f));
            Slider sfx = CreateSlider(general, "SfxVolumeSlider",
                new Vector2(250f, -320f), "settings.sfx_volume");

            RectTransform bindings = CreatePanel(root.transform, "BindingsPanel",
                Vector2.zero, Vector2.one);
            CreateText(bindings, "KeyboardHeader", "settings.keyboard", font, 25,
                new Vector2(0.2f, 0.68f), new Vector2(0.46f, 0.73f));
            CreateText(bindings, "GamepadHeader", "settings.gamepad", font, 25,
                new Vector2(0.54f, 0.68f), new Vector2(0.8f, 0.73f));
            string[] commandNames = { "Up", "Down", "Left", "Right", "Submit", "Back", "Help" };
            string[] commandKeys =
            {
                "settings.command_up", "settings.command_down", "settings.command_left",
                "settings.command_right", "settings.command_submit",
                "settings.command_back", "settings.command_help"
            };
            var keyboardButtons = new Button[commandNames.Length];
            var gamepadButtons = new Button[commandNames.Length];
            for (int index = 0; index < commandNames.Length; index++)
            {
                float y = 135f - index * 60f;
                keyboardButtons[index] = CreateButton(bindings,
                    "Keyboard" + commandNames[index] + "Button", commandKeys[index], font,
                    new Vector2(-300f, y), ProductTextContentMode.Dynamic,
                    labelFontSize: 20);
                gamepadButtons[index] = CreateButton(bindings,
                    "Gamepad" + commandNames[index] + "Button", commandKeys[index], font,
                    new Vector2(300f, y), ProductTextContentMode.Dynamic,
                    labelFontSize: 20);
                keyboardButtons[index].GetComponent<RectTransform>().sizeDelta =
                    new Vector2(520f, 58f);
                gamepadButtons[index].GetComponent<RectTransform>().sizeDelta =
                    new Vector2(520f, 58f);
            }
            Button cancelRebind = CreateButton(bindings, "CancelRebindButton",
                "settings.cancel_rebind", font, new Vector2(0f, -310f));
            cancelRebind.GetComponent<RectTransform>().sizeDelta = new Vector2(520f, 72f);

            RectTransform accessibility = CreatePanel(root.transform, "AccessibilityPanel",
                Vector2.zero, Vector2.one);
            CreateText(accessibility, "LocaleLabel", "settings.locale", font, 27,
                new Vector2(0.22f, 0.61f), new Vector2(0.45f, 0.69f));
            Dropdown locale = CreateDropdown(accessibility, "LocaleDropdown", font,
                new Vector2(240f, 155f), "settings.locale", "settings.locale_en");
            locale.GetComponent<RectTransform>().sizeDelta = new Vector2(480f, 68f);
            CreateText(accessibility, "TextScaleLabel", "settings.text_scale", font, 27,
                new Vector2(0.22f, 0.49f), new Vector2(0.45f, 0.57f));
            Dropdown textScale = CreateDropdown(accessibility, "TextScaleDropdown", font,
                new Vector2(240f, 25f), "settings.text_scale");
            textScale.GetComponent<RectTransform>().sizeDelta = new Vector2(480f, 68f);
            Toggle highContrast = CreateToggle(accessibility, "HighContrastToggle",
                "settings.high_contrast", font, new Vector2(180f, -120f));
            Toggle reducedMotion = CreateToggle(accessibility, "ReducedMotionToggle",
                "settings.reduced_motion", font, new Vector2(180f, -220f));

            Text feedback = CreateText(root.transform, "Feedback", "settings.feedback", font, 18,
                new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.14f),
                ProductTextContentMode.Dynamic, string.Empty);
            SetFixedRect(feedback.rectTransform,
                new Vector2(0f, 270f), new Vector2(1550f, 44f));
            Button back = CreateButton(root.transform, "BackButton", "common.back", font,
                new Vector2(-290f, -425f));
            Button reset = CreateButton(root.transform, "ResetButton", "common.reset_defaults",
                font, new Vector2(0f, -425f));
            reset.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 72f);
            Button apply = CreateButton(root.transform, "ApplyButton", "common.apply", font,
                new Vector2(290f, -425f));

            root.GetComponent<ProductSettingsScreen>().Configure(
                general.gameObject, bindings.gameObject, accessibility.gameObject,
                generalPage, bindingsPage, accessibilityPage, displayMode, resolution,
                vSync, master, music, sfx, masterLabel, musicLabel, sfxLabel,
                presentationSpeed, locale, textScale, highContrast, reducedMotion,
                keyboardButtons, gamepadButtons, cancelRebind, feedback, apply, reset, back);
            bindings.gameObject.SetActive(false);
            accessibility.gameObject.SetActive(false);
            cancelRebind.gameObject.SetActive(false);
            return SavePrefab(root, PrefabDirectory + "/ProductSettingsScreen.prefab");
        }

        private static GameObject CreateMatchPrefab(Font font)
        {
            GameObject root = ScreenRoot<MatchScreen>("MatchScreen");
            Text status = CreateText(root.transform, "Status", "match.status_human", font, 34,
                new Vector2(0.22f, 0.88f), new Vector2(0.78f, 0.97f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("match.status_human", 1));
            Text opponent = CreateText(root.transform, "OpponentHand", "match.opponent_hand",
                font, 30, new Vector2(0.18f, 0.72f), new Vector2(0.82f, 0.84f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("match.opponent_hand", "# # # # # # #"));
            Text stock = CreateText(root.transform, "Stock", "match.stock", font, 30,
                new Vector2(0.2f, 0.48f), new Vector2(0.4f, 0.62f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("match.stock", 37));
            Text discard = CreateText(root.transform, "Discard", "match.discard", font, 30,
                new Vector2(0.6f, 0.48f), new Vector2(0.8f, 0.62f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("match.discard", "7H"));
            Text hand = CreateText(root.transform, "HumanHand", "match.human_hand", font, 30,
                new Vector2(0.12f, 0.29f), new Vector2(0.88f, 0.40f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("match.human_hand", "AC 3D 4H 7S 8C 10D KH"));
            RectTransform actionViewport = CreatePanel(root.transform, "ActionViewport",
                new Vector2(0.18f, 0.01f), new Vector2(0.82f, 0.22f));
            Image viewportImage = actionViewport.gameObject.AddComponent<Image>();
            ConfigureGraphic(viewportImage, ProductGraphicRole.Surface);
            var mask = actionViewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var scroll = actionViewport.gameObject.AddComponent<ScrollRect>();
            RectTransform actions = CreatePanel(actionViewport, "ActionRoot", Vector2.zero, Vector2.one);
            actions.anchorMin = new Vector2(0f, 1f);
            actions.anchorMax = new Vector2(1f, 1f);
            actions.pivot = new Vector2(0.5f, 1f);
            actions.sizeDelta = Vector2.zero;
            GridLayoutGroup actionGrid = actions.gameObject.AddComponent<GridLayoutGroup>();
            actionGrid.cellSize = new Vector2(225f, 190f);
            actionGrid.spacing = new Vector2(10f, 8f);
            actionGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            actionGrid.constraintCount = 4;
            var fitter = actions.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = actions;
            scroll.viewport = actionViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            Text actionSummary = CreateText(root.transform, "ActionSummary",
                "match.action_summary_locked", font, 22,
                new Vector2(0.18f, 0.22f), new Vector2(0.82f, 0.27f),
                ProductTextContentMode.Dynamic);
            Button actionTemplate = CreateButton(actions, "ActionButtonTemplate",
                "match.action_button", font, Vector2.zero, ProductTextContentMode.Dynamic,
                "match.action_control", labelFontSize: 17);
            actionTemplate.GetComponent<ProductUiFeedbackEmitter>()
                .SetSubmitFeedbackEnabled(false);
            actionTemplate.gameObject.SetActive(false);
            Button help = CreateButton(root.transform, "HelpButton", "match.help", font,
                new Vector2(710f, 435f));
            help.GetComponent<RectTransform>().sizeDelta = new Vector2(170f, 56f);
            Button settings = CreateButton(root.transform, "SettingsButton", "match.settings", font,
                new Vector2(710f, 365f));
            settings.GetComponent<RectTransform>().sizeDelta = new Vector2(170f, 56f);
            Button rules = CreateButton(root.transform, "RulesButton", "match.rules", font,
                new Vector2(710f, 295f));
            rules.GetComponent<RectTransform>().sizeDelta = new Vector2(170f, 56f);
            RectTransform helpPanel = CreatePanel(root.transform, "ContextHelpPanel",
                new Vector2(0.16f, 0.02f), new Vector2(0.84f, 0.98f));
            Image helpBackground = helpPanel.gameObject.AddComponent<Image>();
            ConfigureGraphic(helpBackground, ProductGraphicRole.Surface);
            Text helpText = CreateText(helpPanel, "ContextHelpText",
                "match.context_opening", font, 20,
                new Vector2(0.06f, 0.085f), new Vector2(0.94f, 0.98f),
                ProductTextContentMode.Dynamic);
            helpText.alignment = TextAnchor.UpperLeft;
            helpText.verticalOverflow = VerticalWrapMode.Overflow;
            Button closeHelp = CreateButton(helpPanel, "CloseHelpButton", "match.close_help", font,
                new Vector2(0f, -330f));
            RectTransform closeHelpRect = closeHelp.GetComponent<RectTransform>();
            closeHelpRect.anchorMin = closeHelpRect.anchorMax = new Vector2(0.5f, 0.035f);
            closeHelpRect.anchoredPosition = Vector2.zero;
            RectTransform tutorialPanel = CreatePanel(root.transform, "TutorialPanel",
                new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.86f));
            Image tutorialBackground = tutorialPanel.gameObject.AddComponent<Image>();
            ConfigureGraphic(tutorialBackground, ProductGraphicRole.Surface);
            Text tutorialProgress = CreateText(tutorialPanel, "TutorialProgress",
                "tutorial.progress", font, 22,
                new Vector2(0.04f, 0.78f), new Vector2(0.2f, 0.96f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("tutorial.progress", 1, 6));
            tutorialProgress.alignment = TextAnchor.MiddleLeft;
            Text tutorialHeading = CreateText(tutorialPanel, "TutorialHeading",
                "tutorial.intro.heading", font, 30,
                new Vector2(0.2f, 0.72f), new Vector2(0.78f, 0.98f),
                ProductTextContentMode.Dynamic);
            Text tutorialInstruction = CreateText(tutorialPanel, "TutorialInstruction",
                "tutorial.intro", font, 22,
                new Vector2(0.04f, 0.4f), new Vector2(0.96f, 0.7f),
                ProductTextContentMode.Dynamic);
            tutorialInstruction.alignment = TextAnchor.UpperLeft;
            Text tutorialGuidance = CreateText(tutorialPanel, "TutorialGuidance",
                "tutorial.guidance_default", font, 20,
                new Vector2(0.04f, 0.1f), new Vector2(0.5f, 0.37f),
                ProductTextContentMode.Dynamic);
            tutorialGuidance.alignment = TextAnchor.UpperLeft;
            Button tutorialContinue = CreateButton(tutorialPanel, "TutorialContinueButton",
                "tutorial.continue_start", font, new Vector2(230f, -95f),
                ProductTextContentMode.Dynamic, labelFontSize: 20);
            tutorialContinue.GetComponent<RectTransform>().sizeDelta = new Vector2(380f, 96f);
            Button tutorialExit = CreateButton(tutorialPanel, "TutorialExitButton",
                "tutorial.exit", font, new Vector2(550f, -95f), labelFontSize: 20);
            tutorialExit.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 96f);
            root.GetComponent<MatchScreen>().Configure(
                status, opponent, stock, discard, hand, actionSummary, actions, actionTemplate,
                help, settings, rules, helpPanel.gameObject, helpText, closeHelp,
                tutorialPanel.gameObject, tutorialProgress, tutorialHeading,
                tutorialInstruction, tutorialGuidance, tutorialContinue, tutorialExit);
            helpPanel.gameObject.SetActive(false);
            tutorialPanel.gameObject.SetActive(false);
            return SavePrefab(root, PrefabDirectory + "/MatchScreen.prefab");
        }

        private static GameObject CreateSessionLibraryPrefab(Font font)
        {
            GameObject root = ScreenRoot<SessionLibraryScreen>("SessionLibraryScreen");
            CreateText(root.transform, "Title", "library.title", font, 52,
                new Vector2(0.2f, 0.79f), new Vector2(0.8f, 0.91f));
            Dropdown dropdown = CreateDropdown(root.transform, "SlotDropdown", font,
                new Vector2(0f, 180f), "title.saved_sessions", "library.empty_option");
            dropdown.GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 112f);
            Text detail = CreateText(root.transform, "Detail",
                "library.empty", font, 25,
                new Vector2(0.16f, 0.46f), new Vector2(0.84f, 0.6f),
                ProductTextContentMode.Dynamic);
            Button resume = CreateButton(root.transform, "ResumeButton", "library.resume", font,
                new Vector2(-270f, -105f));
            Button replay = CreateButton(root.transform, "ReplayButton", "library.replay", font,
                new Vector2(0f, -105f));
            Button delete = CreateButton(root.transform, "DeleteButton", "library.delete", font,
                new Vector2(270f, -105f), ProductTextContentMode.Dynamic);
            Button back = CreateButton(root.transform, "BackButton", "common.back", font,
                new Vector2(0f, -220f));
            root.GetComponent<SessionLibraryScreen>().Configure(
                dropdown, detail, resume, replay, delete, back);
            return SavePrefab(root, PrefabDirectory + "/SessionLibraryScreen.prefab");
        }

        private static GameObject CreateReplayPrefab(Font font)
        {
            GameObject root = ScreenRoot<ReplayScreen>("ReplayScreen");
            CreateText(root.transform, "Title", "replay.title", font, 52,
                new Vector2(0.2f, 0.8f), new Vector2(0.8f, 0.92f));
            Text status = CreateText(root.transform, "Status", "replay.status", font, 30,
                new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.78f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("replay.status", 0,
                    ProductTextCatalog.English.Get("replay.not_finished")));
            Text table = CreateText(root.transform, "Table", "replay.table", font, 28,
                new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.66f),
                ProductTextContentMode.Dynamic);
            Button back = CreateButton(root.transform, "BackButton", "replay.back", font,
                new Vector2(0f, -400f));
            back.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 72f);
            root.GetComponent<ReplayScreen>().Configure(status, table, back);
            return SavePrefab(root, PrefabDirectory + "/ReplayScreen.prefab");
        }

        private static GameObject CreateResultPrefab(Font font)
        {
            GameObject root = ScreenRoot<ResultScreen>("ResultScreen");
            CreateText(root.transform, "Title", "result.title", font, 56,
                new Vector2(0.2f, 0.7f), new Vector2(0.8f, 0.84f));
            Text summary = CreateText(root.transform, "Summary", "result.summary", font, 32,
                new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.68f),
                ProductTextContentMode.Dynamic);
            Button details = CreateButton(root.transform, "DetailsButton", "result.details", font,
                new Vector2(0f, -160f));
            details.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 72f);
            Button rematch = CreateButton(root.transform, "RematchButton", "result.rematch", font,
                new Vector2(140f, -300f));
            Button title = CreateButton(root.transform, "TitleButton", "common.title", font,
                new Vector2(-140f, -300f));
            root.GetComponent<ResultScreen>().Configure(summary, details, rematch, title);
            return SavePrefab(root, PrefabDirectory + "/ResultScreen.prefab");
        }

        private static GameObject CreateHowToPlayPrefab(Font font)
        {
            GameObject root = ScreenRoot<HowToPlayScreen>("HowToPlayScreen");
            CreateText(root.transform, "ScreenTitle", "rules.screen_title", font, 48,
                new Vector2(0.18f, 0.84f), new Vector2(0.82f, 0.94f));
            Text context = CreateText(root.transform, "Context",
                "rules.context_read_only", font, 23,
                new Vector2(0.16f, 0.76f), new Vector2(0.84f, 0.83f),
                ProductTextContentMode.Dynamic);
            Text indicator = CreateText(root.transform, "PageIndicator",
                "rules.page_indicator", font, 24,
                new Vector2(0.17f, 0.68f), new Vector2(0.33f, 0.75f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get("rules.page_indicator", 1, 5));
            Text pageTitle = CreateText(root.transform, "PageTitle",
                "rules.crazy_eights.objective.title", font, 38,
                new Vector2(0.25f, 0.58f), new Vector2(0.75f, 0.68f),
                ProductTextContentMode.Dynamic);
            Text body = CreateText(root.transform, "PageBody",
                "rules.crazy_eights.objective", font, 22,
                new Vector2(0.16f, 0.25f), new Vector2(0.84f, 0.57f),
                ProductTextContentMode.Dynamic);
            body.rectTransform.anchorMin = new Vector2(0.16f, 0.5f);
            body.rectTransform.anchorMax = new Vector2(0.84f, 0.5f);
            body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body.rectTransform.anchoredPosition = new Vector2(0f, -107.5f);
            body.rectTransform.sizeDelta = new Vector2(0f, 365f);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            Button startTutorial = CreateButton(root.transform, "StartTutorialButton",
                "rules.start_tutorial", font, new Vector2(0f, -335f));
            startTutorial.GetComponent<RectTransform>().sizeDelta = new Vector2(520f, 72f);
            Button previous = CreateButton(root.transform, "PreviousButton", "rules.previous", font,
                new Vector2(-300f, -425f));
            Button next = CreateButton(root.transform, "NextButton", "rules.next", font,
                new Vector2(0f, -425f));
            Button back = CreateButton(root.transform, "BackButton", "common.back", font,
                new Vector2(300f, -425f));
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
            ConfigureGraphic(background, ProductGraphicRole.ErrorBackground);
            CreateText(root.transform, "Title", "error.panel_title", font, 48,
                new Vector2(0.2f, 0.67f), new Vector2(0.8f, 0.8f));
            Text message = CreateText(root.transform, "Message", "error.match_stopped", font, 28,
                new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.64f),
                ProductTextContentMode.Dynamic,
                graphicRole: ProductGraphicRole.ErrorText);
            Button dismiss = CreateButton(root.transform, "DismissButton", "error.dismiss", font,
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
            ConfigureGraphic(transitionImage, ProductGraphicRole.Background);
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
            ConfigureGraphic(bannerImage, ProductGraphicRole.ActiveControlBackground);
            bannerImage.raycastTarget = false;
            CanvasGroup banner = bannerObject.GetComponent<CanvasGroup>();
            banner.alpha = 0f;
            banner.interactable = false;
            banner.blocksRaycasts = false;
            Text bannerText = CreateText(bannerObject.transform, "FeedbackText",
                "feedback.navigation", font, 30,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f),
                ProductTextContentMode.Dynamic,
                graphicRole: ProductGraphicRole.ControlText);

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
            ConfigureGraphic(background, ProductGraphicRole.Background);
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

        private static ProductSafeFrame CreateSafeFrame(Canvas canvas)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            var root = new GameObject("SafeFrame", typeof(RectTransform),
                typeof(ProductSafeFrame));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            Stretch(rect);
            ProductSafeFrame frame = root.GetComponent<ProductSafeFrame>();
            frame.Configure((RectTransform)canvas.transform);
            return frame;
        }

        private static InputSystemUIInputModule CreateEventSystem()
        {
            var root = new GameObject("EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            InputSystemUIInputModule module = root.GetComponent<InputSystemUIInputModule>();
            module.UnassignActions();
            return module;
        }

        private static Text CreateText(Transform parent, string name, string stableKey, Font font,
            int size, Vector2 anchorMin, Vector2 anchorMax,
            ProductTextContentMode mode = ProductTextContentMode.Static,
            string? initialValue = null,
            ProductGraphicRole? graphicRole = null)
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
            text.text = InitialText(stableKey, mode, initialValue);
            text.raycastTarget = false;
            ProductTextElement textElement = root.AddComponent<ProductTextElement>();
            textElement.Configure(text, mode, stableKey, size);
            ConfigureGraphic(text, graphicRole ?? (size >= 30
                ? ProductGraphicRole.LargeText
                : ProductGraphicRole.NormalText));
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string labelKey,
            Font font, Vector2 anchoredPosition,
            ProductTextContentMode labelMode = ProductTextContentMode.Static,
            string? accessibleLabelKey = null, int labelFontSize = 28)
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
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            ConfigureGraphic(image, ProductGraphicRole.ControlBackground);
            CreateText(root.transform, "Label", labelKey, font, labelFontSize,
                Vector2.zero, Vector2.one, labelMode,
                graphicRole: ProductGraphicRole.ControlText);
            ConfigureControl(button, accessibleLabelKey ?? labelKey);
            return button;
        }

        private static Toggle CreateToggle(Transform parent, string name, string labelKey,
            Font font, Vector2 anchoredPosition)
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
            ConfigureGraphic(background, ProductGraphicRole.ControlBackground);

            var checkmarkObject = new GameObject("Checkmark", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.18f, 0.18f);
            checkmarkRect.anchorMax = new Vector2(0.82f, 0.82f);
            checkmarkRect.offsetMin = checkmarkRect.offsetMax = Vector2.zero;
            Image checkmark = checkmarkObject.GetComponent<Image>();
            ConfigureGraphic(checkmark, ProductGraphicRole.ActiveControlBackground);

            Text text = CreateText(root.transform, "Label", labelKey, font, 25,
                new Vector2(0.16f, 0f), Vector2.one,
                graphicRole: ProductGraphicRole.NormalText);
            text.alignment = TextAnchor.MiddleLeft;
            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = true;
            ConfigureControl(toggle, labelKey);
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, string name,
            Vector2 anchoredPosition, string labelKey)
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
            ConfigureGraphic(background, ProductGraphicRole.Surface);

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
            ConfigureGraphic(fill, ProductGraphicRole.ActiveControlBackground);

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
            ConfigureGraphic(handle, ProductGraphicRole.ControlBackground);

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = 80f;
            ConfigureControl(slider, labelKey);
            return slider;
        }

        private static InputField CreateInputField(Transform parent, string name, string value,
            string placeholderKey, Font font, Vector2 anchoredPosition, string labelKey)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(ProductUiFeedbackEmitter), typeof(InputField));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(500f, 64f);
            rect.anchoredPosition = anchoredPosition;
            Image background = root.GetComponent<Image>();
            ConfigureGraphic(background, ProductGraphicRole.ControlBackground);
            Text text = CreateText(root.transform, "Text", "input.value", font, 28,
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f),
                ProductTextContentMode.LocaleNeutral, value,
                ProductGraphicRole.ControlText);
            text.alignment = TextAnchor.MiddleLeft;
            Text placeholder = CreateText(root.transform, "Placeholder", placeholderKey, font, 28,
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f),
                graphicRole: ProductGraphicRole.MutedText);
            placeholder.alignment = TextAnchor.MiddleLeft;
            InputField input = root.GetComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.text = value;
            ConfigureControl(input, labelKey);
            return input;
        }

        private static Dropdown CreateDropdown(Transform parent, string name, Font font,
            Vector2 anchoredPosition, string labelKey,
            string initialOptionKey = "common.none")
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(ProductUiFeedbackEmitter), typeof(Dropdown));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 72f);
            rect.anchoredPosition = anchoredPosition;
            Image background = root.GetComponent<Image>();
            ConfigureGraphic(background, ProductGraphicRole.ControlBackground);
            Text caption = CreateText(root.transform, "Label", labelKey, font, 26,
                new Vector2(0.04f, 0.08f), new Vector2(0.9f, 0.92f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get(initialOptionKey),
                ProductGraphicRole.ControlText);
            caption.alignment = TextAnchor.MiddleLeft;
            CreateText(root.transform, "Arrow", "dropdown.arrow", font, 25,
                new Vector2(0.9f, 0.08f), new Vector2(0.98f, 0.92f),
                ProductTextContentMode.LocaleNeutral, "v",
                ProductGraphicRole.ControlText);

            var templateObject = new GameObject("Template", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            templateObject.transform.SetParent(root.transform, false);
            RectTransform template = templateObject.GetComponent<RectTransform>();
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -4f);
            template.sizeDelta = new Vector2(0f, 280f);
            ConfigureGraphic(templateObject.GetComponent<Image>(),
                ProductGraphicRole.Surface);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(template, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            ConfigureGraphic(viewportObject.GetComponent<Image>(),
                ProductGraphicRole.Surface);
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
            RectTransform itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.sizeDelta = new Vector2(0f, 60f);
            itemObject.GetComponent<LayoutElement>().preferredHeight = 60f;
            Image itemBackground = itemObject.GetComponent<Image>();
            ConfigureGraphic(itemBackground, ProductGraphicRole.ControlBackground);
            Toggle item = itemObject.GetComponent<Toggle>();
            item.targetGraphic = itemBackground;
            Text itemLabel = CreateText(itemObject.transform, "Item Label", labelKey, font, 24,
                new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                ProductTextContentMode.Dynamic,
                ProductTextCatalog.English.Get(initialOptionKey),
                ProductGraphicRole.ControlText);
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
            dropdown.options.Add(new Dropdown.OptionData(
                ProductTextCatalog.English.Get(initialOptionKey)));
            templateObject.SetActive(false);
            ConfigureControl(item, labelKey);
            ConfigureControl(dropdown, labelKey);
            return dropdown;
        }

        private static string InitialText(string stableKey, ProductTextContentMode mode,
            string? initialValue)
        {
            if (mode == ProductTextContentMode.Static)
                return ProductTextCatalog.English.Get(stableKey);
            if (initialValue != null) return initialValue;
            if (!ProductTextCatalog.Contains(stableKey)) return string.Empty;
            ProductTextEntry entry = ProductTextCatalog.Entry(stableKey);
            object[] arguments = Enumerable.Range(0, entry.ArgumentCount)
                .Select(index => (object)(index + 1)).ToArray();
            return ProductTextCatalog.English.Get(stableKey, arguments);
        }

        private static void ConfigureGraphic(Graphic graphic, ProductGraphicRole role)
        {
            if (graphic == null) throw new ArgumentNullException(nameof(graphic));
            ProductGraphicElement element = graphic.GetComponent<ProductGraphicElement>() ??
                graphic.gameObject.AddComponent<ProductGraphicElement>();
            element.Configure(graphic, role);
        }

        private static void ConfigureControl(Selectable selectable, string labelKey)
        {
            if (selectable == null) throw new ArgumentNullException(nameof(selectable));
            ProductAccessibleControl accessible =
                selectable.GetComponent<ProductAccessibleControl>() ??
                selectable.gameObject.AddComponent<ProductAccessibleControl>();
            accessible.Configure(selectable, labelKey);
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

        private static void SetFixedRect(RectTransform rect, Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
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
            PlayerSettings.SetUseDefaultGraphicsAPIs(
                BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });
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
                ValidateNoEmbeddedMonoScripts(path);
            }

            ValidateNoEmbeddedMonoScripts(ScenePath);

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
            ValidateLocalizationAccessibilityContract(roots, controller, canvas);
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

        private static void ValidateNoEmbeddedMonoScripts(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException("Unity project root is unavailable.");
            string physicalPath = Path.Combine(projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            string yaml = File.ReadAllText(physicalPath);
            if (yaml.IndexOf("--- !u!115 &", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException(
                    "Generated asset embeds a MonoScript instead of referencing its stable meta GUID: " +
                    assetPath);
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
            ProductSafeFrame safeFrame = canvas.GetComponentInChildren<ProductSafeFrame>(true);
            if (safeFrame == null || presentation.Banner.transform.parent != safeFrame.transform ||
                presentation.Transition.transform.parent != safeFrame.transform ||
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

        private static void ValidateLocalizationAccessibilityContract(GameObject[] roots,
            ProductAppController controller, Canvas canvas)
        {
            ProductTextCatalog.Validate();
            ProductLocalizationController[] localizations = roots.SelectMany(root =>
                root.GetComponentsInChildren<ProductLocalizationController>(true)).ToArray();
            ProductAccessibilityController[] accessibilities = roots.SelectMany(root =>
                root.GetComponentsInChildren<ProductAccessibilityController>(true)).ToArray();
            ProductSafeFrame[] safeFrames = canvas.GetComponentsInChildren<ProductSafeFrame>(true);
            if (localizations.Length != 1 || accessibilities.Length != 1 ||
                safeFrames.Length != 1 || controller.LocalizationController != localizations[0] ||
                controller.AccessibilityController != accessibilities[0])
                throw new InvalidOperationException(
                    "Bootstrap scene requires one wired localization, accessibility, and safe-frame contract.");

            ProductLocalizationController localization = localizations[0];
            ProductAccessibilityController accessibility = accessibilities[0];
            ProductSafeFrame safeFrame = safeFrames[0];
            if (localization.UiRoot != canvas.transform ||
                accessibility.UiRoot != (RectTransform)canvas.transform ||
                accessibility.SafeFrame != safeFrame ||
                safeFrame.transform.parent != canvas.transform)
                throw new InvalidOperationException(
                    "Product localization and accessibility roots are not wired to ProductCanvas.");
            if (controller.Router.Screens.Any(screen => screen.transform.parent != safeFrame.transform) ||
                controller.ErrorPanel.transform.parent != safeFrame.transform)
                throw new InvalidOperationException(
                    "Product screens and modal surfaces must remain inside the centered safe frame.");

            Text[] texts = canvas.GetComponentsInChildren<Text>(true);
            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
            Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
            if (texts.Length == 0 || texts.Any(text =>
                    text.GetComponent<ProductTextElement>() == null))
                throw new InvalidOperationException(
                    "Every generated product Text requires a stable localization element.");
            foreach (Text text in texts)
            {
                ProductTextElement element = text.GetComponent<ProductTextElement>() ??
                    throw new InvalidOperationException(
                        "Generated product text element disappeared: " + text.name);
                if (element.BaseFontSize <= 0 || string.IsNullOrWhiteSpace(element.StableKey) ||
                    element.ContentMode == ProductTextContentMode.Static &&
                    !ProductTextCatalog.Contains(element.StableKey))
                    throw new InvalidOperationException(
                        "Generated product text classification is invalid: " + element.name);
            }
            if (graphics.Length == 0 || graphics.Any(graphic =>
                    graphic.GetComponent<ProductGraphicElement>() == null))
                throw new InvalidOperationException(
                    "Every generated product Graphic requires a semantic palette role.");
            if (selectables.Length == 0 || selectables.Any(selectable =>
                    selectable.GetComponent<ProductAccessibleControl>() == null))
                throw new InvalidOperationException(
                    "Every generated product Selectable requires an accessible label and focus marker.");

            Canvas.ForceUpdateCanvases();
            foreach (Selectable selectable in selectables)
            {
                ProductAccessibleControl accessible =
                    selectable.GetComponent<ProductAccessibleControl>();
                if (!ProductTextCatalog.Contains(accessible.LabelKey) ||
                    ProductTextCatalog.Entry(accessible.LabelKey).ArgumentCount != 0 ||
                    !accessible.HasMinimumReferenceHitTarget)
                    throw new InvalidOperationException(
                        "Generated product control accessibility is invalid: " + selectable.name);
            }

            string[] bundledFonts = AssetDatabase.FindAssets(string.Empty, new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (bundledFonts.Length != 0)
                throw new InvalidOperationException(
                    "Product localization must use Windows fonts without redistributing font files.");
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
