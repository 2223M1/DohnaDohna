shader_type canvas_item;
uniform vec4 role_color : source_color = vec4(0.396, 0.498, 0.604, 1.0);
varying vec4 vertex_color;
void vertex() { vertex_color = COLOR; }
void fragment() {
    vec4 source = texture(TEXTURE, UV);
    float shade = dot(source.rgb, vec3(0.299, 0.587, 0.114));
    // Keep relief/shadows even on the native neutral frame and title banner.
    float relief = pow(clamp(shade / 0.6, 0.0, 1.0), 0.65);
    vec3 tinted = mix(role_color.rgb * relief, vec3(1.0), smoothstep(0.8, 1.0, shade) * 0.45);
    COLOR = vec4(tinted, source.a) * vertex_color;
}
