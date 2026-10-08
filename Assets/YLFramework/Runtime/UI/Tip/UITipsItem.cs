using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Services.UI
{
    public class UITipsItem : MonoBehaviour
    {
        [SerializeField] private Image bg;
        [SerializeField] private Text text;
        private Sprite _originalSprite;
        private Color _originalTextColor;
        private float _originalBgAlpha;

        public void OnInit(string info, Action callback, Color textColor, float showTime = 2f, bool isFade = true,
            Sprite sprite = null)
        {
            text.text = info;
            _originalSprite = bg.sprite;
            _originalTextColor = text.color;
            if (sprite != null)
            {
                bg.sprite = sprite;
            }

            if (textColor != default)
            {
                text.color = textColor;
            }

            //TODO:显现出来
            //DoShow(showTime, isFade, callback).ToAction().StartCurrentScene();
        }

        public void OnReset()
        {
            text.text = "";
            bg.sprite = _originalSprite;
            text.color = _originalTextColor;
            this.Hide();
        }

        private IEnumerator DoShow(float showTime, bool isFade, Action callback)
        {
            if (showTime <= 0)
            {
                yield break;
            }

            var bgColor = bg.color;
            bgColor.a = 0;
            var textColor = text.color;
            textColor.a = 0;

            bg.color = bgColor;
            text.color = textColor;

            this.Show();

            if (isFade)
            {
                while (bgColor.a < 1)
                {
                    yield return null;
                    bgColor.a += Time.deltaTime / (showTime * 0.25f);
                    textColor.a += Time.deltaTime / (showTime * 0.25f);
                    bg.color = bgColor;
                    text.color = textColor;
                }

                yield return new WaitForSeconds(showTime * 0.5f);
                while (bgColor.a > 0)
                {
                    yield return null;
                    bgColor.a -= Time.deltaTime / (showTime * 0.25f);
                    textColor.a -= Time.deltaTime / (showTime * 0.25f);
                    bg.color = bgColor;
                    text.color = textColor;
                }
            }
            else
            {
                bgColor.a = 1;
                textColor.a = 1;
                bg.color = bgColor;
                text.color = textColor;
                yield return new WaitForSeconds(showTime);
                bgColor.a = 0;
                textColor.a = 0;
                bg.color = bgColor;
                text.color = textColor;
            }

            callback?.Invoke();
        }
    }
}