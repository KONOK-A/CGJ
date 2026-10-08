public class Enemy_MachineGun : EnemyBase
{
    public override void OnDead()
    {
        base.OnDead();
        if (IsDropItem)
        {
            var item = Module.LoadView<MachineGunItem>();
            item.transform.position = transform.position;
        }
        Close();
    }
}
