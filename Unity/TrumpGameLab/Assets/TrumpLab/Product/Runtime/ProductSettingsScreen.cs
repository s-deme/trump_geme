#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class ProductSettingsScreen : ProductScreen, IPreferredFocusProvider
    {
        private static readonly ProductDisplayMode[] DisplayModes =
            { ProductDisplayMode.Windowed, ProductDisplayMode.Borderless };
        private static readonly ProductPresentationSpeed[] PresentationSpeeds =
        {
            ProductPresentationSpeed.Reduced,
            ProductPresentationSpeed.Normal,
            ProductPresentationSpeed.Fast
        };
        private static readonly string[] Locales = { "en-US", "ja-JP" };
        private static readonly ProductInputCommand[] Commands =
        {
            ProductInputCommand.Up,
            ProductInputCommand.Down,
            ProductInputCommand.Left,
            ProductInputCommand.Right,
            ProductInputCommand.Submit,
            ProductInputCommand.Cancel,
            ProductInputCommand.Help
        };

        [SerializeField] private GameObject? generalPanel;
        [SerializeField] private GameObject? bindingsPanel;
        [SerializeField] private GameObject? accessibilityPanel;
        [SerializeField] private Button? generalPageButton;
        [SerializeField] private Button? bindingsPageButton;
        [SerializeField] private Button? accessibilityPageButton;
        [SerializeField] private Dropdown? displayModeDropdown;
        [SerializeField] private Dropdown? resolutionDropdown;
        [SerializeField] private Toggle? vSyncToggle;
        [SerializeField] private Slider? masterVolumeSlider;
        [SerializeField] private Slider? musicVolumeSlider;
        [SerializeField] private Slider? sfxVolumeSlider;
        [SerializeField] private Text? masterVolumeLabel;
        [SerializeField] private Text? musicVolumeLabel;
        [SerializeField] private Text? sfxVolumeLabel;
        [SerializeField] private Dropdown? presentationSpeedDropdown;
        [SerializeField] private Dropdown? localeDropdown;
        [SerializeField] private Dropdown? textScaleDropdown;
        [SerializeField] private Toggle? highContrastToggle;
        [SerializeField] private Toggle? reducedMotionToggle;
        [SerializeField] private Button[] keyboardBindingButtons = Array.Empty<Button>();
        [SerializeField] private Button[] gamepadBindingButtons = Array.Empty<Button>();
        [SerializeField] private Button? cancelRebindButton;
        [SerializeField] private Text? feedbackLabel;
        [SerializeField] private Button? applyButton;
        [SerializeField] private Button? resetButton;
        [SerializeField] private Button? backButton;

        private ProductSettings? sourceSettings;
        private ProductInputBindings editingBindings = ProductInputBindings.Default;
        private IProductText text = ProductTextCatalog.English;

        public override ScreenId Id => ScreenId.ProductSettings;
        public Selectable? PreferredFocus => generalPageButton;
        public Dropdown DisplayModeDropdown => Require(displayModeDropdown, "Display mode");
        public Dropdown ResolutionDropdown => Require(resolutionDropdown, "Resolution");
        public Toggle VSyncToggle => Require(vSyncToggle, "VSync");
        public Slider MasterVolumeSlider => Require(masterVolumeSlider, "Master volume");
        public Slider MusicVolumeSlider => Require(musicVolumeSlider, "Music volume");
        public Slider SfxVolumeSlider => Require(sfxVolumeSlider, "SFX volume");
        public Dropdown PresentationSpeedDropdown => Require(
            presentationSpeedDropdown, "Presentation speed");
        public Button AccessibilityPageButton => Require(
            accessibilityPageButton, "Accessibility page");
        public Dropdown LocaleDropdown => Require(localeDropdown, "Locale");
        public Dropdown TextScaleDropdown => Require(textScaleDropdown, "Text scale");
        public Toggle HighContrastToggle => Require(highContrastToggle, "High contrast");
        public Toggle ReducedMotionToggle => Require(reducedMotionToggle, "Reduced motion");
        public IReadOnlyList<Button> KeyboardBindingButtons => keyboardBindingButtons;
        public IReadOnlyList<Button> GamepadBindingButtons => gamepadBindingButtons;
        public Text FeedbackLabel => Require(feedbackLabel, "Settings feedback");

        public event System.Action<ProductSettings>? ApplyRequested;
        public event System.Action? ResetRequested;
        public event System.Action? BackRequested;
        public event System.Action<ProductInputScheme, ProductInputCommand>? RebindRequested;
        public event System.Action? CancelRebindRequested;
        public event System.Action? ValidationRejected;

        public void Configure(GameObject general, GameObject bindings,
            Button generalPage, Button bindingsPage, Dropdown displayMode,
            Dropdown resolution, Toggle vSync, Slider masterVolume, Slider musicVolume,
            Slider sfxVolume, Text masterLabel, Text musicLabel, Text sfxLabel,
            Dropdown presentationSpeed, Button[] keyboardButtons, Button[] gamepadButtons,
            Button cancelRebind, Text feedback, Button apply, Button reset, Button back)
        {
            generalPanel = general;
            bindingsPanel = bindings;
            generalPageButton = generalPage;
            bindingsPageButton = bindingsPage;
            displayModeDropdown = displayMode;
            resolutionDropdown = resolution;
            vSyncToggle = vSync;
            masterVolumeSlider = masterVolume;
            musicVolumeSlider = musicVolume;
            sfxVolumeSlider = sfxVolume;
            masterVolumeLabel = masterLabel;
            musicVolumeLabel = musicLabel;
            sfxVolumeLabel = sfxLabel;
            presentationSpeedDropdown = presentationSpeed;
            keyboardBindingButtons = keyboardButtons?.ToArray() ??
                throw new ArgumentNullException(nameof(keyboardButtons));
            gamepadBindingButtons = gamepadButtons?.ToArray() ??
                throw new ArgumentNullException(nameof(gamepadButtons));
            cancelRebindButton = cancelRebind;
            feedbackLabel = feedback;
            applyButton = apply;
            resetButton = reset;
            backButton = back;
            ConfigureOptions();
            ShowGeneralPage();
            SetRebindState(false, string.Empty);
            RefreshStaticButtonLabels();
        }

        public void Configure(GameObject general, GameObject bindings,
            GameObject accessibility, Button generalPage, Button bindingsPage,
            Button accessibilityPage, Dropdown displayMode, Dropdown resolution,
            Toggle vSync, Slider masterVolume, Slider musicVolume, Slider sfxVolume,
            Text masterLabel, Text musicLabel, Text sfxLabel,
            Dropdown presentationSpeed, Dropdown locale, Dropdown textScale,
            Toggle highContrast, Toggle reducedMotion, Button[] keyboardButtons,
            Button[] gamepadButtons, Button cancelRebind, Text feedback, Button apply,
            Button reset, Button back)
        {
            Configure(general, bindings, generalPage, bindingsPage, displayMode, resolution,
                vSync, masterVolume, musicVolume, sfxVolume, masterLabel, musicLabel,
                sfxLabel, presentationSpeed, keyboardButtons, gamepadButtons,
                cancelRebind, feedback, apply, reset, back);
            ConfigureAccessibility(accessibility, accessibilityPage, locale, textScale,
                highContrast, reducedMotion);
        }

        public void ConfigureAccessibility(GameObject accessibility, Button accessibilityPage,
            Dropdown locale, Dropdown textScale, Toggle highContrast, Toggle reducedMotion)
        {
            accessibilityPanel = accessibility ??
                throw new ArgumentNullException(nameof(accessibility));
            accessibilityPageButton = accessibilityPage ??
                throw new ArgumentNullException(nameof(accessibilityPage));
            localeDropdown = locale ?? throw new ArgumentNullException(nameof(locale));
            textScaleDropdown = textScale ?? throw new ArgumentNullException(nameof(textScale));
            highContrastToggle = highContrast ??
                throw new ArgumentNullException(nameof(highContrast));
            reducedMotionToggle = reducedMotion ??
                throw new ArgumentNullException(nameof(reducedMotion));
            ConfigureOptions();
            accessibilityPanel.SetActive(false);
            RefreshStaticButtonLabels();
        }

        public void SetText(IProductText configuredText)
        {
            text = configuredText ?? throw new ArgumentNullException(nameof(configuredText));
            ConfigureOptions();
            RefreshVolumeLabels();
            RefreshBindingLabels();
            RefreshStaticButtonLabels();
        }

        private void Awake()
        {
            ValidateConfiguration();
            ConfigureOptions();
            generalPageButton!.onClick.AddListener(ShowGeneralPage);
            bindingsPageButton!.onClick.AddListener(ShowBindingsPage);
            accessibilityPageButton?.onClick.AddListener(ShowAccessibilityPage);
            masterVolumeSlider!.onValueChanged.AddListener(HandleVolumeChanged);
            musicVolumeSlider!.onValueChanged.AddListener(HandleVolumeChanged);
            sfxVolumeSlider!.onValueChanged.AddListener(HandleVolumeChanged);
            applyButton!.onClick.AddListener(HandleApply);
            resetButton!.onClick.AddListener(HandleReset);
            backButton!.onClick.AddListener(HandleBack);
            cancelRebindButton!.onClick.AddListener(HandleCancelRebind);
            for (int index = 0; index < Commands.Length; index++)
            {
                int captured = index;
                keyboardBindingButtons[index].onClick.AddListener(
                    () => HandleRebind(ProductInputScheme.Keyboard, Commands[captured]));
                gamepadBindingButtons[index].onClick.AddListener(
                    () => HandleRebind(ProductInputScheme.Gamepad, Commands[captured]));
            }
            ShowGeneralPage();
            SetRebindState(false, string.Empty);
        }

        private void OnDestroy()
        {
            generalPageButton?.onClick.RemoveAllListeners();
            bindingsPageButton?.onClick.RemoveAllListeners();
            accessibilityPageButton?.onClick.RemoveAllListeners();
            masterVolumeSlider?.onValueChanged.RemoveAllListeners();
            musicVolumeSlider?.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider?.onValueChanged.RemoveAllListeners();
            applyButton?.onClick.RemoveAllListeners();
            resetButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            cancelRebindButton?.onClick.RemoveAllListeners();
            foreach (Button button in keyboardBindingButtons) button.onClick.RemoveAllListeners();
            foreach (Button button in gamepadBindingButtons) button.onClick.RemoveAllListeners();
        }

        public void SetValues(ProductSettings settings, string feedback = "",
            bool feedbackIsError = false)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            sourceSettings = settings;
            editingBindings = settings.InputBindings;
            DisplayModeDropdown.SetValueWithoutNotify(IndexOf(DisplayModes, settings.DisplayMode));
            ResolutionDropdown.SetValueWithoutNotify(IndexOf(
                ProductResolution.Supported, settings.Resolution));
            VSyncToggle.SetIsOnWithoutNotify(settings.VSync);
            MasterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
            MusicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
            SfxVolumeSlider.SetValueWithoutNotify(settings.SfxVolume);
            PresentationSpeedDropdown.SetValueWithoutNotify(IndexOf(
                PresentationSpeeds, settings.PresentationSpeed));
            if (localeDropdown != null)
                localeDropdown.SetValueWithoutNotify(IndexOf(Locales, settings.Locale));
            if (textScaleDropdown != null)
                textScaleDropdown.SetValueWithoutNotify(IndexOf(
                    ProductSettings.SupportedTextScalePercents, settings.TextScalePercent));
            highContrastToggle?.SetIsOnWithoutNotify(settings.HighContrast);
            reducedMotionToggle?.SetIsOnWithoutNotify(settings.ReducedMotion);
            RefreshVolumeLabels();
            RefreshBindingLabels();
            SetFeedback(feedback, feedbackIsError);
            ShowGeneralPage();
            SetRebindState(false, string.Empty);
        }

        public bool TrySetBinding(ProductInputScheme scheme, ProductInputCommand command,
            string path, out string error)
        {
            try
            {
                editingBindings = editingBindings.With(scheme, command, path);
                RefreshBindingLabels();
                SetFeedback(text.Get("settings.feedback_binding_updated"), isError: false);
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("Product binding was rejected: " + exception.Message);
                error = text.Get("settings.error_binding_invalid");
                SetFeedback(error, isError: true);
                ValidationRejected?.Invoke();
                return false;
            }
        }

        public void SetRebindState(bool active, string message)
        {
            if (cancelRebindButton == null) return;
            cancelRebindButton.gameObject.SetActive(active);
            foreach (Button button in keyboardBindingButtons) button.interactable = !active;
            foreach (Button button in gamepadBindingButtons) button.interactable = !active;
            if (!string.IsNullOrWhiteSpace(message)) SetFeedback(message, isError: false);
        }

        public void SetFeedback(string message, bool isError)
        {
            FeedbackLabel.text = message ?? string.Empty;
        }

        public bool TryReadSettings(out ProductSettings? settings, out string error)
        {
            settings = null;
            if (sourceSettings == null)
            {
                error = text.Get("settings.error_not_loaded");
                return false;
            }
            if (DisplayModeDropdown.value < 0 || DisplayModeDropdown.value >= DisplayModes.Length ||
                ResolutionDropdown.value < 0 ||
                ResolutionDropdown.value >= ProductResolution.Supported.Count ||
                PresentationSpeedDropdown.value < 0 ||
                PresentationSpeedDropdown.value >= PresentationSpeeds.Length)
            {
                error = text.Get("settings.error_unsupported");
                return false;
            }
            string locale = localeDropdown == null
                ? sourceSettings.Locale
                : localeDropdown.value >= 0 && localeDropdown.value < Locales.Length
                    ? Locales[localeDropdown.value]
                    : string.Empty;
            int textScalePercent = textScaleDropdown == null
                ? sourceSettings.TextScalePercent
                : textScaleDropdown.value >= 0 &&
                    textScaleDropdown.value < ProductSettings.SupportedTextScalePercents.Count
                    ? ProductSettings.SupportedTextScalePercents[textScaleDropdown.value]
                    : 0;
            if (string.IsNullOrEmpty(locale) || textScalePercent == 0)
            {
                error = text.Get("settings.error_unsupported");
                return false;
            }
            try
            {
                settings = new ProductSettings(
                    ProductSettings.CurrentFormatVersion,
                    DisplayModes[DisplayModeDropdown.value],
                    ProductResolution.Supported[ResolutionDropdown.value],
                    VSyncToggle.isOn,
                    Mathf.RoundToInt(MasterVolumeSlider.value),
                    Mathf.RoundToInt(MusicVolumeSlider.value),
                    Mathf.RoundToInt(SfxVolumeSlider.value),
                    PresentationSpeeds[PresentationSpeedDropdown.value],
                    editingBindings,
                    locale,
                    textScalePercent,
                    highContrastToggle?.isOn ?? sourceSettings.HighContrast,
                    reducedMotionToggle?.isOn ?? sourceSettings.ReducedMotion);
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("Product settings selection was rejected: " +
                    exception.Message);
                error = text.Get("settings.error_unsupported");
                return false;
            }
        }

        private void ConfigureOptions()
        {
            if (displayModeDropdown != null)
            {
                ReplaceOptions(displayModeDropdown, new List<string>
                {
                    text.Get("settings.display_windowed"),
                    text.Get("settings.display_borderless")
                });
            }
            if (resolutionDropdown != null)
            {
                ReplaceOptions(resolutionDropdown, ProductResolution.Supported
                    .Select(resolution => resolution.ToString()).ToList());
            }
            if (presentationSpeedDropdown != null)
            {
                ReplaceOptions(presentationSpeedDropdown, new List<string>
                {
                    text.Get("settings.speed_reduced"),
                    text.Get("settings.speed_normal"),
                    text.Get("settings.speed_fast")
                });
            }
            if (localeDropdown != null)
            {
                ReplaceOptions(localeDropdown, new List<string>
                {
                    text.Get("settings.locale_en"),
                    text.Get("settings.locale_ja")
                });
            }
            if (textScaleDropdown != null)
            {
                ReplaceOptions(textScaleDropdown, ProductSettings.SupportedTextScalePercents
                    .Select(value => text.Get("settings.text_scale_value", value)).ToList());
            }
            ConfigureSlider(masterVolumeSlider);
            ConfigureSlider(musicVolumeSlider);
            ConfigureSlider(sfxVolumeSlider);
        }

        private static void ConfigureSlider(Slider? slider)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
        }

        private void ValidateConfiguration()
        {
            if (generalPanel == null || bindingsPanel == null || generalPageButton == null ||
                bindingsPageButton == null || displayModeDropdown == null ||
                resolutionDropdown == null || vSyncToggle == null ||
                masterVolumeSlider == null || musicVolumeSlider == null ||
                sfxVolumeSlider == null || masterVolumeLabel == null ||
                musicVolumeLabel == null || sfxVolumeLabel == null ||
                presentationSpeedDropdown == null || cancelRebindButton == null ||
                feedbackLabel == null || applyButton == null || resetButton == null ||
                backButton == null || keyboardBindingButtons.Length != Commands.Length ||
                gamepadBindingButtons.Length != Commands.Length ||
                keyboardBindingButtons.Any(button => button == null) ||
                gamepadBindingButtons.Any(button => button == null))
                throw new InvalidOperationException(
                    "Product settings screen controls are not configured.");
            bool anyAccessibility = accessibilityPanel != null ||
                accessibilityPageButton != null || localeDropdown != null ||
                textScaleDropdown != null || highContrastToggle != null ||
                reducedMotionToggle != null;
            if (anyAccessibility && (accessibilityPanel == null ||
                accessibilityPageButton == null || localeDropdown == null ||
                textScaleDropdown == null || highContrastToggle == null ||
                reducedMotionToggle == null))
                throw new InvalidOperationException(
                    "Product accessibility settings controls are incomplete.");
        }

        private void ShowGeneralPage()
        {
            if (generalPanel != null) generalPanel.SetActive(true);
            if (bindingsPanel != null) bindingsPanel.SetActive(false);
            if (accessibilityPanel != null) accessibilityPanel.SetActive(false);
        }

        private void ShowBindingsPage()
        {
            if (generalPanel != null) generalPanel.SetActive(false);
            if (bindingsPanel != null) bindingsPanel.SetActive(true);
            if (accessibilityPanel != null) accessibilityPanel.SetActive(false);
        }

        private void ShowAccessibilityPage()
        {
            if (generalPanel != null) generalPanel.SetActive(false);
            if (bindingsPanel != null) bindingsPanel.SetActive(false);
            if (accessibilityPanel != null) accessibilityPanel.SetActive(true);
        }

        private void HandleVolumeChanged(float _) => RefreshVolumeLabels();

        private void RefreshVolumeLabels()
        {
            if (masterVolumeLabel != null && masterVolumeSlider != null)
                masterVolumeLabel.text = text.Get("settings.master_volume_value",
                    Mathf.RoundToInt(masterVolumeSlider.value));
            if (musicVolumeLabel != null && musicVolumeSlider != null)
                musicVolumeLabel.text = text.Get("settings.music_volume_value",
                    Mathf.RoundToInt(musicVolumeSlider.value));
            if (sfxVolumeLabel != null && sfxVolumeSlider != null)
                sfxVolumeLabel.text = text.Get("settings.sfx_volume_value",
                    Mathf.RoundToInt(sfxVolumeSlider.value));
        }

        private void RefreshBindingLabels()
        {
            for (int index = 0; index < Commands.Length; index++)
            {
                SetButtonLabel(keyboardBindingButtons[index], ProductInputScheme.Keyboard,
                    Commands[index], editingBindings.Get(
                        ProductInputScheme.Keyboard, Commands[index]));
                SetButtonLabel(gamepadBindingButtons[index], ProductInputScheme.Gamepad,
                    Commands[index], editingBindings.Get(
                        ProductInputScheme.Gamepad, Commands[index]));
            }
        }

        private void SetButtonLabel(Button button, ProductInputScheme scheme,
            ProductInputCommand command, string path)
        {
            Text? label = button.GetComponentInChildren<Text>(true);
            if (label == null) throw new InvalidOperationException(
                "A binding button requires a Text label.");
            string commandLabel = text.Get(CommandKey(command));
            bool defaultKeyboardSubmit = scheme == ProductInputScheme.Keyboard &&
                command == ProductInputCommand.Submit && string.Equals(path,
                    ProductInputBindings.Default.Get(scheme, command),
                    StringComparison.OrdinalIgnoreCase);
            label.text = defaultKeyboardSubmit
                ? text.Get("settings.binding_keyboard_submit_default", commandLabel)
                : text.Get("settings.binding_label", commandLabel,
                    ProductInputController.CanonicalControlToken(path));
        }

        private static string CommandKey(ProductInputCommand command)
        {
            switch (command)
            {
                case ProductInputCommand.Up: return "settings.command_up";
                case ProductInputCommand.Down: return "settings.command_down";
                case ProductInputCommand.Left: return "settings.command_left";
                case ProductInputCommand.Right: return "settings.command_right";
                case ProductInputCommand.Submit: return "settings.command_submit";
                case ProductInputCommand.Cancel: return "settings.command_back";
                case ProductInputCommand.Help: return "settings.command_help";
                default: throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        private void HandleApply()
        {
            if (!TryReadSettings(out ProductSettings? settings, out string error) || settings == null)
            {
                SetFeedback(error, isError: true);
                ValidationRejected?.Invoke();
                return;
            }
            ApplyRequested?.Invoke(settings);
        }

        private void HandleReset() => ResetRequested?.Invoke();
        private void HandleBack() => BackRequested?.Invoke();
        private void HandleCancelRebind() => CancelRebindRequested?.Invoke();

        private void HandleRebind(ProductInputScheme scheme, ProductInputCommand command) =>
            RebindRequested?.Invoke(scheme, command);

        private static void ReplaceOptions(Dropdown dropdown, List<string> options)
        {
            int selected = dropdown.value;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            if (options.Count > 0)
                dropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, options.Count - 1));
            dropdown.RefreshShownValue();
        }

        private void RefreshStaticButtonLabels()
        {
            SetButtonText(generalPageButton, "settings.tab_general");
            SetButtonText(bindingsPageButton, "settings.tab_bindings");
            SetButtonText(accessibilityPageButton, "settings.tab_accessibility");
            SetButtonText(cancelRebindButton, "settings.cancel_rebind");
            SetButtonText(applyButton, "common.apply");
            SetButtonText(resetButton, "common.reset_defaults");
            SetButtonText(backButton, "common.back");
        }

        private void SetButtonText(Button? button, string key)
        {
            Text? label = button?.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text.Get(key);
        }

        private static int IndexOf<T>(IReadOnlyList<T> values, T value)
        {
            for (int index = 0; index < values.Count; index++)
                if (EqualityComparer<T>.Default.Equals(values[index], value)) return index;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported setting value.");
        }

        private static T Require<T>(T? value, string name) where T : class => value ??
            throw new InvalidOperationException(name + " control is not configured.");
    }
}
