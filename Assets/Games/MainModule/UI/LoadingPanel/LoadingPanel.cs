using System;
using UnityEngine;
using UnityEngine.UI;
using XFGameFramework;

public class LoadingPanel : Panel
{
   private Text _textProgress;
   private float _targetProgress;
   private float _currentProgress;
   public bool IsDone => _currentProgress >= 1;
   protected override void Awake()
   {
      base.Awake();
      _textProgress = transform.Find("Text").GetComponent<Text>();
   }

   public override void OnLoaded(params object[] param)
   {
      base.OnLoaded(param);
      _targetProgress = _currentProgress = 0;
   }

   public void UpdateProgress(float progress)
   {
      _targetProgress = progress;
   }

   private void Update()
   {
      if (_currentProgress < _targetProgress)
      {
         _currentProgress += Time.deltaTime;
      }

      _textProgress.text = $"Loading...{(int)Mathf.Lerp(0,100,_currentProgress)}%";
   }
}
