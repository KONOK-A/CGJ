using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

namespace YLFramework.Services.Resource
{
        /// <summary>
        /// 资产对象类型码表：把资源记录中的平台无关类型码（short）与 Unity 类型互转。
        /// 仅覆盖下列类型（GameObject / AudioClip / Sprite / Scene / SpriteAtlas / Mesh /
        /// Texture2D / TextAsset / AssetBundle / Animator），未收录的类型统一编码为 0。
        /// </summary>
        public static class ObjectAssetTypeCode
        {
            private const short GameObject = 1;
            private const short AudioClip = 2;
            private const short Sprite = 3;
            private const short Scene = 4;
            private const short SpriteAtlas = 5;
            private const short Mesh = 6;
            private const short Texture2D = 7;
            private const short TextAsset = 8;
            private const short AssetBundle = 9;
            private const short Animator = 10;
            
            private static readonly Type GameObjectType = typeof(GameObject);
            private static readonly Type AudioClipType = typeof(AudioClip);
            private static readonly Type SpriteType = typeof(Sprite);
            private static readonly Type SceneType = typeof(Scene);
            private static readonly Type SpriteAtlasType = typeof(SpriteAtlas);
            private static readonly Type MeshType = typeof(Mesh);
            private static readonly Type Texture2DType = typeof(Texture2D);
            private static readonly Type TextAssetType = typeof(TextAsset);
            private static readonly Type AssetBundleType = typeof(AssetBundle);
            private static readonly Type AnimatorType = typeof(Animator);
            
            /// <summary>把 Unity 类型转换为类型码；未收录的类型返回 0。</summary>
            public static short ToCode(this Type type)
            {
                if (type == GameObjectType)
                    return GameObject;
                if (type == AudioClipType)
                    return AudioClip;
                if (type == SpriteType)
                    return Sprite;
                if (type == SceneType)
                    return Scene;
                if (type == SpriteAtlasType)
                    return SpriteAtlas;
                if (type == MeshType)
                    return Mesh;
                if (type == Texture2DType)
                    return Texture2D;
                if (type == TextAssetType)
                    return TextAsset;
                if (type == AssetBundleType)
                    return AssetBundle;
                if (type == AnimatorType)
                    return Animator;
                return 0;
            }
            /// <summary>把类型码还原为 Unity 类型；0 或未知类型码返回 null。</summary>
            public static Type ToType(this short code)
            {
                return code switch
                {
                    GameObject => GameObjectType,
                    AudioClip => AudioClipType,
                    Sprite => SpriteType,
                    Scene => SceneType,
                    SpriteAtlas => SpriteAtlasType,
                    Mesh => MeshType,
                    Texture2D => Texture2DType,
                    TextAsset => TextAssetType,
                    AssetBundle => AssetBundleType,
                    Animator => AnimatorType,
                    _ => null
                };
            }
        }

        /// <summary>
        /// 资产记录：描述一个 Unity 资产（或 Bundle 文件本身）的名字、所属 Bundle、
        /// 加载方式与对象类型码；可作为二进制配置的一部分被序列化。
        /// </summary>
        [Serializable]
        public class AssetData
        {
            private string _assetName;
            private string _ownerBundleName;
            private int _abIndex;
            private short _assetResType;
            private short _assetObjectTypeCode;

            /// <summary>资产名；AssetDataGroup 建索引时会转小写。</summary>
            public string AssetName
            {
                get => _assetName;
                set => _assetName = value;
            }

            /// <summary>所属 Bundle 名；Bundle 自身的记录（AssetBundle 类型）为 null。</summary>
            public string OwnerBundleName
            {
                get => _ownerBundleName;
                set => _ownerBundleName = value;
            }
            /// <summary>
            /// 所属 Bundle 在 AssetDataGroup 内部列表中的下标（即资源数据在组内的存储序号）。
            /// </summary>
            public int AbIndex
            {
                get => _abIndex;
                set => _abIndex = value;
            }

            /// <summary>
            /// 资源的加载方式码，取值见 <see cref="ResLoadType"/>（AssetBundle / ABAsset / ABScene 等）。
            /// </summary>
            public short AssetResType
            {
                get => _assetResType;
                set => _assetResType = value;
            }
            /// <summary>
            /// 资产对象的类型码，取值见 <see cref="ObjectAssetTypeCode"/>；0 表示未收录的类型。
            /// </summary>
            public short AssetObjectTypeCode
            {
                get => _assetObjectTypeCode;
                set => _assetObjectTypeCode = value;
            }

            /// <summary>组内唯一键：OwnerBundleName + AssetName（Bundle 自身的记录即 AssetName）。</summary>
            public string Uid => string.IsNullOrEmpty(_ownerBundleName) ? AssetName : _ownerBundleName + AssetName;

            /// <summary>创建资产记录；<paramref name="assetObjectTypeCode"/> 为 0 表示类型未收录。</summary>
            public AssetData(string assetName, short assetResType, int abIndex, string ownerBundleName,
                short assetObjectTypeCode = 0)
            {
                _assetName = assetName;
                _assetResType = assetResType;
                _abIndex = abIndex;
                _ownerBundleName = ownerBundleName;
                _assetObjectTypeCode = assetObjectTypeCode;
            }

            public AssetData()
            {
                
            }
        }
        
        
}