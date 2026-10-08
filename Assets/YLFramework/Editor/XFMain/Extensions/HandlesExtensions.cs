// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/Extensions/HandlesExtensions.cs
using UnityEditor;
using UnityEngine;


namespace YLFramework.Core.Editor 
{
    
    public static class HandlesExtensions 
    {
        // Fix编码

        /// <summary>
        /// 在Scene视图绘制2D胶囊体
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="size">大小</param>
        /// <param name="dir">方向</param>
        public static void DrawCapsule2D(Vector3 center, Vector2 size, CapsuleDirection2D dir)
        {
            size.Set(Mathf.Abs(size.x),Mathf.Abs(size.y));

            if (dir == CapsuleDirection2D.Vertical)
            {
                if(size.y < size.x)
                    size.Set(size.x, size.x);

                Handles.DrawWireArc(new Vector3(0, (size.y - size.x) / 2) + center, -Vector3.forward, Vector3.left, 180, size.x / 2);

                Handles.DrawLine(new Vector3(-size.x / 2, -(size.y - size.x) / 2) + center, new Vector3(-size.x / 2, (size.y - size.x) / 2));
                Handles.DrawLine(new Vector3(size.x / 2, -(size.y - size.x) / 2) + center, new Vector3(size.x / 2, (size.y - size.x) / 2));

                Handles.DrawWireArc(new Vector3(0, -(size.y - size.x) / 2) + center, Vector3.forward, Vector3.left, 180, size.x / 2);
            }
            else
            {
                if (size.x < size.y)
                    size.Set(size.y, size.y);

                Handles.DrawWireArc(new Vector3(-(size.x - size.y) / 2, 0) + center, -Vector3.forward, Vector3.down, 180, size.y / 2);

                Handles.DrawLine(new Vector3(-(size.x - size.y) / 2, -size.y / 2) + center, center + new Vector3((size.x - size.y) / 2, -size.y / 2));
                Handles.DrawLine(new Vector3(-(size.x - size.y) / 2, size.y / 2) + center, center + new Vector3((size.x - size.y) / 2, size.y / 2));

                Handles.DrawWireArc(new Vector3((size.x - size.y) / 2, 0) + center, Vector3.forward, Vector3.down, 180, size.y / 2);
            }
        }

    }
}
