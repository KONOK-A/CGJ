using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFramework;
/// <summary>
/// 吃了长普通躯体
/// </summary>
public class Food : View
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<SnakeHead>(out var player))
        {
            player.Grow(SegmentType.Normal);
            Close();
        }
    }
}
