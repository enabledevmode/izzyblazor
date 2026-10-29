"""Build IzzyBlazor brand assets: outlined SVGs, PNG exports, ICO."""
import io, os, sys
import uharfbuzz as hb
import resvg_py
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.boundsPen import BoundsPen
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else "out/izzyblazor-brand"
FD = "node_modules/@fontsource"

INK, PAPER, EMBER, AMBER = "#14161A", "#F5F4F1", "#F26A1B", "#FFB020"
STEEL, GREY_L, GREY_D, TRACK = "#8A8F98", "#5A6069", "#A9AEB6", "#2C3038"
TAGLINE = "Blazor components. No circuit required."
SUBHEAD = "Rich components for static SSR. C# interactivity when you need it."
MODES = ["Static", "Interactive"]

class Font:
    def __init__(self, path):
        tt = TTFont(path)
        tt.flavor = None
        buf = io.BytesIO(); tt.save(buf)
        self.data = buf.getvalue()
        self.tt = TTFont(io.BytesIO(self.data))
        self.gs = self.tt.getGlyphSet()
        self.upm = self.tt["head"].unitsPerEm
        self.hbfont = hb.Font(hb.Face(hb.Blob(self.data)))

    def shape(self, text):
        b = hb.Buffer(); b.add_str(text); b.guess_segment_properties()
        hb.shape(self.hbfont, b, {"kern": True, "liga": True})
        return [(self.tt.getGlyphName(i.codepoint), p.x_advance, p.x_offset, p.y_offset)
                for i, p in zip(b.glyph_infos, b.glyph_positions)]

FONTS = {
    "b700": Font(f"{FD}/bricolage-grotesque/files/bricolage-grotesque-latin-700-normal.woff"),
    "b500": Font(f"{FD}/bricolage-grotesque/files/bricolage-grotesque-latin-500-normal.woff"),
    "m600": Font(f"{FD}/jetbrains-mono/files/jetbrains-mono-latin-600-normal.woff"),
}

class Bounds:
    def __init__(self): self.b = None
    def add(self, b):
        if b is None: return
        if self.b is None: self.b = list(b); return
        self.b = [min(self.b[0], b[0]), min(self.b[1], b[1]), max(self.b[2], b[2]), max(self.b[3], b[3])]

def run(fontkey, text, size, x, base, tracking_em=0.0, extra=None):
    """Outline text. Returns (path d, end x, bounds, per-glyph x list). Pixel coords, y down."""
    f = FONTS[fontkey]; s = size / f.upm
    pen = SVGPathPen(f.gs); bounds = Bounds(); xs = []
    for i, (name, adv, xo, yo) in enumerate(f.shape(text)):
        gx = x + xo * s; xs.append((name, gx))
        tr = (s, 0, 0, -s, gx, base - yo * s)
        f.gs[name].draw(TransformPen(pen, tr))
        bp = BoundsPen(f.gs); f.gs[name].draw(TransformPen(bp, tr)); bounds.add(bp.bounds)
        x += adv * s + tracking_em * size
        if extra and i in extra: x += extra[i] * size
    return pen.getCommands(), x, bounds.b, xs

def text(fontkey, s, size, x, base, fill, tracking_em=0.0, anchor="start", opacity=None):
    d, end, b, _ = run(fontkey, s, size, 0, 0, tracking_em)
    w = end - tracking_em * size
    if anchor == "middle": x -= w / 2
    elif anchor == "end": x -= w
    op = f' fill-opacity="{opacity}"' if opacity else ""
    return f'<path transform="translate({x:.2f} {base:.2f})" fill="{fill}"{op} d="{d}"/>', w

# ---------- knob ----------
def knob(cx, cy, r, mode="color", halo=True, core=True, halo_op=0.25, mono=INK):
    """r = body radius."""
    if mode == "mono":
        out_r = r; ring_r = out_r * 22 / 27; sw = out_r * 10 / 27; core_r = out_r * 8 / 27
        return (f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{ring_r:.2f}" fill="none" stroke="{mono}" stroke-width="{sw:.2f}"/>'
                f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{core_r:.2f}" fill="{mono}"/>')
    s = ""
    if halo: s += f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{r*31/22:.2f}" fill="{EMBER}" fill-opacity="{halo_op}"/>'
    s += f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{r:.2f}" fill="{EMBER}"/>'
    if core: s += f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{r*9/22:.2f}" fill="{AMBER}"/>'
    return s

# ---------- wordmark ----------
SCHEMES = {
    "light": dict(izzy=INK, blazor=GREY_L, halo_op=0.22, mode="color"),
    "dark": dict(izzy=PAPER, blazor=GREY_D, halo_op=0.32, mode="color"),
    "mono-ink": dict(izzy=INK, blazor=INK, mode="mono", mono=INK),
    "mono-white": dict(izzy="#FFFFFF", blazor="#FFFFFF", mode="mono", mono="#FFFFFF"),
}

def wordmark(size, scheme="light", halo=True, core=True):
    """Wordmark at font size `size`, origin at left baseline. Returns (svg, bounds)."""
    sc = SCHEMES[scheme]; tr = -0.018
    d1, x1, b1, xs = run("b700", "ızzy", size, 0, 0, tr)
    d2, x2, b2, _ = run("b500", "blazor", size, x1, 0, tr)
    f = FONTS["b700"]; s = size / f.upm
    iname, ix = xs[0]
    bp = BoundsPen(f.gs); f.gs[iname].draw(bp)
    gx0, _, gx1, _ = bp.bounds
    cx = ix + (gx0 + gx1) / 2 * s
    xh = f.tt["OS/2"].sxHeight
    body_r = 0.112 * size
    cy = -(xh * s + 0.075 * size + body_r)
    kn = knob(cx, cy, body_r, sc["mode"], halo, core, sc.get("halo_op", 0.25), sc.get("mono", INK))
    svg = f'<path fill="{sc["izzy"]}" d="{d1}"/><path fill="{sc["blazor"]}" d="{d2}"/>{kn}'
    B = Bounds(); B.add(b1); B.add(b2)
    kr = body_r * 31 / 22 if (halo and sc["mode"] == "color") else body_r
    B.add((cx - kr, cy - kr, cx + kr, cy + kr))
    return svg, B.b

def svg_doc(w, h, body, bg=None, vb=None):
    vb = vb or f"0 0 {w} {h}"
    bgr = f'<rect width="100%" height="100%" fill="{bg}"/>' if bg else ""
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="{vb}">{bgr}{body}</svg>\n'

def save(path, content):
    p = os.path.join(OUT, path); os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w") as fh: fh.write(content)
    return p

def png(svg_path, out_path, width=None, height=None):
    kw = {"svg_path": os.path.abspath(svg_path)}
    if width: kw["width"] = width
    if height: kw["height"] = height
    data = bytes(resvg_py.svg_to_bytes(**kw))
    p = os.path.join(OUT, out_path); os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "wb") as fh: fh.write(data)
    return p

def placed_wordmark(size, x, base, scheme, anchor="start", halo=True, core=True):
    body, b = wordmark(size, scheme, halo, core)
    w = b[2] - b[0]
    if anchor == "middle": x -= w / 2 + b[0]
    else: x -= b[0]
    return f'<g transform="translate({x:.2f} {base:.2f})">{body}</g>', b, w

# ============ 1. logo ============
for scheme in SCHEMES:
    body, b = wordmark(200, scheme)
    pad = 0
    x0, y0, x1, y1 = b
    vb = f"{x0-pad:.2f} {y0-pad:.2f} {x1-x0+2*pad:.2f} {y1-y0+2*pad:.2f}"
    w, h = round(x1 - x0), round(y1 - y0)
    p = save(f"logo/izzyblazor-wordmark-{scheme}.svg", svg_doc(w, h, body, vb=vb))
    png(p, f"logo/png/izzyblazor-wordmark-{scheme}.png", width=w * 4)
# small-size wordmark (no halo) for headers
for scheme in ("light", "dark"):
    body, b = wordmark(200, scheme, halo=False)
    x0, y0, x1, y1 = b
    vb = f"{x0:.2f} {y0:.2f} {x1-x0:.2f} {y1-y0:.2f}"
    save(f"logo/izzyblazor-wordmark-{scheme}-small.svg", svg_doc(round(x1-x0), round(y1-y0), body, vb=vb))

# ---------- "ib" monogram ----------
def ib_paths(track=-0.02, size=100):
    di, xe, bi, _ = run("b700", "i", size, 0, 0, track)
    db, _, bb, _ = run("b700", "b", size, xe, 0, track)
    return di, db, (min(bi[0], bb[0]), min(bi[1], bb[1]), max(bi[2], bb[2]), max(bi[3], bb[3]))

def ib_group(cx, cy, box, i_fill, b_fill, i_class=""):
    """Monogram centered at cx,cy, fitted into a square of side `box`."""
    di, db, (x0, y0, x1, y1) = ib_paths()
    k = min(box / (x1 - x0), box / (y1 - y0))
    tx = cx - k * (x0 + x1) / 2; ty = cy - k * (y0 + y1) / 2
    ic = f' class="{i_class}"' if i_class else ""
    return (f'<g transform="translate({tx:.2f} {ty:.2f}) scale({k:.4f})">'
            f'<path{ic} fill="{i_fill}" d="{di}"/><path fill="{b_fill}" d="{db}"/></g>')

def monogram_svg(i_fill, b_fill=EMBER):
    return svg_doc(64, 64, ib_group(32, 32, 64, i_fill, b_fill))

def tile_svg(size, box_frac, glow=False, radius_frac=None, circle_bg=False, spread="71%"):
    """Monogram on ink, optional soft ember radial glow behind it."""
    c = size / 2
    body = ""
    if glow:
        # inverse radial: clean ink behind the letters, ember rising toward the edges
        body += (f'<defs><radialGradient id="g" cx="50%" cy="50%" r="{spread}">'
                 f'<stop offset="0" stop-color="{EMBER}" stop-opacity="0"/>'
                 f'<stop offset="0.6" stop-color="{EMBER}" stop-opacity="0"/>'
                 f'<stop offset="0.85" stop-color="{EMBER}" stop-opacity="0.12"/>'
                 f'<stop offset="1" stop-color="{EMBER}" stop-opacity="0.30"/></radialGradient></defs>')
    if radius_frac is not None:
        body += f'<rect width="{size}" height="{size}" rx="{size*radius_frac:.1f}" fill="{INK}"/>'
    else:
        body += f'<rect width="{size}" height="{size}" fill="{INK}"/>'
    if glow:
        rr = f' rx="{size*radius_frac:.1f}"' if radius_frac is not None else ""
        body += f'<rect width="{size}" height="{size}"{rr} fill="url(#g)"/>'
    body += ib_group(c, c, size * box_frac, PAPER, EMBER)
    return svg_doc(size, size, body)

# logo folder: monogram files (the glowing knob stays as the dot in the wordmark)
for name, ifill in (("monogram-light", INK), ("monogram-dark", PAPER), ("monogram-fallback", STEEL)):
    p = save(f"logo/izzyblazor-{name}.svg", monogram_svg(ifill))
    png(p, f"logo/png/izzyblazor-{name}-512.png", width=512)
for name, col in (("monogram-mono-ink", INK), ("monogram-mono-white", "#FFFFFF")):
    p = save(f"logo/izzyblazor-{name}.svg", monogram_svg(col, col))
    png(p, f"logo/png/izzyblazor-{name}-512.png", width=512)
p = save("logo/izzyblazor-monogram-tile.svg", tile_svg(512, 0.56, glow=False, radius_frac=0.22))
png(p, "logo/png/izzyblazor-monogram-tile-512.png", width=512)
p = save("logo/izzyblazor-knob.svg", svg_doc(64, 64, knob(32, 32, 22, halo=True, halo_op=0.25)))
png(p, "logo/png/izzyblazor-knob-512.png", width=512)

# ============ 2. favicons / app icons ============
# ---------- pixel-snapped monogram for favicon sizes ----------
# Bricolage Grotesque Bold units: i stem x 66..210 (144 wide), x-height 525, dot x 52..227 y 604..744,
# b stem starts at x 66, b ends at x 600.
SNAP = {  # size: (stem px, x-height px, gap stem-to-stem px, i stem x, baseline y, dot overhang px, dot h px, dot gap px)
    16: (3, 11, 2, 0, 16, 0, 3, 2),
    32: (6, 22, 3, 1, 31, 1, 6, 3),
    48: (9, 33, 4, 1, 47, 1, 9, 5),
}
def snapped_ib(N, i_fill, b_fill, i_class=""):
    stem, xh, gap, ix, base, oh, dh, dg = SNAP[N]
    f = FONTS["b700"]; cm = f.tt.getBestCmap()
    kx, ky = stem / 144.0, xh / 525.0
    bx = ix + stem + gap
    xs = min(1.0, (N - bx) / ((600 - 66) * kx))
    c = f' class="{i_class}"' if i_class else ""
    out = []
    pen = SVGPathPen(f.gs)
    f.gs["dotlessi"].draw(TransformPen(pen, (kx, 0, 0, -ky, ix - 66 * kx, base)))
    out.append(f'<path{c} fill="{i_fill}" d="{pen.getCommands()}"/>')
    dw = stem + 2 * oh; dy = base - xh - dg - dh
    out.append(f'<rect{c} fill="{i_fill}" x="{ix - oh}" y="{dy}" width="{dw}" height="{dh}" rx="{(dh / 2 if dh >= 6 else 0.8):.2f}"/>')
    pen = SVGPathPen(f.gs)
    f.gs[cm[ord("b")]].draw(TransformPen(pen, (kx * xs, 0, 0, -ky, bx - 66 * kx * xs, base)))
    out.append(f'<path fill="{b_fill}" d="{pen.getCommands()}"/>')
    return "".join(out)

# SVG favicon: drawn on the 16 px grid (stays on whole pixels at 32 px for high-DPI tabs)
save("favicon/favicon.svg", f'''<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16">
<style>.i{{fill:{INK}}}@media (prefers-color-scheme:dark){{.i{{fill:{PAPER}}}}}</style>
{snapped_ib(16, INK, EMBER, "i")}
</svg>
''')
ims = []
for N in (16, 32, 48):
    p = save(f"favicon/_f{N}.svg", svg_doc(N, N, snapped_ib(N, STEEL, EMBER)))
    q = png(p, f"favicon/favicon-{N}.png", width=N)
    os.remove(p)
    ims.append(Image.open(q).convert("RGBA"))
ims[2].save(os.path.join(OUT, "favicon/favicon.ico"), format="ICO", sizes=[(16, 16), (32, 32), (48, 48)], append_images=ims[:2])

sq = save("favicon/_square.svg", tile_svg(512, 0.56, glow=False))
png(sq, "favicon/apple-touch-icon.png", width=180)
png(sq, "favicon/icon-192.png", width=192)
png(sq, "favicon/icon-512.png", width=512)
mk = save("favicon/_maskable.svg", tile_svg(512, 0.42, glow=False))
png(mk, "favicon/icon-512-maskable.png", width=512)
os.remove(sq); os.remove(mk)
save("favicon/site.webmanifest", """{
  "name": "IzzyBlazor",
  "short_name": "IzzyBlazor",
  "icons": [
    { "src": "/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/icon-512-maskable.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ],
  "theme_color": "#14161A",
  "background_color": "#14161A",
  "display": "standalone"
}
""")
save("favicon/head-snippet.html", """<link rel="icon" href="/favicon.ico" sizes="48x48">
<link rel="icon" href="/favicon.svg" type="image/svg+xml">
<link rel="apple-touch-icon" href="/apple-touch-icon.png">
<link rel="manifest" href="/site.webmanifest">
<meta name="theme-color" content="#14161A">
""")

# GitHub crops avatars to a circle: keep the letters inside the center
av = save("github/_avatar.svg", tile_svg(500, 0.50, glow=False))
png(av, "github/avatar-500.png", width=500)
os.remove(av)
nu = save("nuget/_icon.svg", tile_svg(128, 0.62, glow=False, radius_frac=0.2))
png(nu, "nuget/icon.png", width=128)
os.remove(nu)

# ============ 3. banners ============
def mode_badge(x, y, h, active="Static", dark=True):
    """The render-mode switch. active=None shows both modes as supported (a dot each), for marketing."""
    fs = h * 0.42; padx = h * 0.42; gap = h * 0.08; inset = h * 0.12
    dot = h * 0.13; dgap = h * 0.22
    extra = (dot * 2 + dgap) if active is None else 0
    widths = [text("m600", l, fs, 0, 0, "#000")[1] + 2 * padx + extra for l in MODES]
    total = sum(widths) + gap * (len(MODES) - 1) + inset * 2
    bg, border, idle = ("#1E2126", "#3A3F48", STEEL) if dark else ("#FFFFFF", "#D6D3CC", "#6B717B")
    s = f'<rect x="{x:.2f}" y="{y:.2f}" width="{total:.2f}" height="{h:.2f}" rx="{h/2:.2f}" fill="{bg}" stroke="{border}" stroke-width="{h*0.03:.2f}"/>'
    cx = x + inset
    col = {"Static": AMBER, "Interactive": EMBER}
    for l, w in zip(MODES, widths):
        fill = idle
        if active is None:
            fill = PAPER if dark else INK
            tx = cx + padx + dot * 2 + dgap
            s += f'<circle cx="{cx + padx + dot:.2f}" cy="{y + h/2:.2f}" r="{dot:.2f}" fill="{col[l]}"/>'
            s += text("m600", l, fs, tx, y + h / 2 + fs * 0.36, fill)[0]
        else:
            if l == active:
                s += f'<rect x="{cx:.2f}" y="{y+inset:.2f}" width="{w:.2f}" height="{h-2*inset:.2f}" rx="{(h-2*inset)/2:.2f}" fill="{col[l]}"/>'
                fill = INK
            s += text("m600", l, fs, cx + w / 2, y + h / 2 + fs * 0.36, fill, anchor="middle")[0]
        cx += w + gap
    return s, total

def glow(cx, cy, r):
    s = ""
    for k, op in ((2.6, 0.05), (2.0, 0.07), (1.5, 0.10), (31/22, 0.22)):
        s += f'<circle cx="{cx}" cy="{cy}" r="{r*k:.1f}" fill="{EMBER}" fill-opacity="{op}"/>'
    s += f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{EMBER}"/><circle cx="{cx}" cy="{cy}" r="{r*9/22:.1f}" fill="{AMBER}"/>'
    return s

def card(w, h, wm_size, tag_size, sub_size, badge_h, left, path, glow_r=None, show_badge=True, show_sub=True):
    body = ""
    if glow_r:
        body += f'<g>{glow(w - glow_r * 0.7, h * 0.5, glow_r)}</g>'
    wm, b, ww = placed_wordmark(wm_size, left, 0, "dark")
    wm_h = -b[1]
    top = (h - (wm_h + tag_size * 1.6 + (sub_size * 1.7 if show_sub else 0) + (badge_h * 1.6 if show_badge else 0))) / 2
    base = top + wm_h
    wm, b, ww = placed_wordmark(wm_size, left, base, "dark")
    body += wm
    y = base + b[3] + tag_size * 1.5
    body += text("b700", TAGLINE, tag_size, left, y, PAPER, -0.015)[0]
    if show_sub:
        y += sub_size * 1.7
        body += text("b500", SUBHEAD, sub_size, left, y, GREY_D)[0]
    if show_badge:
        y += badge_h * 0.9
        bs, _ = mode_badge(left, y, badge_h, None)
        body += bs
    p = save(path, svg_doc(w, h, body, bg=INK))
    return p

p = card(1280, 640, 132, 40, 25, 46, 96, "github/social-preview.svg", glow_r=120)
png(p, "github/social-preview.png", width=1280)
p = card(1200, 630, 124, 38, 24, 44, 90, "social/og-image.svg", glow_r=115)
png(p, "social/og-image.png", width=1200)
p = card(1500, 500, 120, 36, 23, 0, 110, "social/banner-1500x500.svg", glow_r=110, show_badge=False)
png(p, "social/banner-1500x500.png", width=1500)

def readme_banner(scheme):
    w, h = 1280, 300
    wm, b, ww = placed_wordmark(116, w / 2, 0, scheme, "middle")
    tag_size = 32
    block = -b[1] + b[3] + tag_size * 1.9
    base = (h - block) / 2 - b[1]
    wm, b, ww = placed_wordmark(116, w / 2, base, scheme, "middle")
    fill = INK if scheme == "light" else PAPER
    t = text("b500", TAGLINE, tag_size, w / 2, base + b[3] + tag_size * 1.7, GREY_L if scheme == "light" else GREY_D, anchor="middle")[0]
    return svg_doc(w, h, wm + t)
for sc in ("light", "dark"):
    p = save(f"github/readme-banner-{sc}.svg", readme_banner(sc))
    png(p, f"github/readme-banner-{sc}.png", width=1280)

# hero 1600x800 (export 2x), dark and light
def hero(scheme="dark"):
    dark = scheme == "dark"
    fg, sub, panel, line = (PAPER, GREY_D, "#1E2126", "#3A3F48") if dark else (INK, GREY_L, "#FFFFFF", "#D6D3CC")
    accent = EMBER if dark else "#E0590F"
    w, h = 1600, 800; left = 120; body = glow(w - 160, h / 2, 140)
    base = 300
    wm, b, _ = placed_wordmark(145, left, base, scheme)
    body += wm
    y = base + b[3] + 88
    body += text("b700", "Blazor components.", 64, left, y, fg, -0.02)[0]
    y += 76
    body += text("b700", "No circuit required.", 64, left, y, accent, -0.02)[0]
    y += 60
    body += text("b500", SUBHEAD, 28, left, y, sub)[0]
    y += 50
    cmd = "dotnet add package IzzyBlazor"
    cw = text("m600", cmd, 22, 0, 0, "#000")[1]
    body += f'<rect x="{left}" y="{y}" width="{cw+56:.1f}" height="56" rx="12" fill="{panel}" stroke="{line}" stroke-width="1.5"/>'
    body += text("m600", "$", 22, left + 24, y + 36, STEEL)[0]
    body += text("m600", cmd, 22, left + 46, y + 36, fg)[0]
    bs, _ = mode_badge(left + cw + 56 + 24, y + 4, 48, None, dark)
    body += bs
    return svg_doc(w, h, body, bg=INK if dark else PAPER)
for sc in ("dark", "light"):
    p = save(f"hero/hero-{sc}.svg", hero(sc))
    png(p, f"hero/hero-{sc}.png", width=3200)

# mode badges: one per mode (for demos and thumbnails), plus "both" for marketing
for mode in MODES + [None]:
    for dk in (True, False):
        bs, tw = mode_badge(2, 2, 40, mode, dk)
        nm = f"badges/mode-{(mode or 'both').lower()}-{'dark' if dk else 'light'}.svg"
        p = save(nm, svg_doc(round(tw + 4), 44, bs))
        png(p, nm.replace(".svg", ".png").replace("badges/", "badges/png/"), height=88)

print("done")
