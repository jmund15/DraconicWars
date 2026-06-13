shader_type canvas_item;

// Boosts the sprite's hottest palette pixels above 1.0 so the WorldEnvironment HDR glow
// blooms them. The element tint / hit-flash live on modulate (the incoming COLOR) and are
// left intact — emissive rides the material, a separate lane (compose, don't stomp).
// Matching is on the raw texture color (pre-modulate), so it is tint-stable.
// tolerance + boost are playtest-tuned.
uniform vec4 emissive_a = vec4(0.0);
uniform vec4 emissive_b = vec4(0.0);
uniform float boost = 1.5;
uniform float tolerance : hint_range(0.0, 0.5) = 0.06;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    COLOR = tex * COLOR;
    if (tex.a > 0.5 && (distance(tex.rgb, emissive_a.rgb) < tolerance || distance(tex.rgb, emissive_b.rgb) < tolerance)) {
        COLOR.rgb = tex.rgb * boost;
    }
}
