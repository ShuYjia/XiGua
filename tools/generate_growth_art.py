# -*- coding: utf-8 -*-
"""Generate the three program-drawn stage illustrations for ModulePanel_2
(Watermelon Science, growth module).

Style: flat vector-like teaching illustration, matching the existing
Assets/Art/watermelon_whole.png and watermelon_cross_section.png
(white background, 2-3 tones per element, soft ground shadow).

Outputs (into Assets/Art/):
  growth_stage1_germination.png  - seed in moist soil, root down, sprout up
  growth_stage2_seedling.png     - vine with lobed watermelon leaves + tendril
  growth_stage3_flowering.png    - vine, yellow melon flower, bee

Deterministic: no randomness, safe to re-run. Drawn at 3x and downscaled
with LANCZOS for smooth edges.
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

# ---------------------------------------------------------------- constants
W, H = 1600, 1100          # final logical canvas
S = 3                      # supersample factor
OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "Assets", "Art")

# palette (aligned with the Unity UI colors)
SOIL_DARK = (122, 78, 42, 255)
SOIL_BASE = (156, 107, 63, 255)
SOIL_LIGHT = (201, 138, 82, 255)
SOIL_SPECK = (110, 68, 36, 255)
STONE = (203, 195, 178, 255)
SEED = (46, 32, 24, 255)
SEED_EDGE = (74, 52, 40, 255)
ROOT = (242, 230, 200, 255)
ROOT_EDGE = (217, 201, 168, 255)
STEM = (92, 168, 92, 255)
STEM_DARK = (62, 142, 78, 255)
LEAF = (100, 183, 91, 255)
LEAF_DARK = (62, 142, 78, 255)
LEAF_LIGHT = (178, 219, 138, 255)
COTYLEDON = (143, 208, 138, 255)
PETAL = (255, 215, 94, 255)
PETAL_EDGE = (232, 169, 60, 255)
FLOWER_CORE = (255, 176, 58, 255)
STAMEN = (232, 147, 12, 255)
BEE_BODY = (255, 215, 94, 255)
BEE_DARK = (58, 46, 30, 255)
WATER = (191, 227, 242, 255)
SHADOW = (214, 226, 200, 255)


def new_canvas():
    img = Image.new("RGBA", (W * S, H * S), (255, 255, 255, 255))
    return img, ImageDraw.Draw(img)


def sc(v):
    return v * S


def box(x0, y0, x1, y1):
    return [sc(x0), sc(y0), sc(x1), sc(y1)]


def ellipse(d, cx, cy, rx, ry, fill, outline=None, width=1):
    d.ellipse(box(cx - rx, cy - ry, cx + rx, cy + ry), fill=fill,
              outline=outline, width=int(sc(width)))


def rrect(d, x0, y0, x1, y1, r, fill, outline=None, width=1):
    d.rounded_rectangle(box(x0, y0, x1, y1), radius=sc(r), fill=fill,
                        outline=outline, width=int(sc(width)))


def rot_ellipse(img, cx, cy, rx, ry, angle, fill, outline=None, width=1):
    """Axis-aligned ellipse drawn on a tile, rotated, pasted."""
    pad = int(sc(max(rx, ry) + width + 4))
    tile = Image.new("RGBA", (pad * 2, pad * 2), (0, 0, 0, 0))
    td = ImageDraw.Draw(tile)
    td.ellipse([pad - sc(rx), pad - sc(ry), pad + sc(rx), pad + sc(ry)],
               fill=fill, outline=outline, width=int(sc(width)))
    tile = tile.rotate(angle, resample=Image.BICUBIC, center=(pad, pad))
    img.alpha_composite(tile, (int(sc(cx)) - pad, int(sc(cy)) - pad))


def thick_path(d, pts, width, fill):
    """Polygon from a centerline path with constant width."""
    n = len(pts)
    left, right = [], []
    for i, (x, y) in enumerate(pts):
        if i == 0:
            dx, dy = pts[1][0] - x, pts[1][1] - y
        elif i == n - 1:
            dx, dy = x - pts[i - 1][0], y - pts[i - 1][1]
        else:
            dx, dy = pts[i + 1][0] - pts[i - 1][0], pts[i + 1][1] - pts[i - 1][1]
        L = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / L, dx / L
        hw = width / 2.0
        left.append((x + nx * hw, y + ny * hw))
        right.append((x - nx * hw, y - ny * hw))
    poly = left + right[::-1]
    d.polygon([(sc(px), sc(py)) for px, py in poly], fill=fill)


def sine_path(x0, y0, x1, y1, amp, waves, steps=60):
    pts = []
    for i in range(steps + 1):
        t = i / steps
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t + amp * math.sin(t * math.pi * waves)
        pts.append((x, y))
    return pts


def lobed_leaf_pts(cx, cy, R, angle_deg, lobes=5, dip=0.30, steps=180, jitter=None):
    """Watermelon-style lobed leaf outline in polar form."""
    pts = []
    for i in range(steps + 1):
        th = 2 * math.pi * i / steps
        base = math.cos(lobes * th)
        if jitter:
            base *= 1.0 + jitter[int(th / (2 * math.pi) * lobes) % lobes] * 0.06
        r = R * (1.0 - dip + dip * base)
        # slight vertical squash for a leaf look
        x = r * math.cos(th)
        y = r * math.sin(th) * 0.82
        a = math.radians(angle_deg)
        px = cx + x * math.cos(a) - y * math.sin(a)
        py = cy + x * math.sin(a) + y * math.cos(a)
        pts.append((px, py))
    return pts


def draw_leaf(img, d, cx, cy, R, angle_deg, attach=None, fill=LEAF, edge=LEAF_DARK,
              jitter=None):
    # connector to the stem first, so the leaf covers its end
    if attach is not None:
        thick_path(d, [attach, (cx, cy)], 11, STEM)
    pts = lobed_leaf_pts(cx, cy, R, angle_deg, jitter=jitter)
    d.polygon([(sc(px), sc(py)) for px, py in pts], fill=fill,
              outline=edge, width=int(sc(5)))
    # veins: center -> each lobe tip
    a = math.radians(angle_deg)
    for k in range(5):
        th = 2 * math.pi * k / 5
        tipx = cx + R * math.cos(th) * math.cos(a) - R * 0.82 * math.sin(th) * math.sin(a)
        tipy = cy + R * math.cos(th) * math.sin(a) + R * 0.82 * math.sin(th) * math.cos(a)
        d.line([(sc(cx), sc(cy)), (sc(tipx), sc(tipy))], fill=edge, width=int(sc(4)))


def soft_shadow(img, cx, cy, rx, ry, color=SHADOW, blur=18):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    ld.ellipse(box(cx - rx, cy - ry, cx + rx, cy + ry), fill=color)
    layer = layer.filter(ImageFilter.GaussianBlur(sc(blur)))
    img.alpha_composite(layer)


def spiral_pts(cx, cy, r0, turns, a0=0.0, steps=80):
    pts = []
    for i in range(steps + 1):
        t = i / steps
        ang = a0 + t * turns * 2 * math.pi
        r = r0 * (1 - 0.75 * t)
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    return pts


def droplet(d, cx, cy, size, fill=WATER, edge=(140, 190, 214, 255)):
    """Teardrop pointing up."""
    pts = []
    for i in range(41):
        t = 2 * math.pi * i / 40
        x = size * math.sin(t) * (1 - 0.35 * max(0.0, math.cos(t)))
        y = -size * math.cos(t)
        pts.append((cx + x, cy + y))
    d.polygon([(sc(px), sc(py)) for px, py in pts], fill=fill,
              outline=edge, width=int(sc(3)))


# ---------------------------------------------------------------- stage 1
def stage1():
    img, d = new_canvas()
    soft_shadow(img, 800, 905, 520, 58)

    # soil bed (rounded top mound, full width)
    rrect(d, 210, 700, 1390, 1030, 120, SOIL_BASE)
    # lighter top band
    d.pieslice(box(210, 620, 1390, 860), 180, 360, fill=SOIL_LIGHT)
    rrect(d, 210, 700, 1390, 780, 90, SOIL_LIGHT)
    # deeper-soil band near the bottom (kept inside the bed)
    rrect(d, 240, 940, 1360, 1010, 40, SOIL_DARK)
    # speckles + stones (deterministic positions)
    for sx, sy, r in [(360, 800, 14), (480, 860, 10), (1080, 820, 13),
                      (1180, 900, 9), (640, 950, 12), (900, 980, 10),
                      (300, 900, 11), (1240, 780, 8)]:
        ellipse(d, sx, sy, r, r * 0.7, SOIL_SPECK)
    for sx, sy in [(430, 940), (1010, 960), (760, 1010)]:
        ellipse(d, sx, sy, 22, 15, STONE)

    # seed (flat oval, slightly tilted)
    rot_ellipse(img, 772, 812, 46, 62, -18, SEED, SEED_EDGE, 4)
    d = ImageDraw.Draw(img)
    d.line([(sc(742), sc(830)), (sc(800), sc(792))], fill=SEED_EDGE, width=int(sc(5)))
    # bury the lower half of the seed under a soil patch
    ellipse(d, 772, 846, 47, 26, SOIL_BASE)

    # root going down from the buried seed
    root_pts = [(775, 850), (800, 900), (795, 950), (815, 985)]
    thick_path(d, root_pts, 12, ROOT)
    for s in [(762, 905, 645, 930), (805, 940, 862, 962)]:
        d.line([(sc(s[0]), sc(s[1])), (sc(s[2]), sc(s[3]))], fill=ROOT, width=int(sc(7)))

    # sprout: stem + two cotyledons
    stem_pts = [(760, 758), (748, 700), (735, 648), (722, 610)]
    thick_path(d, stem_pts, 13, STEM)
    rot_ellipse(img, 668, 585, 78, 46, -32, COTYLEDON, STEM_DARK, 5)
    rot_ellipse(img, 782, 578, 78, 46, 28, COTYLEDON, STEM_DARK, 5)
    d = ImageDraw.Draw(img)
    # cotyledon veins
    d.line([(sc(700), sc(600)), (sc(636), sc(572))], fill=STEM_DARK, width=int(sc(3)))
    d.line([(sc(752), sc(594)), (sc(812), sc(564))], fill=STEM_DARK, width=int(sc(3)))

    # moisture droplets near the surface
    droplet(d, 540, 690, 20)
    droplet(d, 1050, 700, 16)
    droplet(d, 990, 668, 12)

    return finish(img, "growth_stage1_germination.png")


# ---------------------------------------------------------------- stage 2
def stage2():
    img, d = new_canvas()
    soft_shadow(img, 800, 920, 540, 60)

    # soil strip
    rrect(d, 300, 860, 1300, 1020, 80, SOIL_BASE)
    d.pieslice(box(300, 800, 1300, 940), 180, 360, fill=SOIL_LIGHT)
    for sx, sy, r in [(420, 920, 12), (700, 950, 10), (1080, 930, 13), (1180, 960, 9)]:
        ellipse(d, sx, sy, r, r * 0.7, SOIL_SPECK)

    # main vine stem (gentle S-curve)
    stem = sine_path(770, 880, 745, 400, 55, 1.0)
    thick_path(d, stem, 26, STEM)
    d.line([(sc(px), sc(py)) for px, py in stem], fill=STEM_DARK, width=int(sc(3)))

    # leaves along the vine (attached to stem points)
    draw_leaf(img, d, 545, 715, 165, -38, attach=(761, 761), jitter=[0.4, -0.3, 0.2, -0.4, 0.3])
    draw_leaf(img, d, 985, 660, 175, 34, attach=(756, 670), jitter=[-0.3, 0.4, -0.2, 0.3, -0.4])
    draw_leaf(img, d, 830, 500, 128, -12, attach=(751, 558), jitter=[0.2, 0.3, -0.4, 0.2, -0.3])
    draw_leaf(img, d, 585, 520, 118, -55, attach=(750, 500),
              fill=LEAF_LIGHT, edge=STEM_DARK, jitter=[-0.4, 0.2, 0.3, -0.3, 0.4])

    # curling tendril at the top
    tendril = spiral_pts(700, 385, 55, 1.35, a0=math.radians(200))
    d.line([(sc(px), sc(py)) for px, py in tendril], fill=STEM, width=int(sc(7)),
           joint="curve")

    # tiny new bud on the vine
    rot_ellipse(img, 760, 430, 20, 30, -10, LEAF_LIGHT, STEM_DARK, 4)

    return finish(img, "growth_stage2_seedling.png")


# ---------------------------------------------------------------- stage 3
def stage3():
    img, d = new_canvas()
    soft_shadow(img, 800, 900, 560, 58)

    # trailing vine
    vine = sine_path(240, 640, 1360, 610, 45, 1.5)
    thick_path(d, vine, 24, STEM)
    d.line([(sc(px), sc(py)) for px, py in vine], fill=STEM_DARK, width=int(sc(3)))

    # leaves on the vine
    draw_leaf(img, d, 400, 520, 130, -30)
    draw_leaf(img, d, 1180, 505, 140, 28)

    # main open flower (5 petals)
    fx, fy = 800, 470
    for k in range(5):
        ang = k * 72 - 90
        a = math.radians(ang)
        px, py = fx + 105 * math.cos(a), fy + 105 * math.sin(a)
        rot_ellipse(img, px, py, 62, 92, ang + 90, PETAL, PETAL_EDGE, 5)
        d = ImageDraw.Draw(img)
    ellipse(d, fx, fy, 52, 52, FLOWER_CORE, PETAL_EDGE, 4)
    for k in range(6):
        a = math.radians(k * 60 + 15)
        ellipse(d, fx + 30 * math.cos(a), fy + 30 * math.sin(a), 9, 9, STAMEN)

    # smaller bud on the right
    for k in range(5):
        ang = k * 72 - 90
        a = math.radians(ang)
        px, py = 1055 + 52 * math.cos(a), 690 + 52 * math.sin(a)
        rot_ellipse(img, px, py, 30, 46, ang + 90, PETAL, PETAL_EDGE, 4)
        d = ImageDraw.Draw(img)
    ellipse(d, 1055, 690, 26, 26, FLOWER_CORE, PETAL_EDGE, 3)

    # bee (top-left of the flower, with dashed flight path)
    path = sine_path(380, 300, 620, 330, 30, 1.0)
    for i in range(0, len(path) - 1, 2):
        d.line([(sc(path[i][0]), sc(path[i][1])),
                (sc(path[i + 1][0]), sc(path[i + 1][1]))],
               fill=(180, 190, 175, 255), width=int(sc(4)))
    bx, by = 655, 322
    # wings (behind body)
    rot_ellipse(img, bx - 8, by - 26, 34, 18, -28, (232, 244, 255, 200))
    rot_ellipse(img, bx + 14, by - 26, 30, 16, 24, (232, 244, 255, 200))
    d = ImageDraw.Draw(img)
    # body
    ellipse(d, bx, by, 42, 32, BEE_BODY, BEE_DARK, 4)
    for k, w in enumerate([9, 9, 9]):
        sx = bx - 20 + k * 20
        d.line([(sc(sx), sc(by - 24)), (sc(sx - 4), sc(by + 24))],
               fill=BEE_DARK, width=int(sc(w)))
    ellipse(d, bx + 44, by - 4, 15, 14, BEE_DARK)          # head
    d.line([(sc(bx + 52), sc(by - 14)), (sc(bx + 62), sc(by - 26))],
           fill=BEE_DARK, width=int(sc(3)))                # antenna
    d.line([(sc(bx + 50), sc(by - 12)), (sc(bx + 58), sc(by - 24))],
           fill=BEE_DARK, width=int(sc(3)))

    # pollen dots around the flower
    for px, py, r in [(690, 610, 7), (920, 600, 6), (740, 660, 5), (960, 520, 5)]:
        ellipse(d, px, py, r, r, (255, 233, 168, 255))

    return finish(img, "growth_stage3_flowering.png")


# ---------------------------------------------------------------- save
def finish(img, name):
    img = img.resize((W, H), Image.LANCZOS)
    flat = Image.new("RGB", (W, H), (255, 255, 255))
    flat.paste(img, mask=img.split()[3])
    path = os.path.join(OUT_DIR, name)
    flat.save(path, "PNG")
    print("written:", path, flat.size)
    return path


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    stage1()
    stage2()
    stage3()
    print("done")
