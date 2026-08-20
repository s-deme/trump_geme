#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public interface IProductText
    {
        string RequestedLocale { get; }
        string EffectiveLocale { get; }
        string Get(string key, params object[] args);
    }

    public sealed class ProductTextEntry
    {
        private readonly int[] placeholderIndexes;

        public string Key { get; }
        public string EnglishFallback { get; }
        public string Japanese { get; }
        public IReadOnlyList<int> PlaceholderIndexes { get; }
        public int ArgumentCount { get; }

        public ProductTextEntry(string key, string englishFallback, string japanese)
        {
            Key = ProductTextCatalog.RequireStableKey(key, nameof(key));
            EnglishFallback = Required(englishFallback, nameof(englishFallback));
            Japanese = Required(japanese, nameof(japanese));

            int[] englishSchema = PlaceholderSchema(EnglishFallback);
            int[] japaneseSchema = PlaceholderSchema(Japanese);
            if (!englishSchema.SequenceEqual(japaneseSchema))
                throw new ArgumentException(
                    "Localized templates must use the same placeholder schema.",
                    nameof(japanese));
            placeholderIndexes = englishSchema;
            PlaceholderIndexes = Array.AsReadOnly(placeholderIndexes);
            ArgumentCount = placeholderIndexes.Length == 0
                ? 0
                : placeholderIndexes[placeholderIndexes.Length - 1] + 1;
            for (int index = 0; index < ArgumentCount; index++)
            {
                if (Array.IndexOf(placeholderIndexes, index) < 0)
                    throw new ArgumentException(
                        "Localized template placeholders must be contiguous from zero.",
                        nameof(englishFallback));
            }
        }

        public string Format(string locale, params object[] args)
        {
            object[] supplied = args ?? throw new ArgumentNullException(nameof(args));
            if (supplied.Length != ArgumentCount)
                throw new ArgumentException(
                    "Text key '" + Key + "' requires " + ArgumentCount +
                    " arguments, but received " + supplied.Length + ".", nameof(args));
            for (int index = 0; index < supplied.Length; index++)
                ValidateArgument(supplied[index], index);

            string template = locale switch
            {
                ProductTextCatalog.EnglishLocale => EnglishFallback,
                ProductTextCatalog.JapaneseLocale => Japanese,
                _ => throw new ArgumentException(
                    "Locale must be ja-JP or en-US.", nameof(locale))
            };
            return string.Format(CultureInfo.InvariantCulture, template, supplied);
        }

        private static string Required(string value, string name) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Localized text cannot be empty.", name)
                : value;

        private static void ValidateArgument(object value, int index)
        {
            if (value == null)
                throw new ArgumentNullException("args[" + index + "]");
            if (value is string || value is char || value is bool || value is Enum ||
                value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal ||
                value is DateTime || value is DateTimeOffset || value is TimeSpan ||
                value is Guid)
                return;
            throw new ArgumentException(
                "Localized argument " + index + " has an unsupported type: " +
                value.GetType().FullName + ".", nameof(value));
        }

        private static int[] PlaceholderSchema(string template)
        {
            var indexes = new List<int>();
            for (int index = 0; index < template.Length; index++)
            {
                char current = template[index];
                if (current == '{')
                {
                    if (index + 1 < template.Length && template[index + 1] == '{')
                    {
                        index++;
                        continue;
                    }
                    int close = template.IndexOf('}', index + 1);
                    if (close < 0)
                        throw new ArgumentException("Localized template has an unmatched '{'.");
                    string item = template.Substring(index + 1, close - index - 1);
                    int separator = item.IndexOfAny(new[] { ',', ':' });
                    string indexText = (separator < 0 ? item : item.Substring(0, separator)).Trim();
                    if (!int.TryParse(indexText, NumberStyles.None,
                            CultureInfo.InvariantCulture, out int placeholder) || placeholder < 0)
                        throw new ArgumentException(
                            "Localized template has an invalid placeholder: {" + item + "}.");
                    indexes.Add(placeholder);
                    index = close;
                }
                else if (current == '}')
                {
                    if (index + 1 < template.Length && template[index + 1] == '}')
                    {
                        index++;
                        continue;
                    }
                    throw new ArgumentException("Localized template has an unmatched '}'.");
                }
            }
            indexes.Sort();
            return indexes.ToArray();
        }
    }

    public static class ProductTextCatalog
    {
        public const string EnglishLocale = "en-US";
        public const string JapaneseLocale = "ja-JP";

        private static readonly ProductTextEntry[] Entries =
        {
            E("app.title", "TRUMP GAME LAB", "トランプ・ゲーム・ラボ"),
            E("app.subtitle", "Crazy Eights vertical slice", "クレイジーエイト製品版"),
            E("common.apply", "Apply", "適用"),
            E("common.back", "Back", "戻る"),
            E("common.cancel", "Cancel", "キャンセル"),
            E("common.close", "Close", "閉じる"),
            E("common.cpu", "CPU", "CPU"),
            E("common.help", "Help", "ヘルプ"),
            E("common.none", "None", "なし"),
            E("common.reset_defaults", "Reset defaults", "既定値に戻す"),
            E("common.rules", "Rules", "ルール"),
            E("common.settings", "Settings", "設定"),
            E("common.title", "Title", "タイトル"),
            E("common.you", "You", "あなた"),

            E("accessibility.dropdown_option", "Option: {0}", "選択肢: {0}"),

            E("title.tutorial", "Tutorial", "チュートリアル"),
            E("title.how_to_play", "How to play", "遊び方"),
            E("title.play", "Play", "プレイ"),
            E("title.saved_sessions", "Saved sessions", "保存した対局"),
            E("title.settings", "Settings", "設定"),
            E("title.quit", "Quit", "終了"),

            E("game_settings.title", "GAME SETTINGS", "ゲーム設定"),
            E("game_settings.summary",
                "Crazy Eights  -  Human: Player 1  -  CPU: Player 2  -  Difficulty: {0}",
                "クレイジーエイト  -  あなた: プレイヤー1  -  CPU: プレイヤー2  -  難易度: {0}"),
            E("game_settings.seed", "Seed", "シード"),
            E("game_settings.seed_placeholder", "Whole number", "整数"),
            E("game_settings.wild_rank", "Wild rank (1-13)", "ワイルドのランク (1～13)"),
            E("game_settings.wild_rank_placeholder", "1 to 13", "1～13"),
            E("game_settings.difficulty", "CPU difficulty", "CPU難易度"),
            E("game_settings.difficulty_easy", "Easy", "かんたん"),
            E("game_settings.difficulty_standard", "Standard", "標準"),
            E("game_settings.difficulty_hard", "Hard", "むずかしい"),
            E("game_settings.start", "Start", "開始"),
            E("game_settings.how_to_play", "How to play", "遊び方"),
            E("game_settings.error_difficulty",
                "Choose Easy, Standard, or Hard difficulty.",
                "難易度を「かんたん」「標準」「むずかしい」から選んでください。"),
            E("game_settings.error_seed", "Seed must be a whole number.",
                "シードには整数を入力してください。"),
            E("game_settings.error_wild_rank", "Wild rank must be from 1 to 13.",
                "ワイルドのランクは1から13で入力してください。"),

            E("settings.title", "PRODUCT SETTINGS", "製品設定"),
            E("settings.tab_general", "General", "一般"),
            E("settings.tab_bindings", "Bindings", "キー割り当て"),
            E("settings.tab_accessibility", "Accessibility", "アクセシビリティ"),
            E("settings.display_mode", "Display mode", "表示モード"),
            E("settings.display_windowed", "Windowed", "ウィンドウ"),
            E("settings.display_borderless", "Borderless", "ボーダーレス"),
            E("settings.resolution", "Resolution", "解像度"),
            E("settings.vsync", "VSync (60 Hz)", "垂直同期 (60 Hz)"),
            E("settings.presentation_speed", "Presentation speed", "演出速度"),
            E("settings.speed_reduced", "Reduced", "軽減"),
            E("settings.speed_normal", "Normal", "標準"),
            E("settings.speed_fast", "Fast", "高速"),
            E("settings.master_volume", "Master volume", "全体音量"),
            E("settings.music_volume", "Music volume", "音楽音量"),
            E("settings.sfx_volume", "SFX volume", "効果音音量"),
            E("settings.master_volume_value", "Master {0}%", "全体 {0}%"),
            E("settings.music_volume_value", "Music {0}%", "音楽 {0}%"),
            E("settings.sfx_volume_value", "SFX {0}%", "効果音 {0}%"),
            E("settings.keyboard", "KEYBOARD", "キーボード"),
            E("settings.gamepad", "GAMEPAD", "ゲームパッド"),
            E("settings.command_up", "Up", "上"),
            E("settings.command_down", "Down", "下"),
            E("settings.command_left", "Left", "左"),
            E("settings.command_right", "Right", "右"),
            E("settings.command_submit", "Submit", "決定"),
            E("settings.command_back", "Back", "戻る"),
            E("settings.command_help", "Help", "ヘルプ"),
            E("settings.binding_label", "{0}: {1}", "{0}: {1}"),
            E("settings.binding_keyboard_submit_default", "{0}: Enter / Space",
                "{0}: Enter / Space"),
            E("settings.cancel_rebind", "Cancel rebind", "割り当てをキャンセル"),
            E("settings.locale", "Language", "言語"),
            E("settings.locale_en", "English", "英語"),
            E("settings.locale_ja", "Japanese", "日本語"),
            E("settings.text_scale", "Text size", "文字サイズ"),
            E("settings.text_scale_value", "{0}%", "{0}%"),
            E("settings.high_contrast", "High contrast", "ハイコントラスト"),
            E("settings.reduced_motion", "Reduced motion", "動きを減らす"),
            E("settings.feedback_binding_updated",
                "Binding updated. Choose Apply to save it.",
                "割り当てを変更しました。「適用」で保存してください。"),
            E("settings.feedback_load_defaults",
                "Using safe defaults. Choose Apply to create the settings file.",
                "安全な既定値を使用しています。「適用」で設定ファイルを作成できます。"),
            E("settings.feedback_load_invalid",
                "The settings file is invalid. Safe defaults are active; the original will be preserved when you Apply or Reset.",
                "設定ファイルが不正です。安全な既定値を使用し、「適用」または「既定値に戻す」時に元ファイルを保全します。"),
            E("settings.feedback_applied", "Settings applied and saved.",
                "設定を適用して保存しました。"),
            E("settings.feedback_applied_preserved",
                "Settings applied. The invalid original was preserved.",
                "設定を適用し、不正だった元ファイルを保全しました。"),
            E("settings.feedback_defaults", "Safe defaults restored and saved.",
                "安全な既定値へ戻して保存しました。"),
            E("settings.feedback_defaults_preserved",
                "Safe defaults restored. The invalid original was preserved.",
                "安全な既定値へ戻し、不正だった元ファイルを保全しました。"),
            E("settings.feedback_rebind_prompt",
                "Press a {0} control for {1}, or cancel.",
                "{1}に割り当てる{0}のボタンを押すか、キャンセルしてください。"),
            E("settings.feedback_rebind_busy", "Finish or cancel the current rebind first.",
                "現在の割り当てを完了またはキャンセルしてください。"),
            E("settings.feedback_rebind_cancelled", "Binding change cancelled.",
                "割り当て変更をキャンセルしました。"),
            E("settings.error_not_loaded", "Product settings are not loaded.",
                "製品設定が読み込まれていません。"),
            E("settings.error_unsupported",
                "Choose a supported display and presentation setting.",
                "対応する表示と演出の設定を選んでください。"),
            E("settings.error_binding_invalid", "That binding cannot be used.",
                "その割り当ては使用できません。"),
            E("settings.error_save_failed", "Settings could not be saved.",
                "設定を保存できませんでした。"),
            E("settings.error_defaults_failed", "Defaults could not be saved.",
                "既定値を保存できませんでした。"),
            E("settings.error_input_apply_failed",
                "Settings were saved, but input could not be applied.",
                "設定は保存されましたが、入力設定を適用できませんでした。"),
            E("settings.error_rebind_start_failed", "Rebinding could not start.",
                "割り当て変更を開始できませんでした。"),

            E("library.title", "SAVED SESSIONS", "保存した対局"),
            E("library.empty_option", "No saved sessions", "保存した対局はありません"),
            E("library.slot_option", "{0:yyyy-MM-dd HH:mm} UTC  -  {1}",
                "{0:yyyy-MM-dd HH:mm} UTC  -  {1}"),
            E("library.resume", "Resume", "再開"),
            E("library.replay", "Replay", "リプレイ"),
            E("library.delete", "Delete", "削除"),
            E("library.confirm_delete", "Confirm delete", "削除を確認"),
            E("library.confirm_delete_instruction",
                "Press Confirm delete again to permanently remove this slot.",
                "この保存を完全に削除するには、もう一度「削除を確認」を押してください。"),
            E("library.instruction",
                "Select Resume to continue, Replay to inspect, or Delete twice to remove.",
                "「再開」「リプレイ」を選ぶか、「削除」を2回押して削除します。"),
            E("library.empty", "No saved sessions are available.",
                "利用できる保存対局はありません。"),

            E("match.status_finished", "Game finished  -  Turn {0}", "対局終了  -  ターン {0}"),
            E("match.status_choose_human", "Choose the starter suit  -  Turn {0}",
                "最初のスートを選んでください  -  ターン {0}"),
            E("match.status_choose_cpu", "CPU is choosing the starter suit  -  Turn {0}",
                "CPUが最初のスートを選んでいます  -  ターン {0}"),
            E("match.status_human", "Your turn  -  Turn {0}", "あなたの手番  -  ターン {0}"),
            E("match.status_cpu", "CPU is thinking...  -  Turn {0}",
                "CPUが考えています...  -  ターン {0}"),
            E("match.opponent_hand", "CPU hand: {0}", "CPUの手札: {0}"),
            E("match.stock", "Stock: {0}", "山札: {0}"),
            E("match.discard", "Discard: {0}", "捨て札: {0}"),
            E("match.discard_called", "Discard: {0}  -  Called {1}",
                "捨て札: {0}  -  指定スート {1}"),
            E("match.human_hand", "Your hand: {0}", "あなたの手札: {0}"),
            E("match.action_summary_locked", "Input locked while the CPU acts.",
                "CPUの手番中は入力できません。"),
            E("match.action_summary_one", "[OK] 1 legal action shown  -  Help explains why",
                "[OK] 合法手を1件表示  -  理由はヘルプで確認できます"),
            E("match.action_summary_many", "[OK] {0} legal actions shown  -  Help explains why",
                "[OK] 合法手を{0}件表示  -  理由はヘルプで確認できます"),
            E("match.action_draw", "Draw", "引く"),
            E("match.action_pass", "Pass", "パス"),
            E("match.action_choose_suit", "Choose {0}", "{0}を選ぶ"),
            E("match.action_play", "Play {0}", "{0}を出す"),
            E("match.action_play_called", "Play {0} -> {1}", "{0}を出す -> {1}"),
            E("match.action_button", "{0} {1}\n{2}", "{0} {1}\n{2}"),
            E("match.action_control", "Legal action", "合法手"),
            E("match.marker_expected", "[EXPECTED]", "★"),
            E("match.marker_legal", "[LEGAL]", "✓"),
            E("match.reason_draw", "Draw one card and end your turn; drawing is a legal choice.",
                "カードを1枚引いて手番を終えます。引くことも合法手です。"),
            E("match.reason_pass", "No card can be played and no card can be drawn.",
                "出せるカードがなく、山札からも引けません。"),
            E("match.reason_starter_suit", "The opening 8 lets you set the active suit to {0}.",
                "最初の8で有効なスートを{0}に指定します。"),
            E("match.reason_wild", "An 8 is wild and calls {0} for the next turn.",
                "8はワイルドで、次の手番のスートを{0}に指定します。"),
            E("match.reason_final", "Your final card matches by {0}.",
                "最後のカードが{0}で一致します。"),
            E("match.reason_last", "Matches by {0} and declares your last card.",
                "{0}で一致し、残り1枚を宣言します。"),
            E("match.reason_match", "Matches the discard by {0}.", "捨て札と{0}で一致します。"),
            E("match.same_suit_rank", "same suit and rank", "同じスートとランク"),
            E("match.same_suit", "same {0} suit", "同じ{0}のスート"),
            E("match.same_rank", "same rank {0}", "同じランク {0}"),
            E("match.context_opening", "The opening card is an 8. Choose the suit that starts the match.",
                "最初のカードは8です。対局を始めるスートを選んでください。"),
            E("match.context_rule",
                "Play the active {0} suit, rank {1}, or any 8. You may draw one card and end your turn whenever Draw is shown.",
                "有効な{0}のスート、ランク{1}、または8を出せます。「引く」が表示されている時は1枚引いて手番を終えることもできます。"),
            E("match.context_cpu", "{0}\n\nThe CPU is acting. Your controls remain locked until its move ends.",
                "{0}\n\nCPUの手番です。CPUの行動が終わるまで操作できません。"),
            E("match.context_actions", "{0}\n\nEvery shown action is legal:\n{1}",
                "{0}\n\n表示されている手はすべて合法です:\n{1}"),
            E("match.action_line", "[OK] {0} - {1}", "[OK] {0} - {1}"),
            E("match.action_line_expected", "[EXPECTED] {0}\n{1}", "[練習対象] {0}\n{1}"),
            E("match.action_line_legal", "[LEGAL] {0}\n{1}", "[合法] {0}\n{1}"),
            E("match.help", "Help", "ヘルプ"),
            E("match.rules", "Rules", "ルール"),
            E("match.settings", "Settings", "設定"),
            E("match.close_help", "Close", "閉じる"),

            E("suit.clubs", "Clubs", "クラブ"),
            E("suit.diamonds", "Diamonds", "ダイヤ"),
            E("suit.hearts", "Hearts", "ハート"),
            E("suit.spades", "Spades", "スペード"),
            E("card.rank_ace", "A", "A"),
            E("card.rank_jack", "J", "J"),
            E("card.rank_queen", "Q", "Q"),
            E("card.rank_king", "K", "K"),
            E("card.suit_clubs", "C", "C"),
            E("card.suit_diamonds", "D", "D"),
            E("card.suit_hearts", "H", "H"),
            E("card.suit_spades", "S", "S"),
            E("card.label", "{0}{1}", "{0}{1}"),
            E("card.hidden", "?", "■"),

            E("rules.crazy_eights.objective.title", "Objective", "目的"),
            E("rules.screen_title", "HOW TO PLAY", "遊び方"),
            E("rules.crazy_eights.objective",
                "Be the first player to empty your hand. The screen shows your cards, the CPU hand count, the stock, the discard top, and whose turn it is.",
                "最初に手札をすべて出したプレイヤーの勝ちです。画面には自分のカード、CPUの手札枚数、山札、捨て札の一番上、現在の手番が表示されます。"),
            E("rules.crazy_eights.legal_play.title", "Discard and legal plays", "捨て札と合法手"),
            E("rules.crazy_eights.legal_play",
                "Play a card that matches the discard top by suit or rank. Every action button shown by the game is legal; its label explains why it can be used.",
                "捨て札の一番上とスートまたはランクが一致するカードを出します。表示される行動ボタンはすべて合法で、ラベルに使える理由が表示されます。"),
            E("rules.crazy_eights.draw.title", "Drawing", "カードを引く"),
            E("rules.crazy_eights.draw",
                "You may draw even when you can play. Draw takes one card when available and ends your turn. Pass appears only when no play or draw is possible.",
                "カードを出せる時でも引くことができます。「引く」は可能なら1枚引いて手番を終えます。「パス」は出すことも引くこともできない時だけ表示されます。"),
            E("rules.crazy_eights.wild_suit.title", "Eights and the called suit", "8と指定スート"),
            E("rules.crazy_eights.wild_suit",
                "An 8 is wild. Its action includes the suit you call. The called suit, shown beside the discard, controls suit matching until a non-wild card is played.",
                "8はワイルドです。行動には指定するスートも表示されます。捨て札の横に表示される指定スートは、ワイルドでないカードが出るまでスート一致に使われます。"),
            E("rules.crazy_eights.result.title", "Winning and score details", "勝敗と得点"),
            E("rules.crazy_eights.result",
                "Play your final card to win. The winner receives the total penalty left in the opponent's hand; the opponent receives the negative value. Eights are 50, face cards are 10, and other cards use their rank.",
                "最後のカードを出すと勝ちです。勝者は相手の手札に残ったペナルティの合計を得点し、相手はその負の値になります。8は50、絵札は10、その他はランクの値です。"),
            E("rules.context_read_only", "Crazy Eights rules - Read-only guide",
                "クレイジーエイトのルール - 閲覧専用"),
            E("rules.context_result", "Result details - Reason: {0}", "結果詳細 - 理由: {0}"),
            E("rules.context_match", "{0} - Phase: {1} - Called suit: {2}",
                "{0} - フェーズ: {1} - 指定スート: {2}"),
            E("rules.turn_human", "Your turn", "あなたの手番"),
            E("rules.turn_cpu", "CPU turn", "CPUの手番"),
            E("rules.called_none", "none", "なし"),
            E("rules.reason_empty_hand", "a player emptied their hand",
                "プレイヤーが手札を出し切った"),
            E("rules.phase_finished", "finished", "終了"),
            E("rules.phase_choose_starter", "choose starter suit", "最初のスート選択"),
            E("rules.phase_play", "play", "プレイ"),
            E("rules.result_current",
                "Current result\n{0} - Reason: {1}\nYou: {2:0.##} - CPU: {3:0.##} - Turns: {4}",
                "現在の結果\n{0} - 理由: {1}\nあなた: {2:0.##} - CPU: {3:0.##} - ターン: {4}"),
            E("rules.outcome_you", "You win", "あなたの勝ち"),
            E("rules.outcome_draw", "Draw", "引き分け"),
            E("rules.outcome_cpu", "CPU wins", "CPUの勝ち"),
            E("rules.page_indicator", "Page {0} / {1}", "ページ {0} / {1}"),
            E("rules.previous", "Previous", "前へ"),
            E("rules.next", "Next", "次へ"),
            E("rules.start_tutorial", "Start tutorial", "チュートリアルを開始"),

            E("tutorial.intro.heading", "Meet the table", "テーブルを確認"),
            E("tutorial.intro", "Find your hand, the CPU card count, stock, discard, and turn label.",
                "自分の手札、CPUの枚数、山札、捨て札、手番表示を確認します。"),
            E("tutorial.matching_play.heading", "Match the discard", "捨て札に合わせる"),
            E("tutorial.matching_play", "Choose the highlighted non-wild card that matches suit or rank.",
                "強調されたワイルドでないカードから、スートかランクが一致するものを選びます。"),
            E("tutorial.draw.heading", "Draw and end the turn", "引いて手番を終える"),
            E("tutorial.draw", "Choose the highlighted Draw action. Drawing is allowed even with a play.",
                "強調された「引く」を選びます。出せるカードがあっても引けます。"),
            E("tutorial.wild_suit.heading", "Play an 8 and call a suit", "8を出してスートを指定"),
            E("tutorial.wild_suit", "Choose the highlighted 8 action. Its label includes the called suit.",
                "強調された8の行動を選びます。ラベルには指定スートも表示されます。"),
            E("tutorial.guided_play.heading", "Read the legal actions", "合法手を読む"),
            E("tutorial.guided_play", "Follow the highlighted action and its reason to finish the hand.",
                "強調された行動と理由を確認し、最後まで進めます。"),
            E("tutorial.win.heading", "You emptied your hand", "手札を出し切りました"),
            E("tutorial.win", "Review the winner, score, reason, and turn count, then finish.",
                "勝者、得点、理由、ターン数を確認して終了します。"),
            E("tutorial.progress", "Step {0} / {1}", "ステップ {0} / {1}"),
            E("tutorial.start_button", "Start tutorial", "チュートリアルを開始"),
            E("tutorial.continue_finish", "Finish tutorial", "チュートリアルを終了"),
            E("tutorial.continue_start", "Start guided match", "ガイド対局を開始"),
            E("tutorial.exit", "Exit tutorial", "チュートリアルを終了"),
            E("tutorial.guidance_stale",
                "That action belongs to an older step. Use the currently highlighted action.",
                "その行動は前のステップのものです。現在強調されている行動を選んでください。"),
            E("tutorial.guidance_expected",
                "That action is legal, but this step practices the highlighted action.",
                "その行動も合法ですが、このステップでは強調された行動を練習します。"),
            E("tutorial.guidance_cpu",
                "The CPU uses only its observation and the public table. Please wait.",
                "CPUは観測できる情報と公開されたテーブルだけを使います。お待ちください。"),
            E("tutorial.guidance_default",
                "This guide uses a normal Crazy Eights game with a fixed seed.",
                "このガイドでは固定シードの通常のクレイジーエイトを使います。"),

            E("result.title", "RESULT", "結果"),
            E("result.outcome_win", "You win!", "あなたの勝ち!"),
            E("result.outcome_loss", "CPU wins", "CPUの勝ち"),
            E("result.outcome_draw", "Draw", "引き分け"),
            E("result.score_you", "You: {0:0.##}", "あなた: {0:0.##}"),
            E("result.score_cpu", "CPU: {0:0.##}", "CPU: {0:0.##}"),
            E("result.scores", "{0}  -  {1}", "{0}  -  {1}"),
            E("result.summary", "{0}\n{1}\nReason: {2}  -  Turns: {3}",
                "{0}\n{1}\n理由: {2}  -  ターン: {3}"),
            E("result.reason_empty_hand", "empty hand", "手札を出し切った"),
            E("result.reason_unknown", "match completed", "対局終了"),
            E("result.marker_win", "[WIN]", "[勝利]"),
            E("result.marker_loss", "[LOSS]", "[敗北]"),
            E("result.marker_draw", "[DRAW]", "[引分]"),
            E("result.with_marker", "{0}  {1}", "{0}  {1}"),
            E("result.details", "Result details", "結果詳細"),
            E("result.rematch", "Rematch", "再戦"),

            E("replay.title", "REPLAY", "リプレイ"),
            E("replay.status", "Replayed {0} actions  -  {1}", "{0}手をリプレイ  -  {1}"),
            E("replay.not_finished", "Saved before the match ended.", "対局終了前に保存されました。"),
            E("replay.table", "{0}\n\n{1}     {2}\n\n{3}\n\n{4}",
                "{0}\n\n{1}     {2}\n\n{3}\n\n{4}"),
            E("replay.back", "Back to sessions", "保存一覧へ戻る"),

            E("feedback.navigation", "[MOVE] Focus moved", "[移動] フォーカスを移動"),
            E("feedback.submit", "[OK] Confirmed", "[決定] 確定"),
            E("feedback.reject", "[X] Not available", "[不可] 使用できません"),
            E("feedback.card_play", "[CARD] Card played", "[カード] カードを出しました"),
            E("feedback.draw", "[+] Card drawn", "[引く] カードを引きました"),
            E("feedback.wild_suit", "[SUIT] Suit confirmed", "[スート] スートを確定"),
            E("feedback.cpu_turn", "[CPU] CPU turn", "[CPU] CPUの手番"),
            E("feedback.win", "[WIN] You win", "[勝利] あなたの勝ち"),
            E("feedback.lose", "[LOSS] CPU wins", "[敗北] CPUの勝ち"),
            E("feedback.error", "[!] Error", "[!] エラー"),

            E("error.panel_title", "MATCH STOPPED", "対局を停止しました"),
            E("error.dismiss", "Return to title", "タイトルへ戻る"),
            E("error.return_to_title", "Return to title", "タイトルへ戻る"),
            E("error.gamepad_disconnected",
                "Gamepad disconnected. Continue with keyboard or mouse.",
                "ゲームパッドが切断されました。キーボードまたはマウスで続けられます。"),
            E("error.gamepad_reconnected", "Gamepad reconnected. You can use it again.",
                "ゲームパッドを再接続しました。再び使用できます。"),
            E("error.session_list", "Saved sessions could not be listed safely.",
                "保存した対局を安全に一覧表示できませんでした。"),
            E("error.resume", "The selected save could not be resumed safely.",
                "選択した保存データを安全に再開できませんでした。"),
            E("error.replay", "The selected save could not be replayed safely.",
                "選択した保存データを安全にリプレイできませんでした。"),
            E("error.delete", "The selected save could not be deleted safely.",
                "選択した保存データを安全に削除できませんでした。"),
            E("error.rematch_unavailable", "The rematch settings are unavailable.",
                "再戦の設定を利用できません。"),
            E("error.tutorial_progress_save", "Tutorial progress could not be saved safely.",
                "チュートリアル進捗を安全に保存できませんでした。"),
            E("error.tutorial_stopped", "The tutorial stopped safely.",
                "チュートリアルを安全に停止しました。"),
            E("error.match_stopped", "The match stopped safely.",
                "対局を安全に停止しました。"),
            E("warning.font.japanese_fallback",
                "Japanese text is unavailable because a compatible Windows font was not found. English is being used instead.",
                "対応するWindowsフォントが見つからないため日本語を表示できません。英語を使用します。"),
            E("warning.font.english_incomplete",
                "Some text glyphs are unavailable because no compatible font was found. Basic English is being used.",
                "対応するフォントが見つからないため一部の文字を表示できません。基本的な英語を使用します。")
        };

        private static readonly IReadOnlyDictionary<string, ProductTextEntry> ByKey =
            new ReadOnlyDictionary<string, ProductTextEntry>(Entries.ToDictionary(
                entry => entry.Key, entry => entry, StringComparer.Ordinal));
        private static readonly IReadOnlyList<string> CatalogKeys =
            Array.AsReadOnly(Entries.Select(entry => entry.Key)
                .OrderBy(key => key, StringComparer.Ordinal).ToArray());
        private static readonly IProductText EnglishResolver =
            new CatalogResolver(EnglishLocale);
        private static readonly IProductText JapaneseResolver =
            new CatalogResolver(JapaneseLocale);

        public static IReadOnlyList<ProductTextEntry> All { get; } = Array.AsReadOnly(Entries);
        public static IReadOnlyList<string> Keys => CatalogKeys;
        public static IProductText English => EnglishResolver;

        public static IProductText ForLocale(string locale) => locale switch
        {
            EnglishLocale => EnglishResolver,
            JapaneseLocale => JapaneseResolver,
            _ => throw new ArgumentException("Locale must be ja-JP or en-US.", nameof(locale))
        };

        public static bool Contains(string key) => key != null && ByKey.ContainsKey(key);

        public static ProductTextEntry Entry(string key)
        {
            string required = RequireStableKey(key, nameof(key));
            return ByKey.TryGetValue(required, out ProductTextEntry? entry)
                ? entry
                : throw new KeyNotFoundException("Unknown product text key: " + required);
        }

        public static string RequiredCharacters(string locale)
        {
            IProductText resolver = ForLocale(locale);
            var characters = new SortedSet<char>();
            foreach (ProductTextEntry entry in Entries)
            {
                string value = resolver.Get(entry.Key,
                    Enumerable.Range(0, entry.ArgumentCount)
                        .Select(index => (object)("ARG" + index)).ToArray());
                foreach (char character in value)
                {
                    if (!char.IsControl(character)) characters.Add(character);
                }
            }
            return new string(characters.ToArray());
        }

        public static string RequiredCharactersForAllLocales()
        {
            var characters = new SortedSet<char>(RequiredCharacters(EnglishLocale));
            characters.UnionWith(RequiredCharacters(JapaneseLocale));
            return new string(characters.ToArray());
        }

        public static void Validate()
        {
            if (Entries.Length == 0 || ByKey.Count != Entries.Length)
                throw new InvalidOperationException(
                    "Product text catalog keys must be non-empty and unique.");
            foreach (ProductTextEntry entry in Entries)
            {
                if (English.Get(entry.Key,
                        Enumerable.Range(0, entry.ArgumentCount)
                            .Select(index => (object)("ARG" + index)).ToArray()) == entry.Key)
                    throw new InvalidOperationException(
                        "Product text catalog exposed a raw key: " + entry.Key);
            }
        }

        public static bool TryValidate(out string error)
        {
            try
            {
                Validate();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static string RequireStableKey(string key, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Product text key cannot be empty.", parameterName);
            if (key[0] == '.' || key[key.Length - 1] == '.' || key.Contains("..") ||
                key.Any(character => !(character == '.' || character == '_' ||
                    character == '-' || character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9')))
                throw new ArgumentException(
                    "Product text key must be a stable lowercase identifier.", parameterName);
            return key;
        }

        private static ProductTextEntry E(string key, string english, string japanese) =>
            new ProductTextEntry(key, english, japanese);

        private sealed class CatalogResolver : IProductText
        {
            public CatalogResolver(string locale)
            {
                RequestedLocale = locale;
                EffectiveLocale = locale;
            }

            public string RequestedLocale { get; }
            public string EffectiveLocale { get; }

            public string Get(string key, params object[] args) =>
                Entry(key).Format(EffectiveLocale, args ?? Array.Empty<object>());
        }
    }

    public enum ProductTextContentMode
    {
        Static,
        Dynamic,
        LocaleNeutral
    }

    public interface IProductFontHost
    {
        IReadOnlyList<string> GetInstalledFontNames();
        Font? CreateDynamicFont(string fontName, int fontSize);
        bool HasCharacters(Font font, string characters, int fontSize);
    }

    public sealed class UnityProductFontHost : IProductFontHost
    {
        public IReadOnlyList<string> GetInstalledFontNames()
        {
            string[] names = Font.GetOSInstalledFontNames() ?? Array.Empty<string>();
            return Array.AsReadOnly(names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        public Font? CreateDynamicFont(string fontName, int fontSize)
        {
            if (string.IsNullOrWhiteSpace(fontName))
                throw new ArgumentException("Font name cannot be empty.", nameof(fontName));
            if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
            try
            {
                return Font.CreateDynamicFontFromOSFont(fontName, fontSize);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool HasCharacters(Font font, string characters, int fontSize)
        {
            if (font == null) throw new ArgumentNullException(nameof(font));
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
            try
            {
                font.RequestCharactersInTexture(characters, fontSize, FontStyle.Normal);
                foreach (char character in characters)
                {
                    if (!char.IsControl(character) && !font.HasCharacter(character)) return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

}
