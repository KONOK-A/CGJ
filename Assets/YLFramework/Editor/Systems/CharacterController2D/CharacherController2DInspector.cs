using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Character.Controller2D
{
    [CustomEditor(typeof(CharacterController2D))]
    public class CharacherController2DInspector : UnityEditor.Editor
    {

        private SerializedProperty autoRotation = null;
        private SerializedProperty showDebugLine = null;
        private SerializedProperty MaxJumpCount = null;
        private SerializedProperty isCheckGround = null;
        private SerializedProperty isCheckWall = null;
        private SerializedProperty isCheckTop = null;
        private SerializedProperty gravityScale = null;
        private SerializedProperty MaxFallingVelocity = null;
        private SerializedProperty groundAngle = null;
        private SerializedProperty wallAngle = null;
        private SerializedProperty topAngle = null;
        private SerializedProperty onGroundValueChange = null;
        private SerializedProperty onTouchWallValueChange = null;
        private SerializedProperty onTopValueChange = null;
        private SerializedProperty onVelocityValueChange = null;
        private SerializedProperty onJumpCountValueChange = null;


        private CharacterController2D characher;

        private bool foldout = true;

        private FieldInfo checkCountInfo = null;

        private void OnEnable()
        {
            autoRotation = serializedObject.FindProperty("autoRotation");
            showDebugLine = serializedObject.FindProperty("showDebugLine");
            MaxJumpCount = serializedObject.FindProperty("MaxJumpCount");
            isCheckGround = serializedObject.FindProperty("isCheckGround");
            isCheckWall = serializedObject.FindProperty("isCheckWall");
            isCheckTop = serializedObject.FindProperty("isCheckTop");
            gravityScale = serializedObject.FindProperty("gravityScale");
            MaxFallingVelocity = serializedObject.FindProperty("MaxFallingVelocity");
            groundAngle = serializedObject.FindProperty("groundAngle");
            wallAngle = serializedObject.FindProperty("wallAngle");
            topAngle = serializedObject.FindProperty("topAngle");
            onGroundValueChange = serializedObject.FindProperty("onGroundValueChange");
            onTouchWallValueChange = serializedObject.FindProperty("onTouchWallValueChange");
            onTopValueChange = serializedObject.FindProperty("onTopValueChange");
            onVelocityValueChange = serializedObject.FindProperty("onVelocityValueChange");
            onJumpCountValueChange = serializedObject.FindProperty("onJumpCountValueChange");

            characher = target as CharacterController2D;


            checkCountInfo = characher.GetType().GetField("checkCount", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public override void OnInspectorGUI()
        {

            serializedObject.Update();

            var colliders = new List<Collider2D>();
            characher.Rigidbody.GetAttachedColliders(colliders);
            if (!colliders.Exists(c => c && !c.isTrigger && (c is BoxCollider2D || c is CircleCollider2D || c is CapsuleCollider2D)))
                EditorGUILayout.HelpBox("Rigidbody2D 缺少非 Trigger 的 Box/Circle/CapsuleCollider2D。运行时添加后需调用 RefreshColliders()。", MessageType.Error);
            if (string.IsNullOrEmpty(LayerMask.LayerToName(characher.gameObject.layer)))
                EditorGUILayout.HelpBox("当前对象使用未命名 Layer；请检查对象 Layer 与 Physics2D 碰撞矩阵。", MessageType.Warning);

            EditorGUILayout.PropertyField(autoRotation);
            EditorGUILayout.PropertyField(showDebugLine);
            EditorGUILayout.PropertyField(MaxJumpCount);
            EditorGUILayout.PropertyField(isCheckGround);
            if (isCheckGround.boolValue)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.BeginVertical();

                EditorGUILayout.PropertyField(gravityScale);
                EditorGUILayout.PropertyField(groundAngle);
                EditorGUILayout.PropertyField(MaxFallingVelocity);
                EditorGUILayout.PropertyField(onGroundValueChange);

                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            }



            EditorGUILayout.PropertyField(isCheckWall);
            if (isCheckWall.boolValue)
            {


                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.BeginVertical();

                EditorGUILayout.PropertyField(wallAngle);
                EditorGUILayout.PropertyField(onTouchWallValueChange);

                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

            }


            EditorGUILayout.PropertyField(isCheckTop);
            if (isCheckTop.boolValue)
            {

                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.BeginVertical();

                EditorGUILayout.PropertyField(topAngle);
                EditorGUILayout.PropertyField(onTopValueChange);

                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

            }

            EditorGUILayout.PropertyField(onVelocityValueChange);
            EditorGUILayout.PropertyField(onJumpCountValueChange);

            serializedObject.ApplyModifiedProperties();

            DrawResult(ref foldout);
        }



        public void DrawResult(ref bool resultFoldout)
        {
            resultFoldout = EditorGUILayout.Foldout(resultFoldout, "Infos");
            if (resultFoldout)
            {
                var oldColor = GUI.color;
                GUI.color = Color.gray;


                EditorGUILayout.LabelField("IsGrounded:", characher.IsGrounded.ToString());
                EditorGUILayout.LabelField("IsTouchWall:", characher.IsTouchWall.ToString());
                EditorGUILayout.LabelField("IsTop:", characher.IsTop.ToString());
                EditorGUILayout.LabelField("Velocity:", characher.Velocity.ToString());

                EditorGUILayout.LabelField("CurrentJumpCount:", characher.CurrentJumpCount.ToString());


                int checkCount = (int)checkCountInfo.GetValue(characher);
                EditorGUILayout.LabelField("每秒射线检测次数:", checkCount.ToString());
                GUI.color = oldColor;
            }
        }



    }

}
