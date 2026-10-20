using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YLFramework.BuffSystem;
using YLFramework.MissionSystem;
using YLFramework.SkillSystem;

namespace YLFramework.Tests.DomainAcceptance.Editor
{
    public static class DomainAcceptanceAssets
    {
        public static string Create(string folder, string prefix, string run, string backend, bool expectLegacyLogger)
        {
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains("..") || File.Exists(folder) || Directory.Exists(folder) ||
                !Regex.IsMatch(prefix, "^[A-Za-z0-9_-]+$") || !Regex.IsMatch(run, "^[a-z0-9-]+$") || (backend != "Mono2x" && backend != "IL2CPP"))
                throw new InvalidOperationException("New receipt-owned fixture path/Run/backend required.");
            Ensure(folder + "/Resources/" + prefix);
            string resources = folder + "/Resources/" + prefix;
            var skills = ScriptableObject.CreateInstance<SkillsConfig>(); skills.name = "Skills";
            var skill = ScriptableObject.CreateInstance<SkillConfigData>();
            skill.id = 100; skill.duration = 0.02f; skill.cooling_time = 0.02f;
            skill.skill_class_full_name = "YLFramework.Tests.DomainAcceptance.ReceiverSkill"; skill.parent = skills; skills.skills.Add(skill);
            AssetDatabase.CreateAsset(skills, resources + "/Skills.asset"); AssetDatabase.AddObjectToAsset(skill, skills);
            var buffs = ScriptableObject.CreateInstance<BuffsConfig>(); buffs.name = "Buffs";
            var buff = ScriptableObject.CreateInstance<BuffConfigData>();
            buff.buff_id = 100; buff.duration = 0.02f; buff.buff_class_full_name = "YLFramework.Tests.DomainAcceptance.ReceiverBuff";
            buff.parent = buffs; buffs.buffs.Add(buff);
            AssetDatabase.CreateAsset(buffs, resources + "/Buffs.asset"); AssetDatabase.AddObjectToAsset(buff, buffs);
            var missions = ScriptableObject.CreateInstance<MissionsConfig>(); missions.name = "Missions";
            missions.param.Add(new MissionParameter { name = "amount", type = MissionParamType.Float });
            var mission = ScriptableObject.CreateInstance<MissionConfigData>();
            mission.id = 100; mission.mission_type_class_fullname = "YLFramework.Tests.DomainAcceptance.ReceiverMission";
            mission.conditions.Add(new MissionCondition { param_name = "amount", compareType = CompareType.GreaterEqual, target = 1 });
            mission.rewards.Add(new RewardInfo { id = 7, count = 2 }); mission.parent = missions; missions.missions.Add(mission);
            AssetDatabase.CreateAsset(missions, resources + "/Missions.asset"); AssetDatabase.AddObjectToAsset(mission, missions);
            foreach (var asset in new ScriptableObject[] { skills, skill, buffs, buff, missions, mission }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(skills); AssetDatabase.SaveAssetIfDirty(buffs); AssetDatabase.SaveAssetIfDirty(missions);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("C Acceptance Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.03f, 0.08f, 0.12f);
            var probe = new GameObject("C Five Module Consumer").AddComponent<DomainAcceptanceProbe>();
            var fields = new SerializedObject(probe);
            fields.FindProperty("_resourcePrefix").stringValue = prefix; fields.FindProperty("_run").stringValue = run;
            fields.FindProperty("_expectedBackend").stringValue = backend; fields.FindProperty("_expectLegacyLogger").boolValue = expectLegacyLogger;
            fields.ApplyModifiedPropertiesWithoutUndo();
            string scenePath = folder + "/Acceptance.unity";
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Could not save the owned acceptance scene.");
            return scenePath;
        }

        private static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Ensure(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path)))) throw new IOException("Cannot create owned fixture folder: " + path);
        }
    }
}
