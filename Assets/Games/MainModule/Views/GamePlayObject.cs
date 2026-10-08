
using UnityEngine;
using XFGameFramework;

public abstract class GamePlayObject : View
{
    protected abstract Collider2D Collider2D { get; }
    private void OnBecameVisible()
    {
        Collider2D.enabled = true;
    }

    private void OnBecameInvisible()
    {
        Collider2D.enabled = false;
    }
}