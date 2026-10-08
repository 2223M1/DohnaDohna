namespace DohnaDohna.Code.Visuals;

internal static class MotionCameraBounds
{
    // The inverse view must remain inside the pre-cinematic visible scene.
    // Clamp the final translation, including shake, not only the requested focus.
    internal static float ClampOrigin(float requested, float minimum, float maximum, float zoom) =>
        Math.Clamp(requested, maximum * (1 - zoom), minimum * (1 - zoom));
}
