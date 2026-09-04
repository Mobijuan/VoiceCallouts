#!/usr/bin/env python3
"""
Rebuilds VoiceCallouts/Data/cactbot-warnings.json from cactbot's (github.com/OverlayPlugin/cactbot)
current `main` branch.

Cactbot's own per-fight trigger files are TypeScript with a lot of computed/conditional text
(directional callouts, role-dependent responses, etc.) that can't be resolved without actually
running the fight. This script does NOT try to interpret that logic - it only extracts triggers
whose displayed text is a single literal string with no interpolation, tied to a specific ability
id, via conservative regex scanning (not a real TS parser). Anything more complex than that is
silently skipped rather than guessed at.

Known limitation (found by hand while building this): a small number of cactbot triggers use one
ability's cast as a *proxy/counter* for a different, upcoming mechanic (see e.g. the "Localized
Blizzard" trigger in ui/raidboss/data/07-dt/alliance/windurst-third-walk.ts, which actually keys
off repeated "Circumscribed Fire" casts because Blizzard's own castbar is too short to trigger on).
Extracting these still produces a timing-correct instruction, just attributed to the technically-
triggering ability's name rather than the one it's actually about. There's no reliable way to
detect this pattern generically; if you spot a confusingly-labeled callout in game, add a manual
override in VoiceCallouts's settings (manual overrides always take priority over this data).

Usage:
    python sync.py

Requires only the Python standard library. Takes a minute or two (downloads ~430 files).
"""
import concurrent.futures
import json
import os
import re
import sys
import urllib.request
from datetime import datetime, timezone

REPO = 'OverlayPlugin/cactbot'
BRANCH = 'main'
RAW_BASE = f'https://raw.githubusercontent.com/{REPO}/{BRANCH}/'
API_TREE_URL = f'https://api.github.com/repos/{REPO}/git/trees/{BRANCH}?recursive=1'

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_PATH = os.path.join(SCRIPT_DIR, '..', '..', 'VoiceCallouts', 'Data', 'cactbot-warnings.json')
META_PATH = os.path.join(SCRIPT_DIR, '..', '..', 'VoiceCallouts', 'Data', 'cactbot-warnings.meta.json')


def fetch(url):
    with urllib.request.urlopen(url, timeout=30) as resp:
        return resp.read().decode('utf-8')


def fetch_trigger_file_paths():
    tree = json.loads(fetch(API_TREE_URL))
    return [
        e['path'] for e in tree['tree']
        if e['path'].startswith('ui/raidboss/data/') and e['path'].endswith('.ts')
    ], tree['sha']


def fetch_all(paths, max_workers=12):
    results = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=max_workers) as pool:
        futures = {pool.submit(fetch, RAW_BASE + p): p for p in paths}
        for future in concurrent.futures.as_completed(futures):
            path = futures[future]
            try:
                results[path] = future.result()
            except Exception as e:
                print(f'  ! failed to fetch {path}: {e}', file=sys.stderr)
    return results


# ---------- Phase 1: resources/outputs.ts -> {key: english text} ----------

def parse_outputs(text):
    outputs = {}
    pattern = re.compile(r"(?P<key>[A-Za-z0-9_]+)\s*:\s*\{(?P<body>[^{}]*?)\}\s*,", re.DOTALL)
    for m in pattern.finditer(text):
        body = m.group('body')
        en_match = re.search(r"en\s*:\s*'((?:[^'\\]|\\.)*)'", body)
        if not en_match:
            continue
        value = en_match.group(1).replace("\\'", "'")
        if '${' in value:
            continue
        outputs[m.group('key')] = value
    return outputs


# ---------- Phase 2: resources/responses.ts -> {key: english text} ----------

def parse_responses(text, outputs):
    responses = {}
    # Only accept single-line, single-branch forms like:
    #   name: (sev?: Severity) => staticResponse(defaultInfoText(sev), Outputs.bleedAoe),
    pattern = re.compile(
        r"^\s*([A-Za-z0-9_]+)\s*:\s*\([^)]*\)\s*=>\s*staticResponse\([^,]+,\s*Outputs\.([A-Za-z0-9_]+)\)\s*,?\s*$"
    )
    for line in text.splitlines():
        m = pattern.match(line)
        if not m:
            continue
        key, outputs_key = m.group(1), m.group(2)
        if outputs_key in outputs:
            responses[key] = outputs[outputs_key]
    return responses


# ---------- Phase 3: split a trigger file into top-level `triggers: [...]` elements ----------

def split_trigger_objects(text):
    m = re.search(r"\btriggers\s*:\s*\[", text)
    if not m:
        return []

    i = m.end()
    depth = 1
    objects = []
    obj_start = None
    in_str = None
    in_line_comment = False
    in_block_comment = False
    prev = ''

    n = len(text)
    while i < n and depth > 0:
        c = text[i]

        if in_line_comment:
            if c == '\n':
                in_line_comment = False
        elif in_block_comment:
            if prev == '*' and c == '/':
                in_block_comment = False
        elif in_str:
            if c == '\\':
                i += 2
                prev = '\\'
                continue
            if c == in_str:
                in_str = None
        else:
            if c == '/' and i + 1 < n and text[i + 1] == '/':
                in_line_comment = True
            elif c == '/' and i + 1 < n and text[i + 1] == '*':
                in_block_comment = True
            elif c in ("'", '"', '`'):
                in_str = c
            elif c in '{[(':
                if depth == 1 and c == '{' and obj_start is None:
                    obj_start = i
                depth += 1
            elif c in '}])':
                depth -= 1
                if depth == 1 and c == '}' and obj_start is not None:
                    objects.append(text[obj_start:i + 1])
                    obj_start = None

        prev = c
        i += 1

    return objects


# ---------- Phase 4: extract (actionId, text) from one trigger object's source text ----------

HEX_ID_RE = re.compile(r"\bid\s*:\s*'([0-9A-Fa-f]{2,8})'")
HEX_ID_ARRAY_RE = re.compile(r"\bid\s*:\s*\[\s*((?:'[0-9A-Fa-f]{2,8}'\s*,?\s*)+)\]")
TYPE_RE = re.compile(r"\btype\s*:\s*'(\w+)'")
TRIGGER_ID_RE = re.compile(r"^\s*id\s*:\s*'([^']*)'", re.MULTILINE)
NETREGEX_RE = re.compile(r"netRegex\s*:\s*\{([^{}]*)\}", re.DOTALL)
RESPONSE_RE = re.compile(r"\bresponse\s*:\s*Responses\.(\w+)\(")
TEXT_FIELD_RE = re.compile(
    r"\b(alertText|infoText|alarmText)\s*:\s*\([^)]*\)\s*=>\s*output\.(\w+)!?\(\)\s*[,;]?"
)
OUTPUT_STRINGS_BLOCK_RE = re.compile(r"outputStrings\s*:\s*\{(.*)\}\s*,?\s*$", re.DOTALL)


def extract_ability_ids(trigger_text):
    netregex_match = NETREGEX_RE.search(trigger_text)
    scope = netregex_match.group(1) if netregex_match else trigger_text

    array_match = HEX_ID_ARRAY_RE.search(scope)
    if array_match:
        return re.findall(r"'([0-9A-Fa-f]{2,8})'", array_match.group(1))

    single = HEX_ID_RE.search(scope)
    return [single.group(1)] if single else []


def resolve_output_strings_key(trigger_text, key, outputs):
    os_match = OUTPUT_STRINGS_BLOCK_RE.search(trigger_text)
    if not os_match:
        return None
    block = os_match.group(1)

    entry_match = re.search(re.escape(key) + r"\s*:\s*\{([^{}]*?)\}", block, re.DOTALL)
    if entry_match:
        en_match = re.search(r"en\s*:\s*'((?:[^'\\]|\\.)*)'", entry_match.group(1))
        if en_match:
            text = en_match.group(1).replace("\\'", "'")
            return None if '${' in text else text

    alias_match = re.search(re.escape(key) + r"\s*:\s*Outputs\.(\w+)\s*,", block)
    if alias_match:
        return outputs.get(alias_match.group(1))

    return None


def extract_trigger(trigger_text, outputs, responses):
    type_match = TYPE_RE.search(trigger_text)
    if not type_match or type_match.group(1) not in ('StartsUsing', 'Ability'):
        return None

    ability_ids = extract_ability_ids(trigger_text)
    if not ability_ids:
        return None

    text = None
    response_match = RESPONSE_RE.search(trigger_text)
    if response_match:
        text = responses.get(response_match.group(1))

    if text is None:
        field_matches = TEXT_FIELD_RE.findall(trigger_text)
        if len(field_matches) == 1:
            _, key = field_matches[0]
            text = resolve_output_strings_key(trigger_text, key, outputs)

    if text is None or not text.strip():
        return None

    trigger_id_match = TRIGGER_ID_RE.search(trigger_text)
    return {
        'ability_ids': [int(h, 16) for h in ability_ids],
        'text': text.strip(),
        'trigger_id': trigger_id_match.group(1) if trigger_id_match else None,
    }


def main():
    print('Fetching cactbot trigger file list...', file=sys.stderr)
    paths, commit_sha = fetch_trigger_file_paths()
    print(f'{len(paths)} trigger files to sync.', file=sys.stderr)

    print('Fetching resources/outputs.ts and resources/responses.ts...', file=sys.stderr)
    outputs = parse_outputs(fetch(RAW_BASE + 'resources/outputs.ts'))
    responses = parse_responses(fetch(RAW_BASE + 'resources/responses.ts'), outputs)
    print(f'  {len(outputs)} shared output strings, {len(responses)} static response helpers', file=sys.stderr)

    print('Downloading trigger files (parallel)...', file=sys.stderr)
    files = fetch_all(paths)
    print(f'  downloaded {len(files)}/{len(paths)}', file=sys.stderr)

    rows = []
    for path, text in files.items():
        zone_match = re.search(r"^\s*id\s*:\s*'([^']*)'", text, re.MULTILINE)
        fight_label = zone_match.group(1) if zone_match else path

        for obj in split_trigger_objects(text):
            result = extract_trigger(obj, outputs, responses)
            if result is None:
                continue
            for aid in result['ability_ids']:
                rows.append({
                    'actionId': aid,
                    'text': result['text'],
                    'fight': fight_label,
                    'trigger': result['trigger_id'],
                    'file': path,
                })

    print(f'Extracted {len(rows)} (ability id -> text) rows', file=sys.stderr)

    by_id = {}
    conflicts = 0
    for row in rows:
        aid = row['actionId']
        if aid in by_id:
            if by_id[aid]['text'] != row['text']:
                conflicts += 1
            continue
        by_id[aid] = row

    print(f'{len(by_id)} unique ability ids, {conflicts} conflicting duplicates dropped', file=sys.stderr)

    sorted_rows = sorted(by_id.values(), key=lambda r: r['actionId'])
    os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)
    with open(OUTPUT_PATH, 'w', encoding='utf-8') as f:
        json.dump(sorted_rows, f, indent=2)
    print(f'Wrote {os.path.abspath(OUTPUT_PATH)}', file=sys.stderr)

    meta = {
        'generatedAt': datetime.now(timezone.utc).isoformat(),
        'sourceRepo': REPO,
        'sourceBranch': BRANCH,
        'sourceTreeSha': commit_sha,
        'triggerFilesScanned': len(files),
        'uniqueAbilityIds': len(by_id),
        'conflictingDuplicatesDropped': conflicts,
    }
    with open(META_PATH, 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=2)
    print(f'Wrote {os.path.abspath(META_PATH)}', file=sys.stderr)


if __name__ == '__main__':
    main()
