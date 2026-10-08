using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using XFGameFramework;

public class GameSettingPanel : Panel
{
    public Button btnEnterGame;
    public InputField generateParamInputField;
    public InputField maxEnemyGenerateCountInputField;
    public InputField maxEnemyCountInputField;
    public InputField hpAddParamInputField;
    private GameController GameController => Module.LoadController<GameController>();
    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        btnEnterGame.interactable = false;
    }

    private void Update()
    {
        if (GameController.HpAddParam != 0 && GameController.MaxEnemyCount != 0 &&
            GameController.MaxEnemyGenerateCount != 0 && GameController.GenerateParam != 0)
        {
            btnEnterGame.interactable = true;
        }
        else
        {
            btnEnterGame.interactable = false;
        }
    }

    public void OnGenerateParamInputFieldSubmit(string str)
    {
        if(str.IsNullOrEmpty()) return;
        float value = float.Parse(str);
        if (value is < 1 or > 2)
        {
            value = Mathf.Clamp(value, 1, 2);
            generateParamInputField.text = value.ToString(CultureInfo.InvariantCulture);
        }
        GameController.SetGenerateParam(value);
    }
    public void OnGenerateParamInputFieldEndEdit(string str)
    {
        if(str.IsNullOrEmpty()) return;
        float value = float.Parse(str);
        if (value is < 1 or > 2)
        {
            value = Mathf.Clamp(value, 1, 2);
            generateParamInputField.text = value.ToString(CultureInfo.InvariantCulture);
        }
        GameController.SetGenerateParam(value);
    }
    public void OnMaxEnemyGenerateCountInputFieldSubmit(string str)
    {
        if(str.IsNullOrEmpty()) return;
        int value = int.Parse(str);
        if (value is < 150 or > 240)
        {
            value = Mathf.Clamp(value, 150, 240);
            maxEnemyGenerateCountInputField.text = value.ToString();
        }
        GameController.SetMaxEnemyGenerateCount(value);
    }
    public void OnMaxEnemyGenerateCountInputFieldEndEdit(string str)
    {
        if(str.IsNullOrEmpty()) return;
        int value = int.Parse(str);
        if (value is < 150 or > 240)
        {
            value = Mathf.Clamp(value, 150, 240);
            maxEnemyGenerateCountInputField.text = value.ToString();
        }
        GameController.SetMaxEnemyGenerateCount(value);
    }
    public void OnMaxEnemyCountInputFieldSubmit(string str)
    {
        if(str.IsNullOrEmpty()) return;
        int value = int.Parse(str);
        if (value is < 150 or > 300)
        {
            value = Mathf.Clamp(value, 150, 300);
            maxEnemyCountInputField.text = value.ToString();
        }
        GameController.SetMaxEnemyCount(value);
    }
    public void OnMaxEnemyCountInputFieldEndEdit(string str)
    {
        if(str.IsNullOrEmpty()) return;
        int value = int.Parse(str);
        if (value is < 150 or > 300)
        {
            value = Mathf.Clamp(value, 150, 300);
            maxEnemyCountInputField.text = value.ToString();
        }
        GameController.SetMaxEnemyCount(value);
    }
    public void OnHpAddParamInputFieldSubmit(string str)
    {
        if(str.IsNullOrEmpty()) return;
        float value = float.Parse(str);
        if (value is < 0.01f or > 0.05f)
        {
            value = Mathf.Clamp(value, 0.01f, 0.05f);
            hpAddParamInputField.text = value.ToString(CultureInfo.InvariantCulture);
        }
        GameController.SetHpAddParam(value);
    }
    public void OnHpAddParamInputFieldEndEdit(string str)
    {
        if(str.IsNullOrEmpty()) return;
        float value = float.Parse(str);
        if (value is < 0.01f or > 0.05f)
        {
            value = Mathf.Clamp(value, 0.01f, 0.05f);
            hpAddParamInputField.text = value.ToString(CultureInfo.InvariantCulture);
        }
        GameController.SetHpAddParam(value);
    }

    public void OnBtnBackToMenu()
    {
        Module.LoadController<GameController>().SetGameState(GameState.Start);
    }

    public void OnBtnEnterGame()
    {
        Module.LoadController<GameController>().SetGameState(GameState.Game);
    }
}
