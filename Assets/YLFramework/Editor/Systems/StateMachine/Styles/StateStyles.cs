using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace YLFramework.Editor.StateMachine
{
    internal enum Style
    {
        Normal = 0,
        Blue,
        Mint,
        Green,
        Yellow,
        Orange,
        Red,
        NormalOn,
        BlueOn,
        MintOn,
        GreenOn,
        YellowOn,
        OrangeOn,
        RedOn,

        NormalHEX,
        BlueHEX,
        MintHEX,
        GreenHEX,
        YellowHEX,
        OrangeHEX,
        RedHEX,
        NormalOnHEX,
        BlueOnHEX,
        MintOnHEX,
        GreenOnHEX,
        YellowOnHEX,
        OrangeOnHEX,
        RedOnHEX,
    }

    internal class StateStyles
    {
        private static Dictionary<Style, GUIStyle> _styleDictionary = null;
        static StateStyles()
        {

            _styleDictionary = new Dictionary<Style, GUIStyle>();

            foreach (Style style in Enum.GetValues(typeof(Style)))
            {
                int value = (int)style;
                string name = $"flow node {(value >= 14 ? "hex " : string.Empty)}{value % 7}{((value / 7) % 2 == 1 ? " on" : string.Empty)}";
                _styleDictionary.Add(style, new GUIStyle(Named(name, EditorStyles.helpBox)));
            }

        }

        public static GUIStyle Get(Style style)
        {
            if (_styleDictionary.ContainsKey(style))
            {
                return _styleDictionary[style];
            }

            return null;
        }

        public static GUIStyle Named(string name, GUIStyle fallback = null)
            => GUI.skin.FindStyle(name) ?? fallback ?? EditorStyles.label;

    }

}
