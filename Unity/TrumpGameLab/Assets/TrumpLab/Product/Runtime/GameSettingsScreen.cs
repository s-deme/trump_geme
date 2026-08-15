#nullable enable

using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class GameSettingsScreen : ProductScreen
    {
        [SerializeField] private Text? summaryLabel;
        [SerializeField] private InputField? seedInput;
        [SerializeField] private InputField? wildRankInput;
        [SerializeField] private Text? validationLabel;
        [SerializeField] private Button? startButton;
        [SerializeField] private Button? backButton;

        public override ScreenId Id => ScreenId.GameSettings;
        public Text SummaryLabel => summaryLabel ?? throw new InvalidOperationException(
            "Settings summary is not configured.");
        public InputField SeedInput => seedInput ?? throw new InvalidOperationException(
            "Seed input is not configured.");
        public InputField WildRankInput => wildRankInput ?? throw new InvalidOperationException(
            "Wild rank input is not configured.");
        public Text ValidationLabel => validationLabel ?? throw new InvalidOperationException(
            "Settings validation label is not configured.");
        public event System.Action<GameStartRequest>? StartRequested;
        public event System.Action? BackRequested;

        public void Configure(Text summary, InputField seed, InputField wildRank,
            Text validation, Button start, Button back)
        {
            summaryLabel = summary;
            seedInput = seed;
            wildRankInput = wildRank;
            validationLabel = validation;
            startButton = start;
            backButton = back;
        }

        private void Awake()
        {
            if (summaryLabel == null || seedInput == null || wildRankInput == null ||
                validationLabel == null || startButton == null || backButton == null)
                throw new InvalidOperationException("Settings screen controls are not configured.");
            startButton.onClick.AddListener(HandleStart);
            backButton.onClick.AddListener(HandleBack);
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(HandleStart);
            if (backButton != null) backButton.onClick.RemoveListener(HandleBack);
        }

        public void SetValues(GameStartRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            SeedInput.text = request.Seed.ToString(CultureInfo.InvariantCulture);
            WildRankInput.text = request.WildRank.ToString(CultureInfo.InvariantCulture);
            ValidationLabel.text = string.Empty;
        }

        public bool TryReadRequest(out GameStartRequest? request, out string error)
            => TryCreateRequest(SeedInput.text, WildRankInput.text, out request, out error);

        public static bool TryCreateRequest(string seedText, string wildRankText,
            out GameStartRequest? request, out string error)
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
            request = new GameStartRequest(seed, wildRank);
            error = string.Empty;
            return true;
        }

        private void HandleStart()
        {
            if (!TryReadRequest(out GameStartRequest? request, out string error) || request == null)
            {
                ValidationLabel.text = error;
                return;
            }
            ValidationLabel.text = string.Empty;
            StartRequested?.Invoke(request);
        }
        private void HandleBack() => BackRequested?.Invoke();
    }
}
