shader_type canvas_item;
// Let the half-body composition dissolve into the native selection background.
void fragment() {
    vec4 tint = COLOR;
    COLOR = texture(TEXTURE, UV) * tint;
    COLOR.a *= 1.0 - smoothstep(0.78, 1.0, UV.y);
}
