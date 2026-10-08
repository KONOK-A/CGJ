#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SnakeInputControlTests
{
    [Test]
    public void InputDisableIsWritableAndGatesPlayerInput()
    {
        var property = typeof(SnakeHead).GetProperty("InputDisable", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsTrue(property != null && property.CanWrite, "InputDisable must be settable to temporarily disable control.");

        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Games/MainModule/Views/Player/Segments/Snake.prefab");
        Assert.NotNull(prefab);
        var snake = Object.Instantiate(prefab).GetComponent<SnakeHead>();
        try
        {
            var playerInput = typeof(SnakeHead).GetMethod("PlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
            var inputX = typeof(SnakeHead).GetField("_inputX", BindingFlags.Instance | BindingFlags.NonPublic);
            var inputY = typeof(SnakeHead).GetField("_inputY", BindingFlags.Instance | BindingFlags.NonPublic);

            inputX.SetValue(snake, 1f);
            inputY.SetValue(snake, 1f);
            snake.InputDisable = true;
            Assert.IsTrue(snake.InputDisable);
            playerInput.Invoke(snake, null);
            Assert.AreEqual(0f, (float)inputX.GetValue(snake), "Disabled input must be zeroed.");
            Assert.AreEqual(0f, (float)inputY.GetValue(snake), "Disabled input must be zeroed.");

            snake.InputDisable = false;
            Assert.IsFalse(snake.InputDisable);
        }
        finally
        {
            Object.DestroyImmediate(snake.gameObject);
        }
        LogAssert.NoUnexpectedReceived();
    }
}
#endif
