"""Draws the pictures in the docs: the keypad flow, what a key can say, the status key walk and
answering a question.

Run it after changing what the keys show: python3 docs/draw_keypad.py
Every key is drawn as on the keypad: an icon, with the label Logi writes under it. The icons come
from plugin/src/Helpers/Icons.cs, the same ones the plugin draws, so a picture cannot drift from a key.
The example data is consistent across the pictures: the status key counts what the pages show.
The script also stamps each picture's link in the README and the docs with a hash of its content,
so a changed picture gets a new URL and no browser or GitHub cache shows the old one.
"""
import hashlib
import html
import os
import re

RED, ACCENT, YELLOW, GREEN, BLUE = "#ff8080", "#f36a1b", "#ffc799", "#99ffe4", "#82aaff"
OVERLAY, SUBTEXT, GROUND, TEXT = "#5c5c5c", "#afb6c2", "#24282d", "#ebf0ec"
KEY = "#2b2d30"       # a keypad key
TILE = "#1c1f23"      # a herdr key on your own page
LABEL = "#8b949e"
K, G = 76, 8          # key size and gap
HERE = os.path.dirname(os.path.abspath(__file__))


def load_icons():
    """The icons the plugin draws on its keys, read from plugin/src/Helpers/Icons.cs: one source for both."""
    source = open(os.path.join(os.path.dirname(HERE), "plugin", "src", "Helpers", "Icons.cs")).read()
    icons = {m.group(1): m.group(2).replace('\\"', '"') for m in re.finditer(r'public const String (\w+) = "(.*?)";\n', source)}
    # Stand-ins for your own apps in the pictures, from Tabler Icons as well.
    icons.update(APPS)
    return icons


APPS = {'Music': '<path d="M3 17a3 3 0 1 0 6 0a3 3 0 0 0 -6 0"/><path d="M13 17a3 3 0 1 0 6 0a3 3 0 0 0 -6 0"/><path d="M9 17v-13h10v13"/><path d="M9 8h10"/>', 'Sparkles': '<path d="M16 18a2 2 0 0 1 2 2a2 2 0 0 1 2 -2a2 2 0 0 1 -2 -2a2 2 0 0 1 -2 2zm0 -12a2 2 0 0 1 2 2a2 2 0 0 1 2 -2a2 2 0 0 1 -2 -2a2 2 0 0 1 -2 2zm-7 12a6 6 0 0 1 6 -6a6 6 0 0 1 -6 -6a6 6 0 0 1 -6 6a6 6 0 0 1 6 6z"/>', 'Chatbot': '<path d="M18 4a3 3 0 0 1 3 3v8a3 3 0 0 1 -3 3h-5l-5 3v-3h-2a3 3 0 0 1 -3 -3v-8a3 3 0 0 1 3 -3h12z"/><path d="M9.5 9h.01"/><path d="M14.5 9h.01"/><path d="M9.5 13a3.5 3.5 0 0 0 5 0"/>'}
ICONS = load_icons()


class Picture:
    def __init__(self, title):
        self.title, self.parts = title, []

    def key(self, x, y, label="", icon=None, color=TEXT, style="normal", framed=False, badge=None, empty=False, count=None):
        """A key as the keypad shows it: the icon, and under it the label Logi writes. A tile, on your own
        page, is a big icon or a count over a small icon on dark grey, with a short label under it."""
        p = self.parts
        if empty:
            p.append(f'<rect x="{x}" y="{y}" width="{K}" height="{K}" rx="10" fill="none" stroke="{LABEL}" '
                     f'stroke-width="1" stroke-dasharray="4 4" opacity="0.5"/>')
            return
        if style == "tile":
            p.append(f'<rect x="{x}" y="{y}" width="{K}" height="{K}" rx="10" fill="{TILE}"/>')
            cx = x + K / 2
            if count is None:
                size = 34
                p.append(f'<g transform="translate({cx - size / 2} {y + 29 - size / 2}) scale({size / 24})" fill="none" stroke="{color}" '
                         f'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">{ICONS[icon]}</g>')
            else:
                p.append(f'<text x="{cx}" y="{y + 30}" text-anchor="middle" font-size="24" font-weight="400" fill="{color}">{html.escape(count)}</text>')
                size = 16
                p.append(f'<g transform="translate({cx - size / 2} {y + 36}) scale({size / 24})" fill="none" stroke="{color}" '
                         f'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">{ICONS[icon]}</g>')
            p.append(f'<text x="{cx}" y="{y + K - 10}" text-anchor="middle" font-size="9.5" fill="{TEXT}">{html.escape(label)}</text>')
            if badge:
                p.append(f'<circle cx="{x + K - 13}" cy="{y + 12}" r="8" fill="{color}"/>')
                p.append(f'<text x="{x + K - 13}" y="{y + 15.5}" text-anchor="middle" font-size="10" font-weight="600" fill="{GROUND}">{html.escape(badge)}</text>')
            return
        p.append(f'<rect x="{x}" y="{y}" width="{K}" height="{K}" rx="10" fill="{KEY}"/>')
        cx, cy = x + K / 2, y + 27
        ink = color
        if style == "highlight":
            p.append(f'<circle cx="{cx}" cy="{cy}" r="18" fill="{color}"/>')
            ink = GROUND
        elif style == "dim":
            ink = OVERLAY
        if framed:
            p.append(f'<circle cx="{cx}" cy="{cy}" r="19" fill="none" stroke="{TEXT}" stroke-width="2"/>')
        size = 20 if style == "highlight" or framed else 26
        if icon:
            p.append(f'<g transform="translate({cx - size / 2} {cy - size / 2}) scale({size / 24})" fill="none" stroke="{ink}" '
                     f'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">{ICONS[icon]}</g>')
        if badge:
            p.append(f'<circle cx="{x + K - 13}" cy="{y + 12}" r="8" fill="{color}"/>')
            p.append(f'<text x="{x + K - 13}" y="{y + 15.5}" text-anchor="middle" font-size="10" font-weight="600" fill="{GROUND}">{html.escape(badge)}</text>')
        lines = wrap(label)
        for i, line in enumerate(lines):
            ly = y + K - 12 - (len(lines) - 1 - i) * 11
            p.append(f'<text x="{cx}" y="{ly}" text-anchor="middle" font-size="9.5" fill="{TEXT}">{html.escape(line)}</text>')

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
                    self.parts.append(f'<circle cx="{cx + 7}" cy="{y - 4}" r="6" fill="none" stroke="{LABEL}" stroke-width="2"/>')
                self.text(cx + 20, y, text, 12, "400")
                cx += 20 + len(text) * 6.6 + 34
            y += 22
        return y

    def save(self, name, width, height):
        svg = f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif">
<title>{html.escape(self.title)}</title>
<defs><marker id="a" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M2 1L8 5L2 9" fill="none" stroke="{LABEL}" stroke-width="1.5" stroke-linecap="round"/></marker></defs>
{chr(10).join(self.parts)}
</svg>
"""
        open(os.path.join(HERE, name), "w").write(svg)
        print(f"wrote {os.path.join(HERE, name)}")


def wrap(label, width=14):
    """A label in at most two lines, as Logi wraps it under a key."""
    if len(label) <= width:
        return [label] if label else []
    cut = label.rfind(" ", 0, width + 1)
    cut = cut if cut > 0 else width
    rest = label[cut:].strip()
    return [label[:cut].strip(), rest if len(rest) <= width else rest[: width - 2] + ".."]


EMPTY = dict(empty=True)
STATUS = {
    "waiting": ("Question", RED), "go?": ("Plan", ACCENT), "CI failed": ("Alert", RED), "changes": ("Report", RED),
    "done": ("Check", GREEN), "ready": ("Merge", BLUE), "working": ("Working", YELLOW), "review": ("Eye", SUBTEXT),
    "idle": ("Sleep", OVERLAY),
}


def issue(label, status, **extra):
    icon, color = STATUS[status]
    return dict(label=label, icon=icon, color=color, **extra)


def flow():
    """The keypad from your own page down to one issue, and what a key shows after a press."""
    pic, top = Picture("The Herdr folder on the MX Keypad"), 70
    step = 3 * K + 2 * G + 44

    x0 = 20
    pic.text(x0, 40, "0. Your page")
    pic.text(x0, 56, "your own apps and keys in Logi Options+", 11, "400")
    pic.grid(x0, top, [
        dict(label="Spotify", icon="Music"),
        dict(label="Claude", icon="Sparkles"),
        dict(label="issues", icon="List", color=RED, badge="2", style="tile", count="9"),
        dict(label="ChatGPT", icon="Chatbot"),
        dict(label="herdr", icon="Terminal", color=SUBTEXT, style="tile"),
        dict(label="projects", icon="Stack", color=ACCENT, style="tile"),
        EMPTY, EMPTY, EMPTY,
    ])
    pic.text(x0, top + 3 * (K + G) + 14, "Three herdr keys, dark tiles with a short word:\n" + "The stack, Projects, opens the herdr menu.\n"
             + "The terminal opens herdr in iTerm.\n" + "The count, All issues, opens every issue at\nonce, most urgent first; the badge counts\nthose that need you.", 11, "400")

    px = x0 + step
    pic.text(px, 40, "1. Projects")
    pic.text(px, 56, "all issues, then one key per workspace", 11, "400")
    pic.grid(px, top, [
        dict(label="9 issues", icon="List", color=RED, badge="2"),
        dict(label="project A", icon="Folder", color=RED, framed=True, badge="2"),
        dict(label="project B", icon="Folder", color=YELLOW),
        dict(label="project C", icon="Folder", color=OVERLAY),
        EMPTY, EMPTY, EMPTY, EMPTY,
        dict(label="back", icon="Back", color=SUBTEXT),
    ])

    qx = px + step
    pic.text(qx, 40, "2. A project")
    pic.text(qx, 56, "main tab, then issues", 11, "400")
    pic.grid(qx, top, [
        dict(label="main", icon="Home", color=OVERLAY),
        issue("3 sign up", "ready"),
        issue("4 search", "working"),
        issue("5 export", "idle"),
        issue("6 dark mode", "waiting"),
        issue("7 checkout", "go?", framed=True),
        issue("8 settings", "idle"),
        dict(label="more 1/2", icon="Dots", color=SUBTEXT),
        dict(label="back", icon="Back", color=SUBTEXT),
    ])

    tx = qx + step
    pic.text(tx, 40, "3. Issue: 7 checkout")
    pic.text(tx, 56, "the action that fits lights up", 11, "400")
    pic.grid(tx, top, [
        issue("7 checkout", "go?"),
        dict(label="go", icon="Play", color=ACCENT, style="highlight"),
        dict(label="review and fix", icon="Tool"),
        dict(label="babysit pr", icon="Lifebuoy"),
        dict(label="commit", icon="Commit"),
        dict(label="stop", icon="Stop"),
        dict(label="open PR", icon="PullRequest", style="dim"),
        dict(label="editor", icon="Code"),
        dict(label="back", icon="Back", color=SUBTEXT),
    ])

    fy = top + 3 * (K + G) + 30
    pic.text(tx, fy, "After a press, for 1.5 seconds")
    for i, (label, icon, color) in enumerate([("sent", "Check", GREEN), ("waits on you", "X", RED), ("not now", "Ban", OVERLAY)]):
        pic.key(tx + i * (K + G), fy + 12, label, icon, color, "highlight")

    mid = top + K + G + K / 2
    for x in (x0, px, qx):
        pic.arrow(x + 3 * K + 2 * G + 6, x + step - 8, mid)

    y = pic.legend(20, fy + 12 + K + 34, [
        [(RED, "answer or fix"), (ACCENT, "decide"), (GREEN, "look at the result"), (BLUE, "merge"), (YELLOW, "working"),
         (OVERLAY, "quiet"), (None, "white ring: on screen")],
    ])
    pic.save("keypad.svg", tx + 3 * K + 2 * G + 20, y + 4)


def statuses():
    """Every word a key can show, with what it means."""
    pic, top = Picture("What a key can say"), 64
    slot = 112
    groups = [
        ("The agent", [("waiting", "asks you\nsomething"), ("go?", "a plan waits\nfor your go"), ("working", "is busy"), ("done", "finished\nits turn")]),
        ("The pull request", [("CI failed", "checks fail"), ("changes", "changes\nrequested"), ("review", "waits on\na review"), ("ready", "approved,\nyours to merge")]),
    ]
    x0 = 20
    for name, keys in groups:
        pic.text(x0, 34, name)
        for i, (word, meaning) in enumerate(keys):
            x = x0 + i * slot
            pic.key(x, top, **issue(word, word))
            pic.text(x + K / 2, top + K + 20, meaning, 11, "400", "middle")
        x0 += 4 * slot + 30
    pic.text(20, top + K + 70, "Each status has its own icon and colour. The agent wins while it asks or works; after that the pull request speaks.", 12, "400")
    pic.save("statuses.svg", 4 * slot * 2 + 50, top + K + 84)


def all_issues():
    """The All issues key opens an overview of every issue across projects, most urgent first."""
    pic, top = Picture("All your issues at once"), 64
    pic.text(20, 40, "All issues")
    pic.text(20, 56, "on your page", 11, "400")
    pic.key(30, top + K + G, "issues", icon="List", color=RED, badge="2", style="tile", count="9")

    ox = 150
    pic.text(ox, 40, "The overview")
    pic.text(ox, 56, "every issue, most urgent first", 11, "400")
    pic.grid(ox, top, [
        issue("6 dark mode · project A", "waiting"),
        issue("7 checkout · project A", "go?"),
        issue("3 sign up · project A", "ready"),
        issue("4 search · project A", "working"),
        issue("2 api docs · project B", "working"),
        issue("5 export · project A", "idle"),
        issue("8 settings · project A", "idle"),
        dict(label="more 1/2", icon="Dots", color=SUBTEXT),
        dict(label="back", icon="Back", color=SUBTEXT),
    ])

    ix = ox + 3 * K + 2 * G + 44
    pic.text(ix, 40, "Its page: 6 dark mode")
    pic.text(ix, 56, "back returns to the overview", 11, "400")
    pic.grid(ix, top, [
        issue("6 dark mode · npm test", "waiting"),
        dict(label="1 Yes · 6 dark mode", icon="Check", color=GREEN),
        dict(label="2 Always · 6 dark mode", icon="Checks", color=GREEN),
        dict(label="3 Auto mode · 6 dark mode", icon="Robot", color=SUBTEXT),
        dict(label="4 No · 6 dark mode", icon="X", color=RED),
        EMPTY, EMPTY, EMPTY,
        dict(label="back", icon="Back", color=SUBTEXT),
    ])

    mid = top + K + G + K / 2
    pic.arrow(30 + K + 10, ox - 10, mid, "press")
    pic.arrow(ox + 3 * K + 2 * G + 6, ix - 8, mid, "pick one")
    note = top + 3 * (K + G) + 24
    pic.text(20, note, "The key counts issue tabs, not main tabs; the badge counts what needs you: waiting, go?, CI failed, changes and done.\n"
                       "Main tabs come last, on the next page, unless they need you. back on the overview goes to your projects.", 12, "400")
    pic.save("all-issues.svg", ix + 3 * K + 2 * G + 20, note + 30)


def answer():
    """A waiting agent's question on screen, and the same choices on the keypad."""
    pic, top = Picture("Answering a question"), 64
    bx, bw, bh = 20, 380, 3 * (K + G) - G
    pic.parts.append(f'<rect x="{bx}" y="{top}" width="{bw}" height="{bh}" rx="8" fill="#16181c"/>')
    lines = [
        ("Bash command", TEXT, "600"),
        ("npm test", SUBTEXT, "400"),
        ("Do you want to proceed?", TEXT, "400"),
        ("❯ 1. Yes", TEXT, "400"),
        ("  2. Yes, and don’t ask again for: npm test", TEXT, "400"),
        ("  3. Yes, and switch to auto mode", TEXT, "400"),
        ("  4. No", TEXT, "400"),
        ("Esc to cancel · Tab to amend", SUBTEXT, "400"),
    ]
    for i, (line, color, weight) in enumerate(lines):
        pic.parts.append(f'<text x="{bx + 14}" y="{top + 26 + i * 26}" font-size="12" font-weight="{weight}" '
                         f'font-family="ui-monospace, SFMono-Regular, Menlo, monospace" fill="{color}" xml:space="preserve">{html.escape(line)}</text>')
    pic.text(bx, 40, "On your screen")
    pic.text(bx, 56, "the agent waits on you", 11, "400")

    kx = bx + bw + 70
    pic.text(kx, 40, "On the keypad")
    pic.text(kx, 56, "the same choices, one key each", 11, "400")
    pic.grid(kx, top, [
        issue("6 dark mode · npm test", "waiting"),
        dict(label="1 Yes · 6 dark mode", icon="Check", color=GREEN),
        dict(label="2 Always · 6 dark mode", icon="Checks", color=GREEN),
        dict(label="3 Auto mode · 6 dark mode", icon="Robot", color=SUBTEXT),
        dict(label="4 No · 6 dark mode", icon="X", color=RED),
        EMPTY, EMPTY, EMPTY,
        dict(label="back", icon="Back", color=SUBTEXT),
    ])
    pic.arrow(bx + bw + 12, kx - 12, top + bh / 2)

    ux = kx + 3 * K + 2 * G + 50
    pic.text(ux, 40, "A question it cannot read")
    pic.text(ux, 56, "answer it on screen", 11, "400")
    pic.grid(ux, top, [
        issue("6 dark mode", "waiting"),
        dict(label="answer on screen", icon="Screen", color=SUBTEXT),
        EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY,
        dict(label="back", icon="Back", color=SUBTEXT),
    ])

    note = top + bh + 30
    pic.text(bx, note, "Each key sends its own choice: the number the screen shows, or the arrow keys and Enter when the choices have no numbers.\n"
                       "Just before sending, helm reads the question again and sends nothing if it changed; the key then reads changed.", 12, "400")
    pic.save("answer.svg", ux + 3 * K + 2 * G + 20, note + 30)


def stamp_links():
    """Points every link to a picture at its current content: name.svg?v=<hash>."""
    root = os.path.dirname(HERE)
    for doc in (os.path.join(root, "README.md"), os.path.join(HERE, "flows.md")):
        text = open(doc).read()

        def versioned(match):
            name = match.group(2)
            path = os.path.join(HERE, name)
            if not os.path.exists(path):
                return match.group(0)
            digest = hashlib.sha1(open(path, "rb").read()).hexdigest()[:8]
            return f"{match.group(1)}{name}?v={digest})"

        stamped = re.sub(r"(\]\((?:docs/)?)([a-z-]+\.svg)(?:\?v=[0-9a-f]+)?\)", versioned, text)
        if stamped != text:
            open(doc, "w").write(stamped)
            print(f"stamped picture links in {doc}")


flow()
statuses()
all_issues()
answer()
stamp_links()
