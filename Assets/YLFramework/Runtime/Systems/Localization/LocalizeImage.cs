using UnityEngine;
using YLFramework.Resource;

namespace YLFramework.Localization
{
    [RequireComponent(typeof(ResourceImage))]
    [AddComponentMenu("YLFramework/Localization/Localize Image")]
    public class LocalizeImage : LocalizeBase
    {
        private ResourceImage _loader;
        protected override void OnLanguageValueChange()
        {
            if (!_loader)
            {
                _loader = GetComponent<ResourceImage>();
            }

            if (!_loader)
            {
                return;
            }

            var entry = LocalizationManager.GetLanguageInfo(Id);
            if (entry == null)
            {
                _loader.Clear();
                return;
            }
            if (entry.type == MultiLanguageType.Image && entry.image != null)
            {
                _loader.SetImage(new ResourceAddress(entry.image.projectName, entry.image.imageName,
                    string.IsNullOrEmpty(entry.image.subAsset) ? null : entry.image.subAsset));
            }
            else if (entry.type == MultiLanguageType.NetworkImage)
            {
                _loader.SetImage(entry.url);
            }
            else
            {
                _loader.Clear();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_loader)
            {
                _loader.Clear();
            }
        }
    }
}
