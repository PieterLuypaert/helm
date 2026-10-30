"""Draws docs/keypad.svg: the status key, the three folder pages and the press feedback.

Run it after changing the keys: python3 docs/draw_keypad.py
The example data is consistent: the status key counts what the pages show.
"""
import html
import os

RED, ACCENT, YELLOW, GREEN, TEAL = "#ff8080", "#f36a1b", "#ffc799", "#99ffe4", "#66ddcc"
OVERLAY, SUBTEXT, GROUND, TEXT = "#5c5c5c", "#afb6c2", "#24282d", "#ebf0ec"
LABEL = "#8b949e"
K, G = 70, 8          # key size and gap
TOP = 70              # top of the key grids
parts = []


def key(x, y, title, sub=None, accent=OVERLAY, style="normal", framed=False, empty=False):
    if empty:
        parts.append(f'<rect x="{x}" y="{y}" width="{K}" height="{K}" rx="8" fill="none" stroke="{LABEL}" '
                     f'stroke-width="1" stroke-dasharray="4 4" opacity="0.5"/>')
        return
    ground = accent if style == "highlight" else GROUND
    ink = GROUND if style == "highlight" else (OVERLAY if style == "dim" else TEXT)
    subink = GROUND if style == "highlight" else (OVERLAY if style == "dim" else accent)
    parts.append(f'<rect x="{x}" y="{y}" width="{K}" height="{K}" rx="8" fill="{ground}"/>')
    if style != "highlight":
        parts.append(f'<rect x="{x}" y="{y}" width="{K}" height="6" rx="3" fill="{OVERLAY if style == "dim" else accent}"/>')
    if framed:
        parts.append(f'<rect x="{x + 1.5}" y="{y + 1.5}" width="{K - 3}" height="{K - 3}" rx="7" fill="none" stroke="{TEXT}" stroke-width="3"/>')
    lines = title.split("\n")
    subs = sub.split("\n") if sub else []
    ty = y + (24 if subs else 30) - (len(lines) - 1) * 6
    for i, line in enumerate(lines):
        parts.append(f'<text x="{x + K / 2}" y="{ty + i * 13}" text-anchor="middle" font-size="11" font-weight="600" '
                     f'fill="{ink}">{html.escape(line)}</text>')
    for i, line in enumerate(subs):
        sy = y + K - 12 - (len(subs) - 1 - i) * 12
        parts.append(f'<text x="{x + K / 2}" y="{sy}" text-anchor="middle" font-size="9.5" fill="{subink}">{html.escape(line)}</text>')


def grid(gx, keys):
    for i, k in enumerate(keys):
        key(gx + (i % 3) * (K + G), TOP + (i // 3) * (K + G), **k)


def label(x, y, text, size=13, weight="600"):
    parts.append(f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" fill="{LABEL}">{html.escape(text)}</text>')


def arrow(x1, x2, y):
    parts.append(f'<line x1="{x1}" y1="{y}" x2="{x2}" y2="{y}" stroke="{LABEL}" stroke-width="1.5" marker-end="url(#a)"/>')


EMPTY = dict(title="", empty=True)

# The status key, before and during a walk through what needs you.
label(20, 40, "Status key")
label(20, 56, "press again: the next", 11, "400")
key(30, TOP, "3 need you", "1 ready", RED, "highlight")
key(30, TOP + 2 * (K + G), "2 of 3", "7 checkout", ACCENT, "highlight")

# 1. Projects.
px = 150
label(px, 40, "1. Projects")
label(px, 56, "one key per workspace", 11, "400")
grid(px, [
    dict(title="project A", sub="1 waiting,\n1 go?", accent=RED, framed=True),
    dict(title="project B", sub="1 working", accent=YELLOW),
    dict(title="project C", sub="idle", accent=OVERLAY),
    EMPTY, EMPTY, EMPTY, EMPTY, EMPTY,
    dict(title="back", sub="closes", accent=OVERLAY),
])

# 2. A project: its issues, with their agents and pull requests.
qx = 420
label(qx, 40, "2. A project")
label(qx, 56, "main tab, then issues", 11, "400")
grid(qx, [
    dict(title="main", sub="idle", accent=OVERLAY),
    dict(title="3 sign up", sub="ready", accent=TEAL),
    dict(title="4 search", sub="working", accent=YELLOW),
    dict(title="5 export", sub="CI failed", accent=RED),
    dict(title="6 dark\nmode", sub="waiting", accent=RED),
    dict(title="7 checkout", sub="go?", accent=ACCENT, framed=True),
    dict(title="8 settings", sub="review", accent=SUBTEXT),
    dict(title="meer", sub="1/2", accent=OVERLAY),
    dict(title="terug", sub="projecten", accent=OVERLAY),
])

# 3. An issue: the action that fits lights up.
tx = 690
label(tx, 40, "3. Issue: 7 checkout")
label(tx, 56, "the action that fits lights up", 11, "400")
grid(tx, [
    dict(title="7 checkout", sub="go?", accent=ACCENT),
    dict(title="go", accent=ACCENT, style="highlight"),
    dict(title="toestaan", accent=RED, style="dim"),
    dict(title="review\nand fix", accent=OVERLAY),
    dict(title="babysit\npr", accent=OVERLAY),
    dict(title="commit", accent=OVERLAY),
    dict(title="stop", accent=OVERLAY),
    dict(title="open PR", accent=OVERLAY, style="dim"),
    dict(title="terug", sub="project A", accent=OVERLAY),
])

# What a key shows for a moment after a press.
fx = 950
label(fx, 40, "After a press")
label(fx, 56, "for 1.5 seconds", 11, "400")
key(fx, TOP, "sent", None, GREEN, "highlight")
key(fx, TOP + K + G, "waits on\nyou", None, RED, "highlight")
key(fx, TOP + 2 * (K + G), "not now", None, OVERLAY, "highlight")

mid = TOP + K + G + K / 2
arrow(104, 142, mid)
arrow(px + 3 * K + 2 * G + 6, qx - 8, mid)
arrow(qx + 3 * K + 2 * G + 6, tx - 8, mid)

# Legend, in two rows.
rows = [
    [(RED, "waiting, CI failed, changes: needs you"), (ACCENT, "go? or done: your turn"), (TEAL, "ready to merge")],
    [(YELLOW, "working"), (SUBTEXT, "review or draft"), (OVERLAY, "idle"), (None, "white frame: what is on screen")],
]
ly = TOP + 3 * (K + G) + 36
for row in rows:
    x = 20
    for color, text in row:
        if color:
            parts.append(f'<rect x="{x}" y="{ly - 10}" width="12" height="12" rx="3" fill="{color}"/>')
        else:
            parts.append(f'<rect x="{x}" y="{ly - 11}" width="14" height="14" rx="3" fill="none" stroke="{LABEL}" stroke-width="2.5"/>')
        label(x + 20, ly, text, 12, "400")
        x += 20 + len(text) * 6.6 + 34
    ly += 22

W, H = 1040, ly + 4
svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif">
<title>The Herdr folder on the MX Keypad</title>
<defs><marker id="a" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M2 1L8 5L2 9" fill="none" stroke="{LABEL}" stroke-width="1.5" stroke-linecap="round"/></marker></defs>
{chr(10).join(parts)}
</svg>
'''
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "keypad.svg")
open(out, "w").write(svg)
print(f"wrote {out}")
