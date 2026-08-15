using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TrumpLab
{
    public sealed class SessionFormatException : FormatException
    {
        public SessionFormatException(string message) : base(message) { }
        public SessionFormatException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public sealed class SessionIntegrityException : InvalidOperationException
    {
        public SessionIntegrityException(string message) : base(message) { }
    }

    public static class SessionArchiveCodec
    {
        public const int MaximumBytes = 1024 * 1024;
        public const int MaximumActions = 10000;
        public const int MaximumOptions = 64;
        private const string FormatName = "trumplab_session";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(SessionArchive archive)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            ValidateModel(archive);
            byte[] payload = PayloadBytes(archive);
            string digest = Digest(payload);
            var builder = new StringBuilder(payload.Length + 120);
            WriteArchive(builder, archive, digest);
            byte[] encoded = StrictUtf8.GetBytes(builder.ToString());
            if (encoded.Length > MaximumBytes)
                throw new SessionFormatException("Session archive exceeds the byte limit.");
            return encoded;
        }

        public static SessionArchive Decode(byte[] encoded)
        {
            if (encoded == null) throw new ArgumentNullException(nameof(encoded));
            if (encoded.Length == 0 || encoded.Length > MaximumBytes)
                throw new SessionFormatException("Session archive size is invalid.");
            string json;
            try { json = StrictUtf8.GetString(encoded); }
            catch (DecoderFallbackException exception)
            { throw new SessionFormatException("Session archive is not valid UTF-8.", exception); }

            JsonNode root = new JsonParser(json).Parse();
            Dictionary<string, JsonNode> top = Object(root, "archive");
            Exact(top, "archive", "format", "format_version", "rules_version", "game",
                "actions", "integrity");
            if (Text(top["format"], "format") != FormatName)
                throw new SessionFormatException("Session format identifier is invalid.");
            int formatVersion = Integer(top["format_version"], "format_version");
            int rulesVersion = Integer(top["rules_version"], "rules_version");
            if (formatVersion != SessionArchive.CurrentFormatVersion)
                throw new UnsupportedSessionVersionException(
                    "Unsupported session format version: " + formatVersion);
            if (rulesVersion != SessionArchive.CurrentRulesVersion)
                throw new UnsupportedSessionVersionException(
                    "Unsupported session rules version: " + rulesVersion);

            SessionConfiguration configuration = ReadConfiguration(top["game"]);
            List<JsonNode> actionNodes = Array(top["actions"], "actions");
            if (actionNodes.Count > MaximumActions)
                throw new SessionFormatException("Session contains too many actions.");
            SessionActionRecord[] actions = actionNodes.Select(ReadAction).ToArray();
            SessionArchive archive;
            try
            {
                archive = new SessionArchive(configuration, actions, formatVersion, rulesVersion);
                ValidateModel(archive);
            }
            catch (ArgumentException exception)
            {
                throw new SessionFormatException("Session model is invalid.", exception);
            }

            Dictionary<string, JsonNode> integrity = Object(top["integrity"], "integrity");
            Exact(integrity, "integrity", "algorithm", "digest");
            if (Text(integrity["algorithm"], "integrity.algorithm") != "sha256")
                throw new SessionFormatException("Integrity algorithm is unsupported.");
            string expected = Text(integrity["digest"], "integrity.digest");
            if (!IsLowerHexDigest(expected))
                throw new SessionFormatException("Integrity digest is malformed.");
            string actual = Digest(PayloadBytes(archive));
            if (!ConstantTimeEquals(expected, actual))
                throw new SessionIntegrityException("Session archive integrity check failed.");
            return archive;
        }

        private static SessionConfiguration ReadConfiguration(JsonNode node)
        {
            Dictionary<string, JsonNode> game = Object(node, "game");
            Exact(game, "game", "id", "players", "seed", "difficulty",
                "human_players", "options");
            string gameId = Limited(Text(game["id"], "game.id"), 128, "game.id");
            int players = Integer(game["players"], "game.players");
            int difficulty = Integer(game["difficulty"], "game.difficulty");
            string seedText = Text(game["seed"], "game.seed");
            if (!long.TryParse(seedText, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out long seed) ||
                seed.ToString(CultureInfo.InvariantCulture) != seedText)
                throw new SessionFormatException("Seed is not a canonical Int64 string.");
            int[] humans = Array(game["human_players"], "game.human_players")
                .Select((value, index) => Integer(value, "game.human_players[" + index + "]"))
                .ToArray();
            List<JsonNode> optionNodes = Array(game["options"], "game.options");
            if (optionNodes.Count > MaximumOptions)
                throw new SessionFormatException("Session contains too many options.");
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < optionNodes.Count; index++)
            {
                Dictionary<string, JsonNode> option = Object(
                    optionNodes[index], "game.options[" + index + "]");
                Exact(option, "game.options[" + index + "]", "key", "value");
                string key = Limited(Text(option["key"], "option.key"), 128, "option.key");
                string value = Limited(Text(option["value"], "option.value"), 4096, "option.value");
                if (!options.TryAdd(key, value))
                    throw new SessionFormatException("Session contains duplicate option keys.");
            }
            try { return new SessionConfiguration(gameId, players, seed, difficulty, humans, options); }
            catch (ArgumentException exception)
            { throw new SessionFormatException("Session game configuration is invalid.", exception); }
        }

        private static SessionActionRecord ReadAction(JsonNode node)
        {
            Dictionary<string, JsonNode> value = Object(node, "action");
            Exact(value, "action", "actor", "kind", "card", "target", "value",
                "turn_after", "current_player_after", "terminal_after");
            int actor = Integer(value["actor"], "action.actor");
            string kind = Limited(Text(value["kind"], "action.kind"), 128, "action.kind");
            Card? card = ReadCard(value["card"]);
            int? target = NullableInteger(value["target"], "action.target");
            string? actionValue = value["value"].Kind == JsonKind.Null ? null :
                Limited(Text(value["value"], "action.value"), 4096, "action.value");
            try
            {
                return new SessionActionRecord(actor,
                    new Action(kind, card, target, actionValue),
                    Integer(value["turn_after"], "action.turn_after"),
                    Integer(value["current_player_after"], "action.current_player_after"),
                    Boolean(value["terminal_after"], "action.terminal_after"));
            }
            catch (ArgumentException exception)
            { throw new SessionFormatException("Session action is invalid.", exception); }
        }

        private static Card? ReadCard(JsonNode node)
        {
            if (node.Kind == JsonKind.Null) return null;
            Dictionary<string, JsonNode> card = Object(node, "action.card");
            Exact(card, "action.card", "suit", "rank");
            Suit suit;
            switch (Text(card["suit"], "action.card.suit"))
            {
                case "clubs": suit = Suit.Clubs; break;
                case "diamonds": suit = Suit.Diamonds; break;
                case "hearts": suit = Suit.Hearts; break;
                case "spades": suit = Suit.Spades; break;
                default: throw new SessionFormatException("Card suit is invalid.");
            }
            try { return new Card(suit, Integer(card["rank"], "action.card.rank")); }
            catch (ArgumentOutOfRangeException exception)
            { throw new SessionFormatException("Card rank is invalid.", exception); }
        }

        private static void ValidateModel(SessionArchive archive)
        {
            SessionConfiguration configuration = archive.Configuration;
            Limited(configuration.GameId, 128, "game.id");
            if (configuration.Options.Count > MaximumOptions)
                throw new SessionFormatException("Session contains too many options.");
            foreach (KeyValuePair<string, string> option in configuration.Options)
            {
                Limited(option.Key, 128, "option.key");
                Limited(option.Value, 4096, "option.value");
            }
            if (archive.Actions.Count > MaximumActions)
                throw new SessionFormatException("Session contains too many actions.");
            foreach (SessionActionRecord record in archive.Actions)
            {
                Limited(record.Action.Kind, 128, "action.kind");
                if (record.Action.Value != null)
                    Limited(record.Action.Value, 4096, "action.value");
            }
        }

        private static byte[] PayloadBytes(SessionArchive archive)
        {
            var builder = new StringBuilder();
            WriteArchive(builder, archive, digest: null);
            return StrictUtf8.GetBytes(builder.ToString());
        }

        private static void WriteArchive(StringBuilder builder, SessionArchive archive, string? digest)
        {
            builder.Append('{');
            Property(builder, "format"); WriteString(builder, FormatName); builder.Append(',');
            Property(builder, "format_version"); Number(builder, archive.FormatVersion); builder.Append(',');
            Property(builder, "rules_version"); Number(builder, archive.RulesVersion); builder.Append(',');
            Property(builder, "game"); WriteConfiguration(builder, archive.Configuration); builder.Append(',');
            Property(builder, "actions"); builder.Append('[');
            for (int index = 0; index < archive.Actions.Count; index++)
            { if (index != 0) builder.Append(','); WriteAction(builder, archive.Actions[index]); }
            builder.Append(']');
            if (digest != null)
            {
                builder.Append(','); Property(builder, "integrity"); builder.Append('{');
                Property(builder, "algorithm"); WriteString(builder, "sha256"); builder.Append(',');
                Property(builder, "digest"); WriteString(builder, digest); builder.Append('}');
            }
            builder.Append('}');
        }

        private static void WriteConfiguration(StringBuilder builder, SessionConfiguration value)
        {
            builder.Append('{'); Property(builder, "id"); WriteString(builder, value.GameId); builder.Append(',');
            Property(builder, "players"); Number(builder, value.Players); builder.Append(',');
            Property(builder, "seed"); WriteString(builder, value.Seed.ToString(CultureInfo.InvariantCulture)); builder.Append(',');
            Property(builder, "difficulty"); Number(builder, value.Difficulty); builder.Append(',');
            Property(builder, "human_players"); builder.Append('[');
            for (int index = 0; index < value.HumanPlayers.Count; index++)
            { if (index != 0) builder.Append(','); Number(builder, value.HumanPlayers[index]); }
            builder.Append(']').Append(','); Property(builder, "options"); builder.Append('[');
            int optionIndex = 0;
            foreach (KeyValuePair<string, string> option in value.Options)
            {
                if (optionIndex++ != 0) builder.Append(',');
                builder.Append('{'); Property(builder, "key"); WriteString(builder, option.Key); builder.Append(',');
                Property(builder, "value"); WriteString(builder, option.Value); builder.Append('}');
            }
            builder.Append(']').Append('}');
        }

        private static void WriteAction(StringBuilder builder, SessionActionRecord record)
        {
            Action action = record.Action;
            builder.Append('{'); Property(builder, "actor"); Number(builder, record.Actor); builder.Append(',');
            Property(builder, "kind"); WriteString(builder, action.Kind); builder.Append(',');
            Property(builder, "card");
            if (action.Card.HasValue)
            {
                builder.Append('{'); Property(builder, "suit"); WriteString(builder, SuitName(action.Card.Value.Suit));
                builder.Append(','); Property(builder, "rank"); Number(builder, action.Card.Value.Rank); builder.Append('}');
            }
            else builder.Append("null");
            builder.Append(','); Property(builder, "target");
            if (action.Target.HasValue) Number(builder, action.Target.Value); else builder.Append("null");
            builder.Append(','); Property(builder, "value");
            if (action.Value != null) WriteString(builder, action.Value); else builder.Append("null");
            builder.Append(','); Property(builder, "turn_after"); Number(builder, record.TurnAfter); builder.Append(',');
            Property(builder, "current_player_after"); Number(builder, record.CurrentPlayerAfter); builder.Append(',');
            Property(builder, "terminal_after"); builder.Append(record.TerminalAfter ? "true" : "false");
            builder.Append('}');
        }

        private static string SuitName(Suit suit) => suit == Suit.Clubs ? "clubs" :
            suit == Suit.Diamonds ? "diamonds" : suit == Suit.Hearts ? "hearts" : "spades";

        private static void Property(StringBuilder builder, string name)
        { WriteString(builder, name); builder.Append(':'); }

        private static void Number(StringBuilder builder, int value) =>
            builder.Append(value.ToString(CultureInfo.InvariantCulture));

        private static void WriteString(StringBuilder builder, string value)
        {
            ValidateSurrogates(value);
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20) builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }

        private static string Digest(byte[] payload)
        {
            byte[] hash;
            using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(payload);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool ConstantTimeEquals(string left, string right)
        {
            int different = left.Length ^ right.Length;
            int length = Math.Min(left.Length, right.Length);
            for (int index = 0; index < length; index++) different |= left[index] ^ right[index];
            return different == 0;
        }

        private static bool IsLowerHexDigest(string value) => value.Length == 64 &&
            value.All(character => character >= '0' && character <= '9' ||
                character >= 'a' && character <= 'f');

        private static string Limited(string value, int maximum, string name)
        {
            if (value.Length > maximum) throw new SessionFormatException(name + " is too long.");
            ValidateSurrogates(value);
            return value;
        }

        private static void ValidateSurrogates(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                        throw new SessionFormatException("String contains an unpaired surrogate.");
                }
                else if (char.IsLowSurrogate(value[index]))
                    throw new SessionFormatException("String contains an unpaired surrogate.");
            }
        }

        private static Dictionary<string, JsonNode> Object(JsonNode node, string name) =>
            node.Kind == JsonKind.Object ? node.ObjectValue! :
            throw new SessionFormatException(name + " must be an object.");
        private static List<JsonNode> Array(JsonNode node, string name) =>
            node.Kind == JsonKind.Array ? node.ArrayValue! :
            throw new SessionFormatException(name + " must be an array.");
        private static string Text(JsonNode node, string name) =>
            node.Kind == JsonKind.String ? node.TextValue! :
            throw new SessionFormatException(name + " must be a string.");
        private static bool Boolean(JsonNode node, string name) =>
            node.Kind == JsonKind.Boolean ? node.BooleanValue :
            throw new SessionFormatException(name + " must be a boolean.");
        private static int? NullableInteger(JsonNode node, string name) =>
            node.Kind == JsonKind.Null ? (int?)null : Integer(node, name);

        private static int Integer(JsonNode node, string name)
        {
            if (node.Kind != JsonKind.Number || !int.TryParse(node.TextValue,
                    NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) ||
                value.ToString(CultureInfo.InvariantCulture) != node.TextValue)
                throw new SessionFormatException(name + " must be a canonical Int32.");
            return value;
        }

        private static void Exact(Dictionary<string, JsonNode> value, string name,
            params string[] expected)
        {
            if (value.Count != expected.Length || expected.Any(field => !value.ContainsKey(field)))
                throw new SessionFormatException(name + " contains missing or unknown fields.");
        }

        private enum JsonKind { Object, Array, String, Number, Boolean, Null }

        private sealed class JsonNode
        {
            public JsonKind Kind { get; }
            public Dictionary<string, JsonNode>? ObjectValue { get; }
            public List<JsonNode>? ArrayValue { get; }
            public string? TextValue { get; }
            public bool BooleanValue { get; }

            public JsonNode(Dictionary<string, JsonNode> value)
            { Kind = JsonKind.Object; ObjectValue = value; }
            public JsonNode(List<JsonNode> value)
            { Kind = JsonKind.Array; ArrayValue = value; }
            public JsonNode(JsonKind kind, string? text = null, bool boolean = false)
            { Kind = kind; TextValue = text; BooleanValue = boolean; }
        }

        private sealed class JsonParser
        {
            private readonly string text;
            private int position;

            public JsonParser(string text) { this.text = text; }

            public JsonNode Parse()
            {
                SkipWhitespace();
                JsonNode value = Value(0);
                SkipWhitespace();
                if (position != text.Length)
                    throw new SessionFormatException("Session JSON contains trailing content.");
                return value;
            }

            private JsonNode Value(int depth)
            {
                if (depth > 16) throw new SessionFormatException("Session JSON is too deeply nested.");
                if (position >= text.Length) throw new SessionFormatException("Unexpected end of JSON.");
                char current = text[position];
                if (current == '{') return ObjectNode(depth + 1);
                if (current == '[') return ArrayNode(depth + 1);
                if (current == '"') return new JsonNode(JsonKind.String, StringValue());
                if (current == 't') { Literal("true"); return new JsonNode(JsonKind.Boolean, boolean: true); }
                if (current == 'f') { Literal("false"); return new JsonNode(JsonKind.Boolean, boolean: false); }
                if (current == 'n') { Literal("null"); return new JsonNode(JsonKind.Null); }
                if (current == '-' || current >= '0' && current <= '9')
                    return new JsonNode(JsonKind.Number, NumberValue());
                throw new SessionFormatException("Unexpected JSON token.");
            }

            private JsonNode ObjectNode(int depth)
            {
                position++;
                var values = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Take('}')) return new JsonNode(values);
                while (true)
                {
                    if (position >= text.Length || text[position] != '"')
                        throw new SessionFormatException("JSON object property must be a string.");
                    string name = StringValue();
                    SkipWhitespace(); Require(':'); SkipWhitespace();
                    if (!values.TryAdd(name, Value(depth)))
                        throw new SessionFormatException("JSON object contains duplicate fields.");
                    SkipWhitespace();
                    if (Take('}')) break;
                    Require(','); SkipWhitespace();
                }
                return new JsonNode(values);
            }

            private JsonNode ArrayNode(int depth)
            {
                position++;
                var values = new List<JsonNode>();
                SkipWhitespace();
                if (Take(']')) return new JsonNode(values);
                while (true)
                {
                    values.Add(Value(depth));
                    SkipWhitespace();
                    if (Take(']')) break;
                    Require(','); SkipWhitespace();
                }
                return new JsonNode(values);
            }

            private string StringValue()
            {
                Require('"');
                var builder = new StringBuilder();
                while (position < text.Length)
                {
                    char current = text[position++];
                    if (current == '"')
                    {
                        string value = builder.ToString();
                        if (value.Length > 4096)
                            throw new SessionFormatException("JSON string is too long.");
                        ValidateSurrogates(value);
                        return value;
                    }
                    if (current < 0x20)
                        throw new SessionFormatException("JSON string contains a control character.");
                    if (current != '\\') { builder.Append(current); continue; }
                    if (position >= text.Length) throw new SessionFormatException("Invalid JSON escape.");
                    char escaped = text[position++];
                    switch (escaped)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(UnicodeEscape()); break;
                        default: throw new SessionFormatException("Invalid JSON escape.");
                    }
                }
                throw new SessionFormatException("Unterminated JSON string.");
            }

            private char UnicodeEscape()
            {
                if (position + 4 > text.Length)
                    throw new SessionFormatException("Incomplete Unicode escape.");
                int value = 0;
                for (int index = 0; index < 4; index++)
                {
                    char digit = text[position++];
                    int hex = digit >= '0' && digit <= '9' ? digit - '0' :
                        digit >= 'a' && digit <= 'f' ? digit - 'a' + 10 :
                        digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                    if (hex < 0) throw new SessionFormatException("Invalid Unicode escape.");
                    value = value * 16 + hex;
                }
                return (char)value;
            }

            private string NumberValue()
            {
                int start = position;
                if (Take('-') && position >= text.Length)
                    throw new SessionFormatException("Invalid JSON number.");
                if (Take('0'))
                {
                    if (position < text.Length && IsAsciiDigit(text[position]))
                        throw new SessionFormatException("Invalid leading zero in JSON number.");
                }
                else
                {
                    if (position >= text.Length || text[position] < '1' || text[position] > '9')
                        throw new SessionFormatException("Invalid JSON number.");
                    while (position < text.Length && IsAsciiDigit(text[position])) position++;
                }
                if (Take('.'))
                {
                    if (position >= text.Length || !IsAsciiDigit(text[position]))
                        throw new SessionFormatException("Invalid JSON fraction.");
                    while (position < text.Length && IsAsciiDigit(text[position])) position++;
                }
                if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
                {
                    position++;
                    if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++;
                    if (position >= text.Length || !IsAsciiDigit(text[position]))
                        throw new SessionFormatException("Invalid JSON exponent.");
                    while (position < text.Length && IsAsciiDigit(text[position])) position++;
                }
                return text.Substring(start, position - start);
            }

            private void Literal(string value)
            {
                if (position + value.Length > text.Length ||
                    string.CompareOrdinal(text, position, value, 0, value.Length) != 0)
                    throw new SessionFormatException("Invalid JSON literal.");
                position += value.Length;
            }

            private void SkipWhitespace()
            {
                while (position < text.Length && (text[position] == ' ' || text[position] == '\t' ||
                    text[position] == '\r' || text[position] == '\n')) position++;
            }

            private static bool IsAsciiDigit(char value) => value >= '0' && value <= '9';

            private bool Take(char value)
            {
                if (position >= text.Length || text[position] != value) return false;
                position++;
                return true;
            }

            private void Require(char value)
            {
                if (!Take(value)) throw new SessionFormatException("Expected JSON token: " + value);
            }
        }
    }
}
