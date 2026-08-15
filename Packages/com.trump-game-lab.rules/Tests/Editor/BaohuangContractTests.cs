using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TrumpLab.Games;

namespace TrumpLab.Tests
{
    public sealed class BaohuangContractTests
    {
        [Test]
        public void BaohuangDeckHasTheDocumented168CardsAndDealBoundary()
        {
            IReadOnlyDictionary<string, int> profile = BaohuangGame.DeckComposition();
            Assert.That(BaohuangGame.DeckSize, Is.EqualTo(168));
            Assert.That(new[] { "A", "2", "6", "7", "8", "9", "10", "J", "Q", "K" }
                .Sum(rank => profile[rank]), Is.EqualTo(160));
            Assert.That(profile["small_joker"], Is.EqualTo(4));
            Assert.That(profile["big_joker"], Is.EqualTo(4));
            Assert.That(profile["marked_small_joker"], Is.EqualTo(1));
            Assert.That(profile["marked_big_joker"], Is.EqualTo(1));

            IGame game = BuiltInGames.Registry.Create("baohuang", 5, 19);
            string[] allCards = Enumerable.Range(0, 5).SelectMany(player => Hand(game, player)).ToArray();
            Assert.That(allCards, Has.Length.EqualTo(168));
            Assert.That(allCards.Count(card => card.StartsWith("SMALL", StringComparison.Ordinal)), Is.EqualTo(4));
            Assert.That(allCards.Count(card => card.StartsWith("BIG", StringComparison.Ordinal)), Is.EqualTo(4));
            Assert.That(allCards.Count(card => card.StartsWith("SMALL*", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(allCards.Count(card => card.StartsWith("BIG*", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(allCards.Any(card => OrdinaryRank(card) == 3 || OrdinaryRank(card) == 4
                || OrdinaryRank(card) == 5), Is.False);
            Assert.That(HandCounts(game).OrderBy(value => value),
                Is.EqualTo(new[] { 33, 33, 34, 34, 34 }));
        }

        [Test]
        public void BaohuangEmperorCardCanCircleOnceAndThenForcesTheOriginalHolder()
        {
            IGame game = BuiltInGames.Registry.Create("baohuang", 5, 23);
            int original = game.CurrentPlayer;
            Assert.That(Hand(game, original).Any(card => card.StartsWith("BIG*", StringComparison.Ordinal)),
                Is.True);

            for (int pass = 0; pass < 5; pass++)
            {
                Assert.That(game.LegalActions().Select(action => action.Kind),
                    Does.Contain("pass_emperor"));
                game.Apply(new TrumpLab.Action("pass_emperor"));
            }

            Assert.That(game.CurrentPlayer, Is.EqualTo(original));
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EqualTo(new[] { "accept_emperor" }));
            Assert.That(Hand(game, original).Any(card => card.StartsWith("BIG*", StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        public void BaohuangHiddenGuardAndSoloAreVisibleOnlyToTheirHolderUntilRevealed()
        {
            IGame hidden = FindOpening(solo: false);
            int emperor = hidden.CurrentPlayer;
            int guard = Enumerable.Range(0, 5).Single(player =>
                hidden.View(player).Contains("your_role=guard", StringComparison.Ordinal));
            int civilian = Enumerable.Range(0, 5).First(player => player != emperor && player != guard);
            Assert.That(hidden.View(emperor), Does.Contain("guard=hidden"));
            Assert.That(hidden.View(civilian), Does.Contain("guard=hidden"));
            Assert.That(hidden.View(guard), Does.Contain("guard=P" + guard));

            hidden.Apply(new TrumpLab.Action("accept_emperor"));
            while (!hidden.View().Contains("phase=play", StringComparison.Ordinal))
            {
                Assert.That(hidden.LegalActions().Select(action => action.Kind),
                    Is.EqualTo(new[] { "remain_hidden", "declare_allegiance" }));
                hidden.Apply(new TrumpLab.Action("remain_hidden"));
            }
            Assert.That(hidden.View(emperor), Does.Contain("guard=hidden"));
            Assert.That(hidden.View(civilian), Does.Contain("solo=hidden"));

            IGame publicGuard = FindOpening(solo: false);
            publicGuard.Apply(new TrumpLab.Action("accept_emperor"));
            int declaredGuard = -1;
            while (!publicGuard.View().Contains("phase=play", StringComparison.Ordinal))
            {
                bool isGuard = publicGuard.View().Contains("your_role=guard", StringComparison.Ordinal);
                if (isGuard) declaredGuard = publicGuard.CurrentPlayer;
                publicGuard.Apply(new TrumpLab.Action(isGuard ? "declare_allegiance" : "remain_hidden"));
            }
            Assert.That(declaredGuard, Is.GreaterThanOrEqualTo(0));
            foreach (int player in Enumerable.Range(0, 5))
                Assert.That(publicGuard.View(player), Does.Contain("guard=P" + declaredGuard));
            Assert.That(publicGuard.View(), Does.Contain("multiplier=2"));

            IGame hiddenSolo = FindOpening(solo: true);
            int soloEmperor = hiddenSolo.CurrentPlayer;
            int outsider = (soloEmperor + 1) % 5;
            Assert.That(hiddenSolo.View(soloEmperor), Does.Contain("solo=yes"));
            Assert.That(hiddenSolo.View(outsider), Does.Contain("solo=hidden"));
            hiddenSolo.Apply(new TrumpLab.Action("accept_emperor"));
            Assert.That(hiddenSolo.LegalActions().Select(action => action.Kind),
                Is.EqualTo(new[] { "remain_hidden", "declare_solo" }));
            hiddenSolo.Apply(new TrumpLab.Action("remain_hidden"));
            while (!hiddenSolo.View().Contains("phase=play", StringComparison.Ordinal))
                hiddenSolo.Apply(new TrumpLab.Action("remain_hidden"));
            Assert.That(hiddenSolo.View(outsider), Does.Contain("solo=hidden"));

            IGame declaredSolo = FindOpening(solo: true);
            declaredSolo.Apply(new TrumpLab.Action("accept_emperor"));
            declaredSolo.Apply(new TrumpLab.Action("declare_solo"));
            Assert.That(declaredSolo.View((declaredSolo.CurrentPlayer + 1) % 5), Does.Contain("solo=yes"));
            Assert.That(declaredSolo.View(), Does.Contain("multiplier=2"));
        }

        [Test]
        public void BaohuangCivilianDeclarationIsPublicUprisingWithoutLeakingTheGuard()
        {
            IGame game = FindOpening(solo: false);
            int emperor = game.CurrentPlayer;
            game.Apply(new TrumpLab.Action("accept_emperor"));
            int rebel = -1;
            while (!game.View().Contains("phase=play", StringComparison.Ordinal))
            {
                bool civilian = game.View().Contains("your_role=civilian", StringComparison.Ordinal);
                if (civilian && rebel < 0)
                {
                    rebel = game.CurrentPlayer;
                    game.Apply(new TrumpLab.Action("declare_allegiance"));
                }
                else game.Apply(new TrumpLab.Action("remain_hidden"));
            }
            Assert.That(rebel, Is.GreaterThanOrEqualTo(0));
            Assert.That(game.View(emperor), Does.Contain("rebellion=True"));
            Assert.That(game.View(emperor), Does.Contain("P" + rebel + ":revolutionary"));
            Assert.That(game.View(emperor), Does.Contain("guard=hidden"));
            Assert.That(game.View(emperor), Does.Contain("multiplier=2"));
        }

        [Test]
        public void BaohuangGeneratesSingleRankAndJokerCombinationsAndRejectsBadResponses()
        {
            IGame game = BuiltInGames.Registry.Create("baohuang", 5, 42);
            ReachPlay(game);
            IReadOnlyList<TrumpLab.Action> opening = game.LegalActions();
            Assert.That(opening.All(action => action.Kind == "play_combo"), Is.True);
            Assert.That(opening.Any(action => CardCount(action) == 1), Is.True);
            Assert.That(opening.Any(action => CardCount(action) > 1), Is.True);
            Assert.That(opening.Any(action => CardCount(action) >= 2
                && action.Value!.Split(',').Any(IsJoker)
                && action.Value.Split(',').Any(card => !IsJoker(card))), Is.True);

            TrumpLab.Action lead = opening.First(action => CardCount(action) == 2
                && action.Value!.Split(',').All(card => !IsJoker(card)));
            game.Apply(lead);
            Assert.That(game.LegalActions().Where(action => action.Kind == "play_combo")
                .All(action => CardCount(action) == 2), Is.True);
            Assert.Throws<ArgumentException>(() => game.Apply(
                new TrumpLab.Action("play_combo", value: Hand(game, game.CurrentPlayer)[0])));

            Assert.That(BaohuangGame.StrengthsBeat(new[] { 10, 14 }, new[] { 9, 13 }), Is.True);
            Assert.That(BaohuangGame.StrengthsBeat(new[] { 10, 14 }, new[] { 10, 13 }), Is.False);
            Assert.That(BaohuangGame.StrengthsBeat(new[] { 11, 14 }, new[] { 9, 14 }), Is.False);
            Assert.That(BaohuangGame.StrengthsBeat(new[] { 11, 15 }, new[] { 9, 14 }), Is.True);
            Assert.That(BaohuangGame.StrengthsBeat(new[] { 11, 15 }, new[] { 9 }), Is.False);
        }

        [Test]
        public void BaohuangPassIsSoftAndConsecutivePassesResetTheLead()
        {
            bool foundReentry = false;
            for (int seed = 1; seed <= 200 && !foundReentry; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("baohuang", 5, seed);
                ReachPlay(game);
                TrumpLab.Action? six = game.LegalActions().FirstOrDefault(action =>
                    CardCount(action) == 1 && OrdinaryRank(action.Value!) == 6);
                if (!six.HasValue || six.Value.Kind == null) continue;
                game.Apply(six.Value);
                int passer = game.CurrentPlayer;
                game.Apply(new TrumpLab.Action("pass"));
                TrumpLab.Action response = game.LegalActions().FirstOrDefault(action =>
                    action.Kind == "play_combo" && CardCount(action) == 1);
                if (response.Kind == null) continue;
                game.Apply(response);
                int guard = 0;
                while (game.CurrentPlayer != passer && guard++ < 5)
                    game.Apply(new TrumpLab.Action("pass"));
                if (game.CurrentPlayer == passer
                    && game.LegalActions().Any(action => action.Kind == "play_combo"))
                    foundReentry = true;
            }
            Assert.That(foundReentry, Is.True, "A prior pass must not lock a player out of the climb.");

            IGame reset = BuiltInGames.Registry.Create("baohuang", 5, 77);
            ReachPlay(reset);
            int leader = reset.CurrentPlayer;
            reset.Apply(reset.LegalActions().First(action => CardCount(action) == 1));
            for (int count = 0; count < 4; count++) reset.Apply(new TrumpLab.Action("pass"));
            Assert.That(reset.CurrentPlayer, Is.EqualTo(leader));
            Assert.That(reset.View(), Does.Contain("lead=[]"));
            Assert.That(reset.LegalActions().Any(action => action.Kind == "pass"), Is.False);
        }

        [Test]
        public void BaohuangScoreTableCoversNormalUprisingAndSoloOutcomes()
        {
            Assert.That(BaohuangGame.ScoreFinishOrder(new[] { 0, 1, 2, 3, 4 }, 0, 2, false),
                Is.EqualTo(new[] { 4, -2, 2, -2, -2 }));
            Assert.That(BaohuangGame.ScoreFinishOrder(new[] { 0, 1, 2, 3, 4 }, 0, 2, true),
                Is.EqualTo(new[] { 8, -4, 4, -4, -4 }));
            Assert.That(BaohuangGame.ScoreFinishOrder(new[] { 0, 1, 2, 3, 4 }, 0, 0, false),
                Is.EqualTo(new[] { 12, -3, -3, -3, -3 }));
            Assert.That(BaohuangGame.ScoreFinishOrder(new[] { 1, 0, 2, 3, 4 }, 0, 0, false),
                Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));
            Assert.That(BaohuangGame.ScoreFinishOrder(new[] { 1, 2, 0, 3, 4 }, 0, 0, true),
                Is.EqualTo(new[] { -24, 6, 6, 6, 6 }));
        }

        [Test]
        public void BaohuangSecondDealResolvesTeamTributeWithoutReturnCards()
        {
            IGame? selected = null;
            for (int seed = 1; seed <= 30 && selected == null; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("baohuang", 5, seed,
                    new Dictionary<string, string> { ["deals"] = "2" });
                var policy = new DeterministicRandom(8000 + seed);
                int turns = 0;
                while (!game.IsTerminal && !game.View().Contains("phase=tribute", StringComparison.Ordinal)
                    && turns++ < 1000)
                    game.Apply(game.ChooseCpuAction(game.CurrentPlayer, policy));
                if (!game.IsTerminal && game.View().Contains("phase=tribute", StringComparison.Ordinal))
                    selected = game;
            }
            Assert.That(selected, Is.Not.Null);
            int[] before = HandCounts(selected!);
            Assert.That(selected!.LegalActions().Single().Kind, Is.EqualTo("resolve_tribute"));
            selected.Apply(new TrumpLab.Action("resolve_tribute"));
            int[] after = HandCounts(selected);
            int[] delta = after.Zip(before, (right, left) => right - left).OrderBy(value => value).ToArray();
            int[][] valid =
            {
                new[] { -1, -1, -1, 1, 2 }, new[] { -2, -1, 1, 1, 1 },
                new[] { -1, -1, -1, -1, 4 }, new[] { -4, 1, 1, 1, 1 }
            };
            Assert.That(valid.Any(candidate => candidate.SequenceEqual(delta)), Is.True,
                "Tribute must be 2+1 for a two-player royal team or 4 for solo.");
            Assert.That(selected.View(), Does.Contain("phase=emperor_choice"));
            Assert.That(selected.LegalActions().Any(action => action.Kind.Contains("return",
                StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("BroadSimulation")]
        public void BaohuangCpuIgnoresAnotherPlayersHiddenGuardIdentityAndAlwaysStaysLegal()
        {
            IGame left = BuiltInGames.Registry.Create("baohuang", 5, 91);
            IGame right = BuiltInGames.Registry.Create("baohuang", 5, 91);
            ReachPlay(left);
            ReachPlay(right);
            left.Apply(left.LegalActions().First(action => CardCount(action) == 1));
            right.Apply(right.LegalActions().First(action => CardCount(action) == 1));
            int viewer = left.CurrentPlayer;
            int emperor = ParsePlayer(left.View(), "emperor=P");
            int[] hiddenSeats = Enumerable.Range(0, 5).Where(player => player != viewer && player != emperor).ToArray();
            SetPrivateField(left, "guard", hiddenSeats[0]);
            SetPrivateField(right, "guard", hiddenSeats[1]);
            SetPrivateField(left, "solo", false);
            SetPrivateField(right, "solo", false);
            SetPrivateField(left, "guardRevealed", false);
            SetPrivateField(right, "guardRevealed", false);
            Assert.That(left.View(viewer), Is.EqualTo(right.View(viewer)));
            Assert.That(left.ChooseCpuAction(viewer, new DeterministicRandom(1)),
                Is.EqualTo(right.ChooseCpuAction(viewer, new DeterministicRandom(999))));

            foreach (int seed in Enumerable.Range(300, 20))
            {
                IGame game = BuiltInGames.Registry.Create("baohuang", 5, seed,
                    new Dictionary<string, string> { ["deals"] = seed % 2 == 0 ? "1" : "2" });
                var policy = new DeterministicRandom(seed + 90000);
                while (!game.IsTerminal)
                {
                    IReadOnlyList<TrumpLab.Action> legal = game.LegalActions();
                    TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, policy);
                    Assert.That(legal, Does.Contain(action), "seed=" + seed + " view=" + game.View());
                    game.Apply(action);
                }
            }
        }

        [Test]
        public void BaohuangOptionsAreInstanceLocalBoundedAndDeterministic()
        {
            var custom = (BaohuangGame)BuiltInGames.Registry.Create("baohuang", 5, 52,
                new Dictionary<string, string> { ["deals"] = "2", ["sixes_last"] = "true" });
            var normal = (BaohuangGame)BuiltInGames.Registry.Create("baohuang", 5, 52);
            Assert.That(custom.SessionDeals, Is.EqualTo(2));
            Assert.That(custom.SixesLast, Is.True);
            Assert.That(normal.SessionDeals, Is.EqualTo(1));
            Assert.That(normal.SixesLast, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInGames.Registry.Create("baohuang", 5, 1,
                new Dictionary<string, string> { ["deals"] = "0" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInGames.Registry.Create("baohuang", 5, 1,
                new Dictionary<string, string> { ["deals"] = "101" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInGames.Registry.Create("baohuang", 4, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInGames.Registry.Create("baohuang", 6, 1));

            GameResult left = Simulator.RunGame(BuiltInGames.Registry.Create("baohuang", 5, 66), 70066);
            GameResult right = Simulator.RunGame(BuiltInGames.Registry.Create("baohuang", 5, 66), 70066);
            Assert.That(right.Winners, Is.EqualTo(left.Winners));
            Assert.That(right.Scores, Is.EqualTo(left.Scores));
            Assert.That(right.Turns, Is.EqualTo(left.Turns));
        }

        [Test]
        public void BaohuangSixesLastIsAnExplicitOffByDefaultLocalOption()
        {
            bool verified = false;
            for (int seed = 1; seed <= 100 && !verified; seed++)
            {
                IGame normal = BuiltInGames.Registry.Create("baohuang", 5, seed);
                IGame restricted = BuiltInGames.Registry.Create("baohuang", 5, seed,
                    new Dictionary<string, string> { ["sixes_last"] = "true" });
                ReachPlay(normal);
                ReachPlay(restricted);
                bool normalHasSix = normal.LegalActions().Any(action => action.Value!.Split(',')
                    .Any(card => OrdinaryRank(card) == 6));
                if (!normalHasSix) continue;
                Assert.That(restricted.LegalActions().Any(action => action.Value!.Split(',')
                    .Any(card => OrdinaryRank(card) == 6)), Is.False);
                verified = true;
            }
            Assert.That(verified, Is.True);
        }

        private static IGame FindOpening(bool solo)
        {
            for (int seed = 1; seed <= 10000; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("baohuang", 5, seed);
                if (game.View().Contains("solo=" + (solo ? "yes" : "no"), StringComparison.Ordinal))
                    return game;
            }
            throw new AssertionException("No fixed seed found for solo=" + solo);
        }

        private static void ReachPlay(IGame game)
        {
            int guard = 0;
            while (!game.View().Contains("phase=play", StringComparison.Ordinal) && guard++ < 20)
            {
                string phase = game.View().Split(' ')[0];
                string action = phase == "phase=tribute" ? "resolve_tribute"
                    : phase == "phase=emperor_choice" ? "accept_emperor" : "remain_hidden";
                game.Apply(new TrumpLab.Action(action));
            }
            Assert.That(game.View(), Does.Contain("phase=play"));
        }

        private static string[] Hand(IGame game, int player) => game.View(player)
            .Split(new[] { "your hand: " }, StringSplitOptions.None)[1]
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private static int[] HandCounts(IGame game)
        {
            string text = game.View();
            string body = text.Split(new[] { "hand_counts=[" }, StringSplitOptions.None)[1].Split(']')[0];
            return body.Split(',').Select(int.Parse).ToArray();
        }

        private static int CardCount(TrumpLab.Action action) => action.Value!.Split(',').Length;
        private static bool IsJoker(string id) => id.StartsWith("SMALL", StringComparison.Ordinal)
            || id.StartsWith("BIG", StringComparison.Ordinal);

        private static int OrdinaryRank(string id)
        {
            if (IsJoker(id)) return -1;
            string label = id.Split('#')[0];
            label = label.Substring(0, label.Length - 1);
            return label == "A" ? 1 : label == "J" ? 11 : label == "Q" ? 12
                : label == "K" ? 13 : int.Parse(label);
        }

        private static int ParsePlayer(string view, string prefix)
        {
            string tail = view.Split(new[] { prefix }, StringSplitOptions.None)[1];
            return int.Parse(tail.Substring(0, 1));
        }

        private static void SetPrivateField(IGame game, string name, object value)
        {
            FieldInfo? field = game.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field!.SetValue(game, value);
        }
    }
}
