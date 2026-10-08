"""Offline layer geometry shared by card art and IsShadow silhouettes.

Coordinates retain the original MB/MM origin; no whole-body bounding-box
centering is applied by the runtime. Commercial source pixels stay external.
"""
import math
from PIL import Image, ImageOps, ImageFilter
import numpy as np


def compose(root, layers, *, shadow=False):
    parts = []
    for layer in sorted(layers, key=lambda value: value.get("Z", 0)):
        if not layer.get("CgName"):
            continue
        image = Image.open(root / layer["CgName"].removeprefix("res://")).convert("RGBA")
        if layer.get("ReverseLR"):
            image = ImageOps.mirror(image)
        w, h = image.size
        # EOriginPos is one-based: AIN9224 maps 5 -> MM and 8 -> MB.
        ox, oy = -w * .5, -h * (.5 if not shadow and layer.get("IsCenterOrigin") else 1)
        angle = math.radians(layer.get("Rotation", 0))
        c, s = math.cos(angle), math.sin(angle)
        corners = [(c*x-s*y, s*x+c*y) for x,y in [(ox,oy),(ox+w,oy),(ox,oy+h),(ox+w,oy+h)]]
        left, top = math.floor(min(x for x,y in corners)), math.floor(min(y for x,y in corners))
        right, bottom = math.ceil(max(x for x,y in corners)), math.ceil(max(y for x,y in corners))
        # Pillow maps output -> source; positive Godot rotation is clockwise.
        image = image.transform((right-left,bottom-top), Image.Transform.AFFINE,
            (c,s,c*left+s*top-ox, -s,c,-s*left+c*top-oy), Image.Resampling.BICUBIC)
        # AIN33893/33894 project each IsShadow image from MB at (PosX, 0).
        # Body elevation is not baked into the ground projection.
        parts.append((image, round(left+layer.get("PosX",0)), round(top+(0 if shadow else layer.get("PosY",0)))))
    if not parts:
        return None, (0, 0)
    left, top = min(x for _,x,y in parts), min(y for _,x,y in parts)
    right, bottom = max(x+i.width for i,x,y in parts), max(y+i.height for i,x,y in parts)
    result = Image.new("RGBA", (right-left,bottom-top))
    for image,x,y in parts:
        result.alpha_composite(image,(x-left,y-top))
    bounds = result.getbbox()
    if not bounds:
        return None, (0,0)
    return result.crop(bounds), (left+bounds[0],top+bounds[1])


def contact(image, origin):
    """Preserve the authored horizontal station; calibrate only ground height.

    AIN33554/33425 center the parameter bar on the common player station, not
    the lowest foot's pixels. A sole-weighted X shifts wide/asymmetric stances
    toward one foot. Neither a sole nor a body bbox may recenter original X.
    Vertical calibration is fixed from the first idle; steps remain authored.
    """
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if not bounds:
        raise ValueError("Idle has no ground contact pixels")
    return [0, origin[1] + bounds[3]]


def ground_shadow(image, origin):
    """Bake a pose-driven painted contact patch, not a flattened human cutout.

    Native 0.111.0 ironclad/leaf-slime shadows are black, grainy, low patches
    (peak alpha 101/91), without recognizable heads/weapons/holes. The source
    IsShadow layers still determine horizontal support and mass each frame.
    Noise is anchored in authored coordinates, not randomized every pose.
    """
    alpha = np.asarray(image.getchannel("A"), dtype=np.float64) / 255
    mass = alpha.sum(axis=0)
    if mass.max() <= 0:
        raise ValueError("Cannot project an empty shadow pose")
    # Close the gaps between legs without inventing a fixed character outline.
    radius = max(2, round(image.width * .07))
    padded = np.pad(mass, (radius, radius), mode="edge")
    profile = np.convolve(padded, np.ones(radius*2+1)/(radius*2+1), mode="valid")
    profile = np.sqrt(np.clip(profile / max(1, np.percentile(profile, 90)), 0, 1))
    width = image.width
    depth = min(64, max(26, width * .19))
    pad = 8
    top = -math.ceil(depth * .62) - pad
    bottom = math.ceil(depth * .38) + pad
    yy, xx = np.mgrid[top:bottom, -pad:width+pad]
    support = np.interp(xx, np.arange(width), profile, left=0, right=0)
    # A shallow, rounded brush stroke whose thickness follows silhouette mass.
    end = np.sqrt(np.clip(1-((xx-(width-1)/2)/(width/2+1))**8, 0, 1))
    half = depth * .5 * (.68 + .32 * support) * end
    center = -depth * .12
    wx = xx + origin[0]
    grain = np.mod(np.sin(wx*12.9898 + yy*78.233)*43758.5453, 1)
    broad = np.sin(wx*.39 + yy*.57)*np.sin(wx*.17 - yy*.43)
    edge = half - abs(yy-center) + broad*1.2 + (grain-.5)*2
    coverage = np.clip(edge / 2.5, 0, 1) * np.clip((xx+2)/3,0,1) * np.clip((width+1-xx)/3,0,1)
    mask = np.rint(coverage * (91 + grain*10)).astype(np.uint8)
    result = Image.new("RGBA", (width+pad*2, bottom-top), (0,0,0,0))
    result.putalpha(Image.fromarray(mask).filter(ImageFilter.GaussianBlur(.35)))
    return result, (origin[0]-pad, top)
