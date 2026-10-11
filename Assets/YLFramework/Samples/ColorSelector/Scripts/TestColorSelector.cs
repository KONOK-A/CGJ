using System;
using System.Collections;
using UnityEngine;
using YLFramework.Core;

namespace YLFramework.Samples.ColorSelector
{

    public class TestColorSelector : MonoBehaviour
    {
        [SerializeField] private GameObject windowPrefab;
        [SerializeField] private Canvas uiRoot;
        [SerializeField] private Renderer preview;
        [SerializeField] private Color initialColor = Color.white;
        private SampleModule owner;
        private StartUpModuleRequest startup;
        private Material originalMaterial;
        private Material previewMaterial;
        private sealed class SampleModule : YLFramework.Core.Module
        {
            public override void OnStart() { }
        }

        private IEnumerator Start()
        {
            if (!windowPrefab) throw new InvalidOperationException("Bind the formal Prefabs/ColorSelector.prefab.");
            owner = new SampleModule();
            startup = ModuleManager.StartUpModule(owner);
            yield return startup;
            if (!this) yield break;
            if (!string.IsNullOrEmpty(startup.error)) throw new InvalidOperationException("Color selector sample startup failed: " + startup.error);
            if (preview)
            {
                originalMaterial = preview.sharedMaterial;
                previewMaterial = new Material(originalMaterial);
                preview.sharedMaterial = previewMaterial;
            }
            var windows = new WindowCatalog();
            windows.Register(new WindowDefinition("color", windowPrefab)
            {
                Modal = true,
                UIType = uiRoot && uiRoot.gameObject.scene.name == "DontDestroyOnLoad" ? UIType.DontDestroyUI : UIType.UI
            });
            var window = windows.Open<ColorSelectorWindow>(owner, "color", uiRoot ? uiRoot.transform : null,
                ColorSelectorWindow.Arguments(initialColor, OnColorSelected));
            OnColorSelected(window.CurrentColor);
        }

        private void OnColorSelected(Color color)
        {
            if (previewMaterial) previewMaterial.color = color;
        }

        private void OnDestroy()
        {
            try
            {
                if (startup != null && !startup.isDone) startup.Abort();
                if (owner != null && owner.State == ModuleState.Running) owner.Close();
            }
            finally
            {
                if (preview && preview.sharedMaterial == previewMaterial) preview.sharedMaterial = originalMaterial;
                if (previewMaterial) Destroy(previewMaterial);
            }
        }

    }
}
