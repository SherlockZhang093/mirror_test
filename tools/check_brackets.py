import sys

files = [
    'Assets/MirrorTrial/Scripts/Level/LevelCommon.cs',
    'Assets/MirrorTrial/Scripts/Level/MirrorTransitionBridge.cs',
    'Assets/MirrorTrial/Scripts/Level/MirrorGate.cs',
    'Assets/MirrorTrial/Scripts/Level/LevelTrigger.cs',
    'Assets/MirrorTrial/Scripts/Level/LevelValidationUtility.cs',
    'Assets/MirrorTrial/Scripts/Level/MirrorReturnOnClear.cs',
    'Assets/MirrorTrial/Scripts/Level/LevelManager.cs',
    'Assets/MirrorTrial/Editor/Level/Level01Setup.cs',
    'Assets/MirrorTrial/Editor/Level/Level01MirrorSetup.cs',
]

def check(src):
    """State machine bracket check, skipping strings/comments/chars."""
    pairs = {'{': '}', '(': ')', '[': ']'}
    stack = []
    i = 0
    n = len(src)
    line = 1
    while i < n:
        c = src[i]
        if c == '\n':
            line += 1
            i += 1
            continue
        # line comment
        if c == '/' and i + 1 < n and src[i+1] == '/':
            while i < n and src[i] != '\n':
                i += 1
            continue
        # block comment
        if c == '/' and i + 1 < n and src[i+1] == '*':
            i += 2
            while i + 1 < n and not (src[i] == '*' and src[i+1] == '/'):
                if src[i] == '\n':
                    line += 1
                i += 1
            i += 2
            continue
        # verbatim string @"..."
        if c == '@' and i + 1 < n and src[i+1] == '"':
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i+1] == '"':
                        i += 2
                        continue
                    else:
                        break
                i += 1
            i += 1
            continue
        # normal string "..."
        if c == '"':
            i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == '"':
                    break
                i += 1
            i += 1
            continue
        # char '...'
        if c == "'":
            i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == "'":
                    break
                i += 1
            i += 1
            continue
        # brackets
        if c in pairs:
            stack.append((c, line))
        elif c in pairs.values():
            if not stack:
                return f"extra closing '{c}' at line {line}"
            top, _ = stack.pop()
            if pairs[top] != c:
                return f"mismatch '{top}' vs '{c}' at line {line}"
        i += 1
    if stack:
        return f"unclosed '{stack[-1][0]}' at line {stack[-1][1]}"
    return None

ok = True
for f in files:
    with open(f, encoding='utf-8', errors='ignore') as fh:
        src = fh.read()
    err = check(src)
    if err:
        print(f'FAIL {f}: {err}')
        ok = False
    else:
        print(f'OK {f}')

sys.exit(0 if ok else 1)
