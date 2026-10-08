using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using XFGameFramework;

public class GamePanel : Panel
{
   public Text timeText;
   public Text segCountText;
   public Text headHpText;
   public Text headSpeedText;
   public Text enemyCountText;
   private int _seconds;
   private float _timer;
   private SnakeHead _snakeHead;
   private readonly StringBuilder _stringBuilder = new();

   public override void OnLoaded(params object[] param)
   {
      base.OnLoaded(param);
      _seconds = 0;
      _timer = 0;
      _stringBuilder.Clear();
   }

   public GamePanel SetPlayer(SnakeHead snakeHead)
   {
      _snakeHead = snakeHead;
      return this;
   }
   

   public void Update()
   {
      _timer += Time.deltaTime;
      if (_timer >= 1)
      {
         _seconds += 1;
         _stringBuilder.Clear();
         _stringBuilder.Append("已存活时间: ");
         _stringBuilder.Append($"{(_seconds / 60).ToString()}分");
         _stringBuilder.Append($"{(_seconds % 60).ToString()}秒");
         timeText.text = _stringBuilder.ToString();
         _timer = 0;
      }
      if(_snakeHead != null)
      {
         segCountText.text = $"身体长度: {_snakeHead.Segments.Count.ToString()}节";
         headHpText.text = $"蛇头生命值: {_snakeHead.Hp.ToString(CultureInfo.InvariantCulture)}";
         headSpeedText.text = $"蛇头速度: {_snakeHead.Speed:F1}";
      }

      enemyCountText.text = $"当前场景内敌人数量: {Module.LoadController<GameController>().CurrentEnemyCount.ToString()}";
   }

   public void OnBtnPauseClicked()
   {
      Module.LoadPanel<PausePanel>();
   }
}
