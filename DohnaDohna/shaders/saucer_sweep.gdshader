shader_type canvas_item;
render_mode blend_mix;

// Native SweepingBeam flipbook and LUT, without its fixed floor-perspective
// projection or GPU particle clock. The role-owned ray supplies both.
uniform sampler2D lut : repeat_disable;
varying vec4 tint;
void vertex() { tint = COLOR; }
void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    COLOR = tint * vec4(texture(lut, vec2(tex.r, 0.0)).rgb, tex.a);
}
