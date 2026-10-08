shader_type canvas_item;
render_mode unshaded, blend_mul;

void fragment() {
    // Godot blend_mul ignores source alpha in RGB. Transparent texels must
    // multiply by white, not their stored black RGB (which creates a rectangle).
    COLOR.rgb = mix(vec3(1.0), COLOR.rgb, COLOR.a);
    COLOR.a = 1.0;
}
