#nullable enable

using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class GameSettingsScreen : ProductScreen
    {
        [SerializeField] private Text? summaryLabel;
        [SerializeField] private InputField? seedInput;
        [SerializeField] private InputField? wildRankInput;
        [SerializeField] private Dropdown? difficultyDropdown;
        [SerializeField] private Text? validationLabel;
        [SerializeField] private Button? startButton;
        [SerializeField] private Button? howToPlayButton;
        [SerializeField] private Button? backButton;

        public override ScreenId Id => ScreenId.GameSettings;
        public Text SummaryLabel => summaryLabel ?? throw new InvalidOperationException(
            "Settings summary is not configured.");
        public InputField SeedInput => seedInput ?? throw new InvalidOperationException(
            "Seed input is not configured.");
        public InputField WildRankInput => wildRankInput ?? throw new InvalidOperationException(
            "Wild rank input is not configured.");
        public Dropdown DifficultyDropdown => difficultyDropdown ??
            throw new InvalidOperationException("Difficulty dropdown is not configured.");
        public Text ValidationLabel => validationLabel ?? throw new InvalidOperationException(
            "Settings validation label is not configured.");
        public Button HowToPlayButton => howToPlayButton ?? throw new InvalidOperationException(
            "Settings how-to-play button is not configured.");
        public event System.Action<GameStartRequest>? StartRequested;
        public event System.Action? HowToPlayRequested;
        public event System.Action? BackRequested;
        public event System.Action? ValidationRejected;

        public void Configure(Text summary, InputField seed, InputField wildRank,
            Dropdown difficulty, Text validation, Button start, Button howToPlay, Button back)
        {
            summaryLabel = summary;
            seedInput = seed;
            wildRankInput = wildRank;
            difficultyDropdown = difficulty;
            validationLabel = validation;
            startButton = start;
            howToPlayButton = howToPlay;
            backButton = back;
            SetDifficultyOptions(CpuDifficulties.Standard);
        }

        private void Awake()
        {
            if (summaryLabel == null || seedInput == null || wildRankInput == null ||
                difficultyDropdown == null || validationLabel == null ||
                startButton == null || howToPlayButton == null || backButton == null)
                throw new InvalidOperationException("Settings screen controls are not configured.");
            SetDifficultyOptions(SelectedDifficultyId());
            difficultyDropdown.onValueChanged.AddListener(HandleDifficultyChanged);
            startButton.onClick.AddListener(HandleStart);
            howToPlayButton.onClick.AddListener(HandleHowToPlay);
            backButton.onClick.AddListener(HandleBack);
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(HandleStart);
            if (howToPlayButton != null)
                howToPlayButton.onClick.RemoveListener(HandleHowToPlay);
            if (backButton != null) backButton.onClick.RemoveListener(HandleBack);
            if (difficultyDropdown != null)
                difficultyDropdown.onValueChanged.RemoveListener(HandleDifficultyChanged);
        }

        public void SetValues(GameStartRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            SeedInput.text = request.Seed.ToString(CultureInfo.InvariantCulture);
            WildRankInput.text = request.WildRank.ToString(CultureInfo.InvariantCulture);
            SetDifficultyOptions(request.Difficulty);
            ValidationLabel.text = string.Empty;
        }

        public bool TryReadRequest(out GameStartRequest? request, out string error)
            => TryCreateRequest(SeedInput.text, WildRankInput.text,
                SelectedDifficultyId(), out request, out error);

        public static bool TryCreateRequest(string seedText, string wildRankText,
            int difficulty, out GameStartRequest? request, out string error)
        {
            if (!BuiltInGames.Registry.Info("crazy_eights")
                .SupportsCpuDifficulty(difficulty))
            {
                request = null;
                error = "Choose Easy, Standard, or Hard difficulty.";
                return false;
            }
            return TryCreateRequestCore(
                seedText, wildRankText, difficulty, out request, out error);
        }

        public static bool TryCreateRequest(string seedText, string wildRankText,
            out GameStartRequest? request, out string error)
            => TryCreateRequest(seedText, wildRankText, CpuDifficulties.Standard,
                out request, out error);

        private static bool TryCreateRequestCore(string seedText, string wildRankText,
            int difficulty, out GameStartRequest? request, out string error)
        {
            request = null;
            if (!long.TryParse(seedText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out long seed))
            {
                error = "Seed must be a whole number.";
                return false;
            }
            if (!int.TryParse(wildRankText, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int wildRank) ||
                wildRank < 1 || wildRank > 13)
            {
                error = "Wild rank must be from 1 to 13.";
                return false;
            }
            request = new GameStartRequest(seed, wildRank, difficulty);
            error = string.Empty;
            return true;
        }

        private void SetDifficultyOptions(int difficulty)
        {
            CpuDifficultyInfo[] choices = SupportedDifficulties();
            int selected = Array.FindIndex(choices, choice => choice.Id == difficulty);
            if (selected < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(difficulty), difficulty, "Unsupported product difficulty.");
            DifficultyDropdown.ClearOptions();
            DifficultyDropdown.AddOptions(choices.Select(choice => choice.DisplayName).ToList());
            DifficultyDropdown.SetValueWithoutNotify(selected);
            DifficultyDropdown.RefreshShownValue();
            UpdateSummary();
        }

        private int SelectedDifficultyId()
        {
            CpuDifficultyInfo[] choices = SupportedDifficulties();
            int index = difficultyDropdown == null ?
                Array.FindIndex(choices, choice => choice.Id == CpuDifficulties.Standard) :
                difficultyDropdown.value;
            if (index < 0 || index >= choices.Length)
                throw new InvalidOperationException("Difficulty selection is out of range.");
            return choices[index].Id;
        }

        private void HandleDifficultyChanged(int _) => UpdateSummary();

        private void UpdateSummary()
        {
            if (summaryLabel == null) return;
            CpuDifficultyInfo difficulty = CpuDifficulties.Get(SelectedDifficultyId());
            summaryLabel.text = "Crazy Eights  •  Human: Player 1  •  CPU: Player 2  •  " +
                "Difficulty: " + difficulty.DisplayName;
        }

        private static CpuDifficultyInfo[] SupportedDifficulties()
        {
            GameInfo game = BuiltInGames.Registry.Info("crazy_eights");
            return CpuDifficulties.ProductOrder
                .Where(difficulty => game.SupportsCpuDifficulty(difficulty.Id))
                .ToArray();
        }

        private void HandleStart()
        {
            if (!TryReadRequest(out GameStartRequest? request, out string error) || request == null)
            {
                ValidationLabel.text = error;
                ValidationRejected?.Invoke();
                return;
            }
            ValidationLabel.text = string.Empty;
            StartRequested?.Invoke(request);
        }
        private void HandleHowToPlay() => HowToPlayRequested?.Invoke();
        private void HandleBack() => BackRequested?.Invoke();
    }
}
