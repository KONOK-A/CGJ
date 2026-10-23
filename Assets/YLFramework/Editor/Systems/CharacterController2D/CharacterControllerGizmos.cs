using UnityEditor;
using UnityEngine;

namespace YLFramework.Character.Controller2D
{
    internal static class CharacterControllerGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(CharacterController2D motor, GizmoType gizmoType)
        {
            if (!motor.ShowDebugLine || !Application.isPlaying)
            {
                return;
            }

            var oldColor = Handles.color;
            try
            {
                if (motor.IsGrounded)
                {
                    DrawHit(motor.GroundInfo, Vector2.up, Color.yellow, "地面角度");
                }

                if (motor.IsTouchingWall)
                {
                    DrawHit(motor.WallInfo, motor.WallInfo.normal.x > 0 ? Vector2.right : Vector2.left, Color.red, "墙面角度");
                }

                if (motor.IsTouchingCeiling)
                {
                    DrawHit(motor.TopInfo, Vector2.down, Color.green, "头顶角度");
                }
            }
            finally
            {
                Handles.color = oldColor;
            }
        }

        private static void DrawHit(RaycastHit2D hit, Vector2 direction, Color color, string label)
        {
            Handles.color = color;
            Handles.Label(hit.point, $"{label}:{Vector2.Angle(hit.normal, direction)}");
            Handles.DrawLine(hit.point, hit.point + hit.normal * 0.2f);
        }
    }
}
