shader_type canvas_item;
render_mode unshaded;
uniform vec4 motion_tint : source_color = vec4(0.0);

void fragment() {
    // AIN33859: colored silhouette = add(color) + multiply(black).
    // None restores add(black) + multiply(white); preserve sampled alpha.
    COLOR.rgb = COLOR.rgb * (1.0 - motion_tint.a) + motion_tint.rgb;
}
