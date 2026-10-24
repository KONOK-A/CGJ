using UnityEditor;
using UnityEngine;
using YLFramework.Platform2D;

namespace YLFramework.Editor.Platform2D
{
    internal static class MoveableGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawPath(Moveable moveable, GizmoType type)
        {
            var oldColor = Handles.color;
            try
            {
                Handles.color = Color.yellow;
                if (moveable.PositionType == PositionType.Positions && moveable.Positions != null)
                {
                    for (int i = 0; i + 1 < moveable.Positions.Count; i++)
                    {
                        Handles.DrawDottedLine(moveable.Positions[i], moveable.Positions[i + 1], 4);
                    }
                }

                if (moveable.PositionType == PositionType.Transforms && moveable.Transforms != null)
                {
                    for (int i = 0; i + 1 < moveable.Transforms.Count; i++)
                    {
                        if (moveable.Transforms[i] && moveable.Transforms[i + 1])
                        {
                            Handles.DrawDottedLine(moveable.Transforms[i].position, moveable.Transforms[i + 1].position, 4);
                        }
                    }
                }
            }
            finally
            {
                Handles.color = oldColor;
            }
        }

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawCatcher(PlatformCatcher2D catcher, GizmoType type)
        {
            if (catcher.areas == null)
            {
                return;
            }

            var oldColor = Handles.color;
            try
            {
                Handles.color = Color.yellow;
                foreach (var area in catcher.areas)
                {
                    Handles.DrawLine(catcher.transform.TransformPoint(area.from), catcher.transform.TransformPoint(area.to));
                }
            }
            finally
            {
                Handles.color = oldColor;
            }
        }
    }
}
