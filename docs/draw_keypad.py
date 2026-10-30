"""Draws the pictures in the docs: the keypad flow, what a key can say, and the status key walk.

Run it after changing what the keys show: python3 docs/draw_keypad.py
The example data is consistent across the pictures: the status key counts what the pages show.
"""
import html
import os

RED, ACCENT, YELLOW, GREEN, TEAL = "#ff8080", "#f36a1b", "#ffc799", "#99ffe4", "#66ddcc"
OVERLAY, SUBTEXT, GROUND, TEXT = "#5c5c5c", "#afb6c2", "#24282d", "#ebf0ec"
LABEL = "#8b949e"
K, G = 70, 8          # key size and gap
HERE = os.path.dirname(os.path.abspath(__file__))


class Picture:
    def __init__(self, title):
        self.title, self.parts = title, []

    def key(self, x, y, title, sub=None, accent=OVERLAY, style="normal", framed=False, empty=False):
        p = self.parts
        if empty:
            p.append(f'<rect x="{x}" y="{y}" width="{K}" height="{K}" rx="8" fill="none" stroke="{LABEL}" '
                     f'stroke-width="1" stroke-dasharray="4 4" opacity="0.5"/>')
            return
        ground = accent if style == "highlight" else GROUND
        ink = GROUND if style == "highlight" else (OVERLAY if style == "dim" else TEXT)
        subink = GROUND if style == "highlight" else (OVERLAY if style == "dim" else accent)
        p.append(f'<rect x="{x}" y="{y}" width="{K}" height="{K}" rx="8" fill="{ground}"/>')
        if style != "highlight":
            p.append(f'<rect x="{x}" y="{y}" width="{K}" height="6" rx="3" fill="{OVERLAY if style == "dim" else accent}"/>')
        if framed:
            p.append(f'<rect x="{x + 1.5}" y="{y + 1.5}" width="{K - 3}" height="{K - 3}" rx="7" fill="none" stroke="{TEXT}" stroke-width="3"/>')
        lines = title.split("\n")
        subs = sub.split("\n") if sub else []
        ty = y + (24 if subs else 30) - (len(lines) - 1) * 6
        for i, line in enumerate(lines):
            p.append(f'<text x="{x + K / 2}" y="{ty + i * 13}" text-anchor="middle" font-size="11" font-weight="600" '
                     f'fill="{ink}">{html.escape(line)}</text>')
        for i, line in enumerate(subs):
            sy = y + K - 12 - (len(subs) - 1 - i) * 12
            p.append(f'<text x="{x + K / 2}" y="{sy}" text-anchor="middle" font-size="9.5" fill="{subink}">{html.escape(line)}</text>')

    def grid(self, gx, gy, keys):
        for i, k in enumerate(keys):
            self.key(gx + (i % 3) * (K + G), gy + (i // 3) * (K + G), **k)

    def text(self, x, y, text, size=13, weight="600", anchor="start", color=LABEL):
        for i, line in enumerate(text.split("\n")):
            self.parts.append(f'<text x="{x}" y="{y + i * (size + 3)}" font-size="{size}" font-weight="{weight}" '
                              f'fill="{color}" text-anchor="{anchor}">{html.escape(line)}</text>')

    def arrow(self, x1, x2, y, word=None):
        self.parts.append(f'<line x1="{x1}" y1="{y}" x2="{x2}" y2="{y}" stroke="{LABEL}" stroke-width="1.5" marker-end="url(#a)"/>')
        if word:
            self.text((x1 + x2) / 2, y - 8, word, 11, "400", "middle")

    def legend(self, x, y, rows):
        for row in rows:
            cx = x
            for color, text in row:
                if color:
                    self.parts.append(f'<rect x="{cx}" y="{y - 10}" width="12" height="12" rx="3" fill="{color}"/>')
                else:
                    self.parts.append(f'<rect x="{cx}" y="{y - 11}" width="14" height="14" rx="3" fill="none" stroke="{LABEL}" stroke-width="2.5"/>')
                self.text(cx + 20, y, text, 12, "400")
                cx += 20 + len(text) * 6.6 + 34
            y += 22
        return y

    def save(self, name, width, height):
        svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif">
<title>{html.escape(self.title)}</title>
<defs><marker id="a" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M2 1L8 5L2 9" fill="none" stroke="{LABEL}" stroke-width="1.5" stroke-linecap="round"/></marker></defs>
{chr(10).join(self.parts)}
</svg>
'''
        path = os.path.join(HERE, name)
        open(path, "w").write(svg)
        print(f"wrote {path}")


EMPTY = dict(title="", empty=True)


def flow():
    """The keypad from your own page down to one issue, and what a key shows after a press."""
    pic, top = Picture("The Herdr folder on the MX Keypad"), 70
    step = 3 * K + 2 * G + 44   # one page of keys plus the gap for an arrow

    # 0. Your own page in Logi Options+: the Herdr folder key opens the folder.
    x0 = 20
    pic.text(x0, 40, "0. Your page")
    pic.text(x0, 56, "your own apps and keys in Logi Options+", 11, "400")
    pic.grid(x0, top, [
        dict(title="Spotify", sub="app", accent=OVERLAY),
        dict(title="Claude", sub="app", accent=OVERLAY),
        dict(title="2 need you", sub="1 ready", accent=RED, style="highlight"),
        dict(title="ChatGPT", sub="app", accent=OVERLAY),
        EMPTY,
        dict(title="Herdr", sub="opens the\nfolder", accent=ACCENT),
        EMPTY, EMPTY, EMPTY,
    ])
    pic.text(x0, top + 3 * (K + G) + 14, "The red key is Herdr status: it jumps\nstraight to the most urgent issue (step 3).", 11, "400")

    # 1. Projects.
    px = x0 + step
    pic.text(px, 40, "1. Projects")
    pic.text(px, 56, "one key per workspace", 11, "400")
    pic.grid(px, top, [
        dict(title="project A", sub="1 waiting,\n1 go?", accent=RED, framed=True),
        dict(title="project B", sub="1 working", accent=YELLOW),
        dict(title="project C", sub="idle", accent=OVERLAY),
        EMPTY, EMPTY, EMPTY, EMPTY, EMPTY,
        dict(title="back", sub="closes", accent=OVERLAY),
    ])

    # 2. A project: its main tab and its issues.
    qx = px + step
    pic.text(qx, 40, "2. A project")
    pic.text(qx, 56, "main tab, then issues", 11, "400")
    pic.grid(qx, top, [
        dict(title="main", sub="idle", accent=OVERLAY),
        dict(title="3 sign up", sub="ready", accent=TEAL),
        dict(title="4 search", sub="working", accent=YELLOW),
        dict(title="5 export", sub="idle", accent=OVERLAY),
        dict(title="6 dark\nmode", sub="waiting", accent=RED),
        dict(title="7 checkout", sub="go?", accent=ACCENT, framed=True),
        dict(title="8 settings", sub="idle", accent=OVERLAY),
        dict(title="meer", sub="1/2", accent=OVERLAY),
        dict(title="terug", sub="projecten", accent=OVERLAY),
    ])

    # 3. An issue: the action that fits lights up.
    tx = qx + step
    pic.text(tx, 40, "3. Issue: 7 checkout")
    pic.text(tx, 56, "the action that fits lights up", 11, "400")
    pic.grid(tx, top, [
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

    # What a key shows for a moment after a press, under the issue page.
    fy = top + 3 * (K + G) + 30
    pic.text(tx, fy, "After a press, for 1.5 seconds")
    for i, (title, color) in enumerate([("sent", GREEN), ("waits on\nyou", RED), ("not now", OVERLAY)]):
        pic.key(tx + i * (K + G), fy + 12, title, None, color, "highlight")

    mid = top + K + G + K / 2
    for x in (x0, px, qx):
        pic.arrow(x + 3 * K + 2 * G + 6, x + step - 8, mid)

    y = pic.legend(20, fy + 12 + K + 34, [
        [(RED, "needs you"), (ACCENT, "your turn"), (TEAL, "ready to merge"), (YELLOW, "working"), (OVERLAY, "quiet"),
         (None, "white frame: what is on screen")],
    ])
    pic.save("keypad.svg", tx + 3 * K + 2 * G + 20, y + 4)


def statuses():
    """Every word a key can show, with what it means."""
    pic, top = Picture("What a key can say"), 64
    slot = 118
    groups = [
        ("The agent", [
            ("waiting", RED, "asks you\nsomething"),
            ("go?", ACCENT, "a plan waits\nfor your go"),
            ("working", YELLOW, "is busy"),
            ("done", ACCENT, "finished\nits turn"),
        ]),
        ("The pull request", [
            ("CI failed", RED, "checks fail"),
            ("changes", RED, "changes\nrequested"),
            ("review", SUBTEXT, "waits on\na review"),
            ("ready", TEAL, "approved,\nyours to merge"),
        ]),
    ]
    x0 = 20
    for name, keys in groups:
        pic.text(x0, 34, name)
        for i, (word, color, meaning) in enumerate(keys):
            x = x0 + i * slot
            pic.key(x, top, "7 checkout", word, color)
            pic.text(x + K / 2, top + K + 20, meaning, 11, "400", "middle")
        x0 += 4 * slot + 30
    pic.text(20, top + K + 70, "The agent wins while it asks or works; after that the pull request speaks.", 12, "400")
    pic.save("statuses.svg", 4 * slot * 2 + 50, top + K + 84)


def status_key():
    """Pressing the status key walks through what needs you, most urgent first."""
    pic, top = Picture("Walking through what needs you"), 46
    steps = [
        ("2 need you", "1 ready", RED, "what needs you"),
        ("1 of 2", "6 dark mode", RED, "the most urgent:\nherdr and the folder\njump to it"),
        ("2 of 2", "7 checkout", ACCENT, "press again within\n10 seconds: the next"),
        ("1 need you", "1 ready", ACCENT, "an issue you handled\ndrops out of the list"),
    ]
    gap = 150
    for i, (title, sub, color, caption) in enumerate(steps):
        x = 40 + i * gap
        pic.key(x, top, title, sub, color, "highlight")
        pic.text(x + K / 2, top + K + 20, caption, 11, "400", "middle")
        if i < len(steps) - 1:
            pic.arrow(x + K + 10, x + gap - 10, top + K / 2, "press" if i < 2 else "later")
    pic.text(40, 26, "The status key")
    pic.save("status-key.svg", 40 + 3 * gap + K + 60, top + K + 70)


flow()
statuses()
status_key()
