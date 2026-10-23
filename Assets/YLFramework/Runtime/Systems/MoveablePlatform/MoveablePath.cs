using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Platform2D
{
    public enum PositionType
    {
        [Tooltip("使用坐标点作为路径")] Positions,
        [Tooltip("使用transform作为路径")] Transforms
    }

    public enum MoveType
    {
        [Tooltip("在起点和终点之间来回移动")] PingPong,
        [Tooltip("循环移动")] Loop,
        [Tooltip("移动到终点之后自动停止")] Once
    }

    public enum UpdateMode
    {
        [Tooltip("正常模式")] Normal,
        [Tooltip("忽略时间缩放")] UnscaledTime
    }

    internal struct PositionInfo
    {
        public float Percent;
        public Vector3 Position;
        public PositionInfo(float percent, Vector3 position)
        {
            this.Percent = percent;
            this.Position = position;
        }
    }

    // 保留原距离加权线性插值，只把总长计算移出逐节点循环，并固定运行快照。
    internal sealed class MoveablePath
    {
        private readonly List<PositionInfo> _nodes = new List<PositionInfo>();
        public float Distance
        {
            get; private set;
        }
        public int Count => _nodes.Count;

        public static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        public bool TryBuild(IList<Vector3> points, out string error)
        {
            _nodes.Clear();
            Distance = 0;
            if (points == null || points.Count == 0)
            {
                error = "Path has no nodes (index 0).";
                return false;
            }
            for (int i = 0; i < points.Count; i++)
            {
                if (!IsFinite(points[i]))
                {
                    _nodes.Clear();
                    Distance = 0;
                    error = $"Path node {i} contains NaN/Infinity.";
                    return false;
                }
                if (i > 0)
                {
                    Distance += Vector3.Distance(points[i - 1], points[i]);
                }

                if (float.IsInfinity(Distance))
                {
                    _nodes.Clear();
                    Distance = 0;
                    error = $"Path distance overflow at node {i}.";
                    return false;
                }
                _nodes.Add(new PositionInfo(Distance, points[i]));
            }
            if (Distance > 0)
            {
                for (int i = 0; i < _nodes.Count; i++)
                {
                    _nodes[i] = new PositionInfo(_nodes[i].Percent / Distance, _nodes[i].Position);
                }
            }

            error = null;
            return true;
        }

        public Vector3 Evaluate(float progress)
        {
            if (_nodes.Count == 0)
            {
                throw new InvalidOperationException("Path has not been initialized.");
            }

            if (progress <= 0 || Distance == 0)
            {
                return _nodes[0].Position;
            }

            for (int i = 1; i < _nodes.Count; i++)
            {
                if (progress >= _nodes[i].Percent)
                {
                    continue;
                }

                float span = _nodes[i].Percent - _nodes[i - 1].Percent;
                if (span > 0)
                {
                    return Vector3.Lerp(_nodes[i - 1].Position, _nodes[i].Position, (progress - _nodes[i - 1].Percent) / span);
                }
            }
            return _nodes[_nodes.Count - 1].Position;
        }

        public static float Progress(float elapsed, float duration, MoveType type)
        {
            if (duration <= 0)
            {
                return 1;
            }

            float t = Math.Max(0, elapsed / duration);
            switch (type)
            {
                case MoveType.PingPong:
                    t %= 2;
                    return 1 - Math.Abs(t - 1);
                case MoveType.Loop:
                    return t % 1;
                case MoveType.Once:
                    return Math.Min(t, 1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
