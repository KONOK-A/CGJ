using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XFABManager;

namespace YLFramework.Core.Editor
{
    public class EditorAssetTool 
    {


        private static Dictionary<string, Dictionary<string, Texture2D>> textures = new Dictionary<string, Dictionary<string, Texture2D>>();

        private static Dictionary<string, Dictionary<string, float>> textures_timer = new Dictionary<string, Dictionary<string, float>>();


        private static Dictionary<string, Dictionary<string, GUIContent>> textures_content = new Dictionary<string, Dictionary<string, GUIContent>>();

        public static Texture2D GetTexture2D(string projectName, string assetName)
        {
            if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(assetName))
                return null;

            if (!textures.ContainsKey(projectName))
                textures.Add(projectName, new Dictionary<string, Texture2D>());

            if (!textures[projectName].ContainsKey(assetName))
                textures[projectName].Add(assetName, null);

            if (textures[projectName].ContainsKey(assetName))
            {
                if (textures[projectName][assetName] != null)
                {
                    //Debug.Log("使用缓存");
                    return textures[projectName][assetName];
                }
                else
                {

                    float timer = GetTexture2DTimer(projectName, assetName);

                    if (Time.realtimeSinceStartup - timer < 1 && Time.realtimeSinceStartup > timer)
                    {
                        //Debug.Log("缓存为空，在1s之内，等待");
                        return null;
                    }
                    else
                    {
                        //Debug.Log("超过1s刷新");
                        SetTexture2DTimer(projectName, assetName);   // 更新一下时间
                    }
                }
            }
             
            XFABProject project = XFABProjectManager.Instance.GetProject(projectName);

            if (project != null)
            {
                List<XFABAssetBundle> bundles = project.GetAllAssetBundles();

                foreach (var bundle in bundles)
                {
                    if (bundle == null)
                    {
                        continue;
                    }

                    string asset_name = assetName;
                    string childAssetName = string.Empty;

                    if (assetName.Contains("/"))
                    {
                        string[] strs = assetName.Split('/');
                        asset_name = strs[0];
                        childAssetName = strs[1];
                    }


                    string asset_path = bundle.GetAssetPathByFileName(asset_name, typeof(Texture2D));

                    if (string.IsNullOrEmpty(asset_path))
                        asset_path = bundle.GetAssetPathByFileName(asset_name, typeof(Sprite));

                    if (string.IsNullOrEmpty(asset_path))
                        continue;

                    Texture2D texture = null;

                    if (string.IsNullOrEmpty(childAssetName))
                        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(asset_path);
                    else
                    {
                        Object[] objects = AssetDatabase.LoadAllAssetRepresentationsAtPath(asset_path);
                        foreach (var item in objects)
                        {
                            if (item.name == childAssetName)
                            {
                                if (item is Sprite || item is Texture)
                                    texture = AssetPreview.GetAssetPreview(item);

                                break;
                            }
                        }
                    }

                    if (texture != null)
                    {
                        if (textures[projectName].ContainsKey(assetName))
                            textures[projectName][assetName] = texture;
                        else
                            textures[projectName].Add(assetName, texture);

                        return texture;
                    }
                }
            }

            return null;
        }

        public static GUIContent GetTextureGUIContent(string projectName, string assetName)
        {
            
            if (projectName == null)
                projectName = string.Empty;

            if(assetName == null)
                assetName = string.Empty;

            if (!textures_content.ContainsKey(projectName))
                textures_content.Add(projectName, new Dictionary<string, GUIContent>());

            if (!textures_content[projectName].ContainsKey(assetName))
                textures_content[projectName].Add(assetName, new GUIContent());

            Texture2D icon = GetTexture2D(projectName, assetName);

            if(icon != null)
                textures_content[projectName][assetName].image = icon;
            else
                textures_content[projectName][assetName].image = EditorGUIUtility.IconContent("BuildSettings.Broadcom").image;
             
            return textures_content[projectName][assetName];
        }

        private static float GetTexture2DTimer(string projectName, string assetName) 
        {
            if (!textures_timer.ContainsKey(projectName))
                textures_timer.Add(projectName, new Dictionary<string, float>());

            if (!textures_timer[projectName].ContainsKey(assetName))
                textures_timer[projectName].Add(assetName, 0);

            return textures_timer[projectName][assetName];
        }
         
        private static void SetTexture2DTimer(string projectName, string assetName) 
        {
            if (!textures_timer.ContainsKey(projectName))
                textures_timer.Add(projectName, new Dictionary<string, float>());

            if (!textures_timer[projectName].ContainsKey(assetName))
                textures_timer[projectName].Add(assetName, 0);

            textures_timer[projectName][assetName] = Time.realtimeSinceStartup;
        }


          

    }

}
