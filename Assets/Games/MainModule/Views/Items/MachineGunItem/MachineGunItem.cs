using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFramework;

public class MachineGunItem : GamePlayObject
{
    public string equipName;
    public new Collider2D collider;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<SnakeHead>(out var player))
        {
            player.Grow(SegmentType.Functional,equipName);
            Close();
        }
    }

    protected override Collider2D Collider2D => collider;
}
