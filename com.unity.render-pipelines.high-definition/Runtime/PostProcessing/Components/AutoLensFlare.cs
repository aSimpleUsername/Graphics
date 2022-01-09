using System;

namespace UnityEngine.Rendering.HighDefinition
{
    [VolumeComponentMenu("Post-processing/AutoLensFlare")]
    public sealed class AutoLensFlare : VolumeComponent, IPostProcessComponent
    {
        /// <summary>
        /// Controls the effectiveness of The Director
        /// </summary>
        [Tooltip("Intensity")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
		[Tooltip("Scale")]
        public ClampedFloatParameter scale = new ClampedFloatParameter(0f, 0f, 10f);

        /// <summary>
        /// Mandatory function, cannot have an Override without it
        /// </summary>
        /// <returns></returns>
        public bool IsActive()
        {
            return intensity.value > 0f;
        }
    }

}