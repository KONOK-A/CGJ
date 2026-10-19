using UnityEngine;
using UnityEngine.Scripting;
using YLFramework.Overlap;

namespace YLFramework.Behavior.Acceptance
{
    [Preserve]
    public sealed class BehaviorEventReceiver : MonoBehaviour
    {
        public int hitCount;
        public int boolCount;
        public int intCount;
        public int vector2Count;
        public int vector3Count;
        public void ReceiveHit(OverlapHit<Collider2D> value) => hitCount++;
        public void ReceiveBool(bool value) => boolCount++;
        public void ReceiveInt(int value) => intCount++;
        public void ReceiveVector2(Vector2 value) => vector2Count++;
        public void ReceiveVector3(Vector3 value) => vector3Count++;
    }
}
