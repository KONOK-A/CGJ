using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Help
{
    /// <summary>标题区各控件按窗口宽度算出的矩形布局；所有位置在构造时一次确定。</summary>
    internal readonly struct HelpHeaderLayout
    {
        internal readonly Rect Title, Module, Back, Tabs, Search, Clear, Scope;
        internal readonly float BodyY;

        internal HelpHeaderLayout(float width)
        {
            Title = new Rect(12, 10, 100, 32);
            Back = new Rect(width - 92, Title.y, 80, Title.height);
            Module = new Rect(Title.xMax + 10, Title.y, Mathf.Max(0, Back.x - Title.xMax - 20), Title.height);
            Tabs = new Rect(12, Title.yMax + 12, width - 24, 34);
            Clear = new Rect(width - 86, Tabs.yMax + 12, 74, 30);
            Search = new Rect(12, Clear.y, Mathf.Max(0, Clear.x - 22), Clear.height);
            Scope = new Rect(12, Search.yMax + 8, width - 24, 20);
            BodyY = Scope.yMax + 10;
        }

        internal Rect Tab(int index) => new Rect(Tabs.x + Tabs.width * index / 3, Tabs.y, Tabs.width / 3, Tabs.height);
    }

    /// <summary>搜索结果高亮：匹配区间与像素矩形按文本缓存，绘制时在文本下方叠加色块。</summary>
    /// <remarks>几何缓存以文本、样式和 DPI 为键；阅读宽度变化由窗口清除缓存，尺寸不同也不会复用。</remarks>
    internal sealed class HelpSearchHighlight
    {
        internal readonly struct Range
        {
            internal readonly int Start, End;
            internal Range(int start, int end) { Start = start; End = end; }
        }

        private string mQuery;
        private string[] mTerms = Array.Empty<string>();
        private readonly Dictionary<string, Range[]> mRanges = new Dictionary<string, Range[]>();
        private readonly Dictionary<(string, GUIStyle, float), (Vector2 Size, Rect[] Fragments)> mGeometry =
            new Dictionary<(string, GUIStyle, float), (Vector2, Rect[])>();

        internal void Clear()
        {
            mQuery = null; mTerms = Array.Empty<string>(); mRanges.Clear(); mGeometry.Clear();
        }

        internal void ClearGeometry() => mGeometry.Clear();

        internal void SetQuery(string query)
        {
            // 每次绘制前都会调用；查询未变时保留已有缓存。
            if (mQuery == query) return;
            Clear(); mQuery = query;
            mTerms = (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal Range[] GetRanges(string text)
        {
            if (mTerms.Length == 0 || string.IsNullOrEmpty(text)) return Array.Empty<Range>();
            if (mRanges.TryGetValue(text, out var cached)) return cached;
            var found = new List<Range>();
            foreach (var term in mTerms)
                for (var start = text.IndexOf(term, StringComparison.OrdinalIgnoreCase); start >= 0;
                    start = text.IndexOf(term, start + 1, StringComparison.OrdinalIgnoreCase))
                    found.Add(new Range(start, start + term.Length));
            found.Sort((a, b) => a.Start.CompareTo(b.Start));
            // 合并重叠或相接的区间，避免高亮在连接处重复绘制或留缝。
            var merged = new List<Range>();
            foreach (var range in found)
            {
                if (merged.Count != 0 && range.Start <= merged[merged.Count - 1].End)
                {
                    var previous = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new Range(previous.Start, Math.Max(previous.End, range.End));
                }
                else merged.Add(range);
            }
            return mRanges[text] = merged.ToArray();
        }

        internal Rect[] GetFragments(string text, GUIStyle style, Vector2 size)
        {
            var ranges = GetRanges(text);
            if (ranges.Length == 0) return Array.Empty<Rect>();
            var key = (text, style, EditorGUIUtility.pixelsPerPoint);
            // 尺寸不参与缓存键，需要单独比较：同一文本会按不同阅读宽度绘制。
            if (mGeometry.TryGetValue(key, out var cached) && cached.Size == size) return cached.Fragments;
            var fragments = BuildFragments(text, style, new Rect(Vector2.zero, size), ranges);
            mGeometry[key] = (size, fragments);
            return fragments;
        }

        internal void Draw(Rect rect, string text, GUIStyle style, bool dark)
        {
            if (Event.current.type != EventType.Repaint) return;
            var color = dark ? new Color(0.66f, 0.46f, 0.13f, 0.42f) : new Color(0.92f, 0.65f, 0.20f, 0.36f);
            foreach (var fragment in GetFragments(text, style, rect.size))
                EditorGUI.DrawRect(new Rect(rect.position + fragment.position, fragment.size), color);
        }

        /// <summary>把字符区间转换成逐行的像素矩形；换行处不绘制，同一行的相邻片段合并。</summary>
        internal static Rect[] BuildFragments(string text, GUIStyle style, Rect rect, Range[] ranges)
        {
            var content = new GUIContent(text);
            var fragments = new List<Rect>();
            GUIStyle unwrapped = null;
            foreach (var range in ranges)
            {
                var start = style.GetCursorPixelPosition(rect, content, range.Start);
                for (var i = range.Start; i < range.End; i++)
                {
                    var end = style.GetCursorPixelPosition(rect, content, i + 1);
                    if (text[i] != '\r' && text[i] != '\n')
                    {
                        var right = end.x;
                        if (Mathf.Abs(end.y - start.y) > 0.5f)
                        {
                            // 换行边界的光标属于下一行；使用相同字体关闭自动换行，测量本行最后一个字形的宽度。
                            if (unwrapped == null) unwrapped = new GUIStyle(style) { wordWrap = false };
                            right = start.x + unwrapped.GetCursorPixelPosition(rect, content, i + 1).x -
                                unwrapped.GetCursorPixelPosition(rect, content, i).x;
                        }
                        var fragment = Rect.MinMaxRect(Mathf.Max(rect.xMin, start.x), Mathf.Max(rect.yMin, start.y),
                            Mathf.Min(rect.xMax, right), Mathf.Min(rect.yMax, start.y + style.lineHeight));
                        if (fragment.width > 0 && fragment.height > 0)
                        {
                            if (fragments.Count != 0 && Mathf.Abs(fragments[fragments.Count - 1].y - fragment.y) < 0.5f &&
                                fragment.xMin <= fragments[fragments.Count - 1].xMax + 0.5f)
                            {
                                var previous = fragments[fragments.Count - 1];
                                previous.xMax = Mathf.Max(previous.xMax, fragment.xMax);
                                fragments[fragments.Count - 1] = previous;
                            }
                            else fragments.Add(fragment);
                        }
                    }
                    start = end;
                }
            }
            return fragments.ToArray();
        }
    }
}
