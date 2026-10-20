// Derived from XF Localization 1.0.9. Copyright (c) 2024 弦风课堂. MIT; see LICENSE.txt in this directory.
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Localization
{
    [RequireComponent(typeof(ResourceImage))]
    [AddComponentMenu("YLFramework/Localization/Localize Image")]
    public class LocalizeImage : LocalizeBase
    {
        private ResourceImage loader;
        protected override void OnLanguageValueChange()
        {
            if (!loader) loader = GetComponent<ResourceImage>();
            if (!loader) return;
            var entry = LocalizationManager.GetLanguageInfo(ID);
            if (entry == null) { loader.Clear(); return; }
            if (entry.type == MultiLanguageType.Image && entry.image != null)
                loader.SetImage(new ResourceAddress(entry.image.projectName, entry.image.imageName,
                    string.IsNullOrEmpty(entry.image.subAsset) ? null : entry.image.subAsset));
            else if (entry.type == MultiLanguageType.NetworkImage) loader.SetImage(entry.url);
            else loader.Clear();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (loader) loader.Clear();
        }
    }
}
