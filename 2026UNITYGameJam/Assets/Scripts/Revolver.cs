using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BulletType
{
    Standard,
    Jackpot
}

public class Revolver : MonoBehaviour
{
    [SerializeField] PlayerCore player;

    [SerializeField] private int maxAmmoCt = 6;
    [SerializeField] private float bulletRad = 0.5f;
    [SerializeField] private Transform bulletOrigin;
    [SerializeField] private LayerMask enemyLayer;
    private Stack<BulletType> ammo;
    private int curAmmoCt => ammo.Count;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Fill up the chamber
        ammo = new Stack<BulletType>();
        while(curAmmoCt < maxAmmoCt) ammo.Push(BulletType.Standard);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void Shoot()
    {
        // If no ammo is availible do sm to alert the player and return
        if(curAmmoCt == 0)
        {
            return;
        }
        // Shoot the shot at the top of the stack
        DelegateBulletEffect(ammo.Pop());
    }

    private void DelegateBulletEffect(BulletType bullet)
    {
        switch (bullet)
        {
            case BulletType.Standard:
                ShootStandard();
                break;
        }
    }

    private void Reload()
    {

    }

    #region Bullet type logic
    /// <summary>
    /// Shoots a standard shot and returns the object hit
    /// </summary>
    /// <returns></returns>
    private GameObject ShootStandard()
    {
        if(Physics.SphereCast(bulletOrigin.position, bulletRad, player.transform.forward, out RaycastHit hit))
        {
            
        }

        return null;
    }

    #endregion

    #region input endpoints
    public void OnShoot(InputAction.CallbackContext ctx)
    {
        if(ctx.started) Shoot();
    }

    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (ctx.started) Reload();
    }
    #endregion
}
