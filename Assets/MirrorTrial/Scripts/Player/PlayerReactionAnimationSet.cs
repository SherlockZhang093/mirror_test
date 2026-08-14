using System;
using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.Player
{
    public sealed class PlayerReactionAnimationSet
    {
        readonly Dictionary<PlayerHitReaction, AnimationClip> clips =
            new Dictionary<PlayerHitReaction, AnimationClip>();

        public static PlayerReactionAnimationSet Load()
        {
            var result = new PlayerReactionAnimationSet();
            var resources = Resources.LoadAll<AnimationClip>(string.Empty);
            for (var i = 0; i < resources.Length; i++)
            {
                PlayerHitReaction reaction;
                if (Enum.TryParse(resources[i].name, out reaction))
                    result.clips[reaction] = resources[i];
            }
            return result;
        }

        public AnimationClip Get(PlayerHitReaction reaction)
        {
            AnimationClip clip;
            if (clips.TryGetValue(reaction, out clip))
                return clip;
            return null;
        }
    }
}
