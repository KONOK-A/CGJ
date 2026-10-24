using System;
using UnityEngine.Audio;

namespace YLFramework.Audio
{
    /// <summary>Optional request routing. The zero value inherits the category's runtime output group.</summary>
    [Serializable]
    public struct AudioOutputRoute
    {
        /// <summary>Inherit category routing, use an explicit mixer group, or bypass mixer output.</summary>
        public enum RouteMode
        {
            Inherit,
            MixerGroup,
            Direct
        }

        /// <summary>Zero means inherit. Only MixerGroup may contain a Group reference.</summary>
        public RouteMode Mode;
        /// <summary>Caller-owned output group, required only for MixerGroup routing.</summary>
        public AudioMixerGroup Group;
        /// <summary>Use the current category default, including subsequent default changes.</summary>
        public static AudioOutputRoute Inherit => default;
        /// <summary>Bypass category mixer routing even when a default group is configured.</summary>
        public static AudioOutputRoute Direct => new AudioOutputRoute { Mode = RouteMode.Direct };

        /// <summary>Route this request to a caller-owned, live mixer group independently of category defaults.</summary>
        public static AudioOutputRoute ToMixer(AudioMixerGroup group)
        {
            var route = new AudioOutputRoute { Mode = RouteMode.MixerGroup, Group = group };
            route.Validate();
            return route;
        }

        internal void Validate()
        {
            if ((uint)Mode > (uint)RouteMode.Direct)
            {
                throw new ArgumentOutOfRangeException(nameof(Mode));
            }

            if (Mode == RouteMode.MixerGroup)
            {
                if (!Group)
                {
                    throw new ArgumentException("A mixer route requires a live output group.", nameof(Group));
                }
            }
            else if (HasReference(Group))
            {
                throw new ArgumentException("Only a MixerGroup route can contain an output group.", nameof(Group));
            }
        }

        // Serialized empty Unity references can have a managed wrapper with instance ID zero.
        internal static bool HasReference(UnityEngine.Object value) => !ReferenceEquals(value, null) && value.GetInstanceID() != 0;

        internal AudioMixerGroup Resolve(AudioMixerGroup categoryGroup)
        {
            var group = Mode == RouteMode.Inherit ? categoryGroup : Mode == RouteMode.MixerGroup ? this.Group : null;
            return group ? group : null;
        }
    }
}
