using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using YLFramework.Extensions;

namespace YLFramework.Overlap
{

    [Serializable]
    public class OverlapEvent2D : UnityEvent<OverlapHit<Collider2D>>
    {
    }

    public abstract class OverlapBase2D : OverlapBase<Collider2D, OverlapEvent2D>
    {
        protected override void Reset() => layerMask.Physics2DSetting(gameObject.layer);


        protected ContactFilter2D CreateLegacyFilter(int layerMask, float minDepth, float maxDepth)
        {
            ContactFilter2D result = default(ContactFilter2D);
            result.useTriggers = Physics2D.queriesHitTriggers;
            result.SetLayerMask(layerMask);
            result.SetDepth(minDepth, maxDepth);
            return result;
        }

    }
}
