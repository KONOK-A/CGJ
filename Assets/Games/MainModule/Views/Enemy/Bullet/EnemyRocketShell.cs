using UnityEngine;
using XFGameFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class EnemyRocketShell : Bullet
{
    public float explodeRange;
    [Tooltip("爆炸特效")]
    public string effect;
    private readonly Collider2D[] _results = new Collider2D[10]; 
    public override void Close()
    {
        base.Close();
        Direction = Vector2.zero;
        IsFlying = false;
        Speed = 0;
        Damage = 0;
        Timer = 0;
        ExistTime = 0;
    }

    private void Update()
    {
        if (IsFlying)
        {
            Timer += Time.deltaTime;
            if (Timer >= ExistTime)
            {
                //存在时间到了，销毁
                Close();
            }
        }
    }
    

    protected override void OnCollisionEnter2D(Collision2D other)
    {
        if (other.collider.transform.CompareTag("CanNotTouch"))
        {
            OnExplode();
        }
        if (other.collider.transform.CompareTag("Player"))
        {
            if(other.collider.transform.TryGetComponent<SnakeBase>(out var snake))
            {
                OnExplode();
            }
            if (other.collider.transform.TryGetComponent<PlayerShield>(out var playerShield))
            {
                playerShield.OnResumeDamage(Damage);
                //只显示特效
                Module.LoadController<EffectController>().ShowEffect(effect, transform.position);
                Module.LoadController<AudioController>().PlaySound("火箭爆炸1", AudioController.SoundPriority.Important);
                Close();
            }
        }
        
    }

    public override Bullet InitWithData(Vector2 direction,float speed,float damage,float existTime)
    {
        Speed = speed;
        Direction = direction.normalized;
        IsFlying = true;
        Damage = damage;
        ExistTime = existTime;
        return this;
    }
    private void OnExplode()
    {
        //显示特效
        Module.LoadController<EffectController>().ShowEffect(effect, transform.position);
        Module.LoadController<AudioController>().PlaySound("火箭爆炸1", AudioController.SoundPriority.Important);
        //检测周围的对象，进行攻击
        var f = new ContactFilter2D
        {
            useLayerMask = true,
        };
        f.layerMask.Physics2DSetting(gameObject.layer);
        int count = Physics2D.OverlapCircle(transform.position, explodeRange, f, _results);
        for (int i = 0; i < count; i++)
        {
            Collider2D collide = _results[i];
            if(collide == null) continue;
            if (collide.TryGetComponent<SnakeBase>(out var snake))
            {
                snake.OnDamage(Damage);
            }
        }
        //回收自己
        Close();
    }
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Handles.color = Color.red;
        Handles.DrawWireDisc(transform.position,Vector3.forward,explodeRange);
    }
#endif
}
