using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class FSMAboutWindow : EditorWindow
    {
        private void OnGUI()
        {
            EditorGUILayout.LabelField("YL Asset FSM", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Based on XFFSM 1.5.4, revision b06333c2cdc801fcc401f14c252853bf099aa1b7", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Copyright (c) 2021 弦小风. MIT License.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox("Runtime 和图编辑能力独立于现有轻量 FSM。完整原许可及局部替换说明随 Runtime/Systems/StateMachine/ThirdPartyNotices.txt 分发。", MessageType.Info);
            if (GUILayout.Button("上游源码 (XFFSM)")) Application.OpenURL("https://gitee.com/xianfengkeji/xffsm");
        }
    }
}
