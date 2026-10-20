using System;
using UnityEngine;

namespace YLFramework.Character.Controller2D
{
    internal static class CharacterMotorMath
    {
        internal static float IntegrateGravity(float speed, float gravity, float scale, float fixedDeltaTime, float maxFallingVelocity)
            => Math.Max(speed + gravity * scale * fixedDeltaTime, -Math.Abs(maxFallingVelocity));

        internal static bool BlocksHorizontal(float movement, float normal)
            => (movement > 0 && normal < 0) || (movement < 0 && normal > 0);

        internal static Vector2 ScaledColliderSize(Vector2 localSize, Vector2 scale)
            => new Vector2(Math.Abs(localSize.x * scale.x), Math.Abs(localSize.y * scale.y));
    }
}
