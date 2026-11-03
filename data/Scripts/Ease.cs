using Unravel.Core;

namespace Sample2048
{
    /// <summary>Easing curves for the board's motion; t runs from 0 to 1.</summary>
    public static class Ease
    {
        public static float OutCubic(float t)
        {
            float u = 1.0f - Mathf.Clamp01(t);
            return 1.0f - u * u * u;
        }

        public static float InCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>Overshoots past 1 and settles back: pops and landings.</summary>
        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            float u = Mathf.Clamp01(t) - 1.0f;
            return 1.0f + u * u * ((overshoot + 1.0f) * u + overshoot);
        }

        /// <summary>0 at both ends and 1 halfway: a bump.</summary>
        public static float Bump(float t)
        {
            return Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
        }

        /// <summary>A bump that rings out over the second half, like a struck surface.</summary>
        public static float Wobble(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI * 3.0f) * (1.0f - t);
        }
    }
}
