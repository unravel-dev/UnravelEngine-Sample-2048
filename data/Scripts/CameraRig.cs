using Unravel.Core;

namespace Sample2048
{
    /// <summary>
    /// Moves the camera around its placed pose: a sweeping intro, a slow idle sway around the board, and a shake
    /// that fades out. Place the camera where the game should be seen; this script only adds motion to that pose.
    /// </summary>
    [ScriptSourceFile]
    public class CameraRig : ScriptComponent
    {
        public Vector3 pivot = new Vector3(0.0f, 0.0f, 0.45f);
        public float introSeconds = 1.8f;
        public float introYaw = -28.0f;
        public float introDistance = 1.7f;
        public float swayDegrees = 1.2f;
        public float swayPeriod = 9.0f;
        public float shakeDecay = 6.0f;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float elapsed;
        private float shake;

        /// <summary>Shakes the camera by up to @p strength metres, fading out; a stronger shake replaces a weaker one.</summary>
        public void Shake(float strength)
        {
            shake = Mathf.Max(shake, strength);
        }

        public override void OnStart()
        {
            basePosition = owner.transform.position;
            baseRotation = owner.transform.rotation;
        }

        public override void OnUpdate()
        {
            elapsed += Time.deltaTime;
            float intro = Ease.OutCubic(elapsed / introSeconds);
            float sway = Mathf.Sin(elapsed * 2.0f * Mathf.PI / swayPeriod) * swayDegrees;
            float yaw = Mathf.Lerp(introYaw, 0.0f, intro) + sway;
            float distance = Mathf.Lerp(introDistance, 1.0f, intro);
            var orbit = Quaternion.Euler(0.0f, yaw, 0.0f);
            var position = pivot + orbit * ((basePosition - pivot) * distance);
            if (shake > 0.0f)
            {
                position += Random.insideUnitSphere * shake;
                shake = Mathf.Max(0.0f, shake - shake * shakeDecay * Time.deltaTime - 0.001f * Time.deltaTime);
            }
            owner.transform.position = position;
            owner.transform.rotation = orbit * baseRotation;
        }
    }
}
