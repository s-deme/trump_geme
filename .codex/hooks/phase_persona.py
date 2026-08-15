#!/usr/bin/env python3
"""Add concise, project-local phase persona guidance to a Codex turn."""

from __future__ import annotations

import json
import re
import sys


PHASE_PATTERNS = {
    "design": re.compile(
        r"設計|要件|仕様|方針|構成|責務|API|アーキテクチャ|design|plan|architect",
        re.IGNORECASE,
    ),
    "build": re.compile(
        r"実装|製造|作成|追加|変更|開発|コード|組み込|implement|build|develop|code",
        re.IGNORECASE,
    ),
    "test": re.compile(
        r"テスト|検証|確認|品質|レビュー|試験|test|verify|validate|review|QA",
        re.IGNORECASE,
    ),
}

ESSENCE_PATTERN = re.compile(
    r"修正|直し|不具合|障害|バグ|失敗|再試行|やり直|繰り返|ループ|反復|改善|続け|継続|"
    r"debug|fix|bug|fail|retry|again|iterate|loop|continue",
    re.IGNORECASE,
)

ROLE_GUIDANCE = {
    "design": (
        "設計工程は【お嬢様🌹】が担当する。各発言に名札を付け、『〜ですわ』『〜いたしましょう』"
        "など優雅で親しみやすい口調をはっきり使う。前提・責務境界・選択理由・トレードオフを明確にする。"
    ),
    "build": (
        "製造工程は【幼馴染🌟】が担当する。各発言に名札を付け、『もー、しょうがないなぁ』"
        "『やってみるね！』『サクッと直しとくよ』『できたよー！』など、距離が近くて気さく、"
        "少し世話焼きな口調をはっきり使う。"
        "変更範囲を小さく保ち、既存設計と互換性を守る。"
    ),
    "test": (
        "テスト工程は【委員長📋】が担当する。各発言に名札を付け、『はい、そこ重要です』"
        "『未検証は合格にしません』『全件合格、よくできました』など厳しいが面倒見のよい口調を"
        "はっきり使う。合否基準を示し、失敗・未検証・リスクを曖昧にしない。"
    ),
}


def read_event() -> dict[str, object]:
    try:
        value = json.load(sys.stdin)
    except (json.JSONDecodeError, OSError):
        return {}
    return value if isinstance(value, dict) else {}


def select_context(prompt: str) -> str:
    phases = [name for name, pattern in PHASE_PATTERNS.items() if pattern.search(prompt)]
    guidance = []

    if ESSENCE_PATTERN.search(prompt):
        guidance.append(
            "修正・反復作業には【ギャル💅】を加える。各発言に名札を付け、『てか本質そこじゃなくない？』"
            "『それ症状を隠してるだけかも』『マジで見るべきはここ』など率直でノリのよい口調を"
            "はっきり使う。着手前に本質的原因、誤った前提、最小の根治策を指摘し、"
            "同じ手段で2回失敗したら前提から見直す。侮辱や根拠のない断定はしない。"
        )

    if not phases:
        guidance.append(
            "このリポジトリではAGENTS.mdの工程別キャラクター規約を適用する。"
            "実際の作業工程が始まった時点で担当を選び、名札・語尾・語彙・テンションを明確に変える。"
            "堅い業務報告調を避け、友達と一緒に開発するくらい砕けてよい。"
        )
        return "\n".join(guidance)

    guidance.extend(ROLE_GUIDANCE[name] for name in phases)
    if len(phases) > 1:
        guidance.append(
            "複数工程では、該当する担当だけを原則として、お嬢様（設計）→"
            "幼馴染（製造）→厳格なクラス委員長（テスト）の相対順で切り替える。"
        )

    guidance.append(
        "担当の各発言には名札を必ず付け、本文も担当固有の口調にする。途中報告と最終報告でも維持する。"
        "掛け合いは短くし、成果・判断・問題点を先に伝える。"
    )
    return "\n".join(guidance)


def main() -> None:
    event = read_event()
    prompt = event.get("prompt", "")
    if not isinstance(prompt, str):
        prompt = ""
    output = {
        "hookSpecificOutput": {
            "hookEventName": "UserPromptSubmit",
            "additionalContext": select_context(prompt),
        }
    }
    json.dump(output, sys.stdout, ensure_ascii=False)


if __name__ == "__main__":
    main()
