using UnityEngine;
using YLFramework.Services.UI;

namespace YLFramework.Samples.ColorSelector
{

    public class TestColorSelector : MonoBehaviour
    {
        public GameObject Cube;

        private void Start()
        {
            UIColorSelectorWindow colorSelectorWindow = UIManager.Instance.Open<UIColorSelectorWindow>();
            Cube.GetComponent<MeshRenderer>().material.color = colorSelectorWindow.GetColor();// 主动获取
            colorSelectorWindow.Init(OnColorSelected, Color.white);    // 事件获取

        }

        private void OnColorSelected(Color obj)
        {
            Cube.GetComponent<MeshRenderer>().material.color = obj;
        }

    }
}
