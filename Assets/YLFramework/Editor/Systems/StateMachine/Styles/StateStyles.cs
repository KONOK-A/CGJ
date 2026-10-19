using YLFramework.Core.Editor;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;


namespace YLFramework.StateMachine
{
    public enum Style {
        Normal=0,
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

    public class StateStyles
    {
        private static Dictionary<Style, GUIStyle> styleDictionary = null;
        static StateStyles() {

            styleDictionary = new Dictionary<Style, GUIStyle>();

            foreach (Style style in Enum.GetValues(typeof(Style)))
            {
                int value = (int)style;
                string name = $"flow node {(value >= 14 ? "hex " : string.Empty)}{value % 7}{((value / 7) % 2 == 1 ? " on" : string.Empty)}";
                styleDictionary.Add(style, new GUIStyle(Named(name, EditorStyles.helpBox)));
            }

        }

        public static GUIStyle Get(Style style)
        {
            if(styleDictionary.ContainsKey(style))
                return styleDictionary[style];

            return null;
        }

        public static GUIStyle Named(string name, GUIStyle fallback = null)
            => GUI.skin.FindStyle(name) ?? fallback ?? EditorStyles.label;

    }

}
