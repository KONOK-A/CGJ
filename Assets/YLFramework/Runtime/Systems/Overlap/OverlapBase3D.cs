using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Overlap
{

    [Serializable]
    public class OverlapEvent3D : UnityEvent<OverlapHit<Collider>>
    {
    }

    public abstract class OverlapBase3D : OverlapBase<Collider, OverlapEvent3D>
    {
        protected override void Reset() => layerMask.PhysicsSetting(gameObject.layer);
    }
}
