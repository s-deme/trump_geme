using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    internal static class RuleAuditTestSupport
    {
        public static void AssertFixedSeedBatch(int seed, params string[] gameIds)
        {
            for (int index = 0; index < gameIds.Length; index++)
            {
                string gameId = gameIds[index];
                IGame left = BuiltInGames.Registry.Create(gameId, seed: seed + index);
                IGame right = BuiltInGames.Registry.Create(gameId, seed: seed + index);
                GameResult leftResult = PlayWithLegalCpu(left, seed * 100 + index);
                GameResult rightResult = PlayWithLegalCpu(right, seed * 100 + index);

                Assert.That(rightResult.Winners, Is.EqualTo(leftResult.Winners), gameId);
                Assert.That(rightResult.Scores, Is.EqualTo(leftResult.Scores), gameId);
                Assert.That(rightResult.Turns, Is.EqualTo(leftResult.Turns), gameId);
            }
        }

        public static GameResult PlayWithLegalCpu(IGame game, int seed)
        {
            DeterministicRandom random = new DeterministicRandom(seed);
            for (int turn = 0; turn < 200000 && !game.IsTerminal; turn++)
            {
                TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, random);
                Assert.That(game.LegalActions(), Does.Contain(action), game.GameId + " CPU action");
                game.Apply(action);
            }
            Assert.That(game.IsTerminal, Is.True, game.GameId + " fixed-seed completion");
            return game.Result();
        }

        public static void AssertOpeningObservationIsDeterministic(string gameId, int seed)
        {
            IGame left = BuiltInGames.Registry.Create(gameId, seed: seed);
            IGame right = BuiltInGames.Registry.Create(gameId, seed: seed);
            for (int player = 0; player < left.Players; player++)
            {
                Assert.That(right.View(player), Is.EqualTo(left.View(player)), gameId + "/P" + player);
                if (player == left.CurrentPlayer)
                {
                    Assert.That(right.ChooseCpuAction(player, new DeterministicRandom(seed + 1)),
                        Is.EqualTo(left.ChooseCpuAction(player, new DeterministicRandom(seed + 1))), gameId);
                }
            }
        }

        public static void AssertOpeningObservationIgnoresTwoOtherHands(string gameId, int seed)
            => AssertOpeningObservationIgnoresTwoOtherHands(gameId, 0, seed);

        public static void AssertOpeningObservationIgnoresTwoOtherHands(string gameId, int players, int seed)
        {
            IGame left = players == 0 ? BuiltInGames.Registry.Create(gameId, seed: seed) :
                BuiltInGames.Registry.Create(gameId, players: players, seed: seed);
            IGame right = players == 0 ? BuiltInGames.Registry.Create(gameId, seed: seed) :
                BuiltInGames.Registry.Create(gameId, players: players, seed: seed);
            int viewer = left.CurrentPlayer;
            int[] hidden = Enumerable.Range(0, left.Players).Where(player => player != viewer).Take(2).ToArray();
            Assert.That(hidden, Has.Length.EqualTo(2), gameId + " needs two non-viewer hands for this check");
            IList hands = Field<IList>(right, "hands", "Hands");
            IList first = Item(hands, hidden[0]);
            IList second = Item(hands, hidden[1]);
            object card = first[0]!; first[0] = second[0]!; second[0] = card;
            AssertObservationEquivalent(left, right, viewer, gameId);
        }

        public static void AssertOpeningObservationIgnoresTwoOtherHandRanksWithinSuit(string gameId, int seed)
        {
            IGame left = BuiltInGames.Registry.Create(gameId, seed: seed);
            IGame right = BuiltInGames.Registry.Create(gameId, seed: seed);
            int viewer = left.CurrentPlayer;
            int[] hidden = Enumerable.Range(0, left.Players).Where(player => player != viewer).Take(2).ToArray();
            IList hands = Field<IList>(right, "hands", "Hands");
            IList first = Item(hands, hidden[0]);
            IList second = Item(hands, hidden[1]);
            bool swapped = false;
            for (int firstIndex = 0; firstIndex < first.Count && !swapped; firstIndex++)
                for (int secondIndex = 0; secondIndex < second.Count && !swapped; secondIndex++)
                    if (first[firstIndex] is Card firstCard && second[secondIndex] is Card secondCard &&
                        firstCard.Suit == secondCard.Suit && firstCard.Rank != secondCard.Rank)
                    {
                        first[firstIndex] = secondCard; second[secondIndex] = firstCard; swapped = true;
                    }
            Assert.That(swapped, Is.True, gameId + " needs same-suit hidden cards with different ranks");
            AssertObservationEquivalent(left, right, viewer, gameId);
        }

        public static void AssertOpeningObservationIgnoresOpponentHandAndStock(string gameId, int seed)
        {
            IGame left = BuiltInGames.Registry.Create(gameId, seed: seed);
            IGame right = BuiltInGames.Registry.Create(gameId, seed: seed);
            int viewer = left.CurrentPlayer;
            int opponent = Enumerable.Range(0, left.Players).Single(player => player != viewer);
            IList hands = Field<IList>(right, "hands", "Hands");
            IList stock = Field<IList>(right, "stock", "Stock");
            Assert.That(stock.Count, Is.GreaterThan(0), gameId + " hidden stock");
            IList opponentHand = Item(hands, opponent);
            object card = opponentHand[0]!;
            opponentHand[0] = stock[0]!; stock[0] = card;
            AssertObservationEquivalent(left, right, viewer, gameId);
        }

        public static void AssertOpeningObservationIgnoresStockOrder(string gameId, int players, int seed)
        {
            IGame left = BuiltInGames.Registry.Create(gameId, players: players, seed: seed);
            IGame right = BuiltInGames.Registry.Create(gameId, players: players, seed: seed);
            int viewer = left.CurrentPlayer;
            IList stock = Field<IList>(right, "stock", "Stock");
            Assert.That(stock.Count, Is.GreaterThan(1), gameId + " hidden stock order");
            object card = stock[0]!; stock[0] = stock[1]!; stock[1] = card;
            AssertObservationEquivalent(left, right, viewer, gameId);
        }

        public static void AssertOpeningObservationIgnoresHiddenListOrder(string gameId, string fieldName, int seed)
        {
            IGame left = BuiltInGames.Registry.Create(gameId, seed: seed);
            IGame right = BuiltInGames.Registry.Create(gameId, seed: seed);
            int viewer = left.CurrentPlayer;
            IList hidden = Field<IList>(right, fieldName);
            Assert.That(hidden.Count, Is.GreaterThan(1), gameId + " hidden list order");
            object card = hidden[0]!; hidden[0] = hidden[1]!; hidden[1] = card;
            AssertObservationEquivalent(left, right, viewer, gameId);
        }

        public static void AssertOpeningObservationIgnoresPrivateDeckOrder(string gameId, int players, int seed)
        {
            IGame left = BuiltInGames.Registry.Create(gameId, players: players, seed: seed);
            IGame right = BuiltInGames.Registry.Create(gameId, players: players, seed: seed);
            int viewer = left.CurrentPlayer;
            IList decks = Field<IList>(right, "decks", "Decks");
            IList deck = Item(decks, viewer);
            Assert.That(deck.Count, Is.GreaterThan(1), gameId + " hidden private deck order");
            object card = deck[0]!; deck[0] = deck[1]!; deck[1] = card;
            AssertObservationEquivalent(left, right, viewer, gameId);
        }

        public static void AssertOpeningObservationIgnoresOpponentHandAndFaceDownLayout(string gameId, int seed)
        {
            IGame left = BuiltInGames.Registry.Create(gameId, seed: seed);
            IGame right = BuiltInGames.Registry.Create(gameId, seed: seed);
            int viewer = left.CurrentPlayer;
            int opponent = Enumerable.Range(0, left.Players).Single(player => player != viewer);
            IList hands = Field<IList>(right, "hands");
            IList layouts = Field<IList>(right, "layouts");
            IList columns = Item(layouts, opponent);
            IList faceDownThenUp = columns.Cast<object?>().Select(item => item as IList)
                .First(column => column != null && column.Count > 1)!;
            IList opponentHand = Item(hands, opponent);
            object card = opponentHand[0]!;
            opponentHand[0] = faceDownThenUp[0]!; faceDownThenUp[0] = card;
            AssertObservationEquivalent(left, right, viewer, gameId);
        }

        private static void AssertObservationEquivalent(IGame left, IGame right, int viewer, string gameId)
        {
            Assert.That(right.View(viewer), Is.EqualTo(left.View(viewer)), gameId + " View");
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()), gameId + " legal actions");
            Assert.That(right.ChooseCpuAction(viewer, new DeterministicRandom(910000 + viewer)),
                Is.EqualTo(left.ChooseCpuAction(viewer, new DeterministicRandom(910000 + viewer))), gameId + " CPU");
        }

        private static T Field<T>(object source, params string[] names)
        {
            Type? type = source.GetType();
            while (type != null)
            {
                foreach (string name in names)
                {
                    FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null) return (T)field.GetValue(source)!;
                }
                type = type.BaseType;
            }
            Assert.Fail(source.GetType().Name + " missing field " + string.Join("/", names));
            throw new InvalidOperationException();
        }

        private static IList Item(IList items, int index) => items[index] as IList ??
            throw new InvalidOperationException("Expected an IList item at index " + index + ".");
    }
}
