﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(WeaponGraphics))]
public class Gun : NetworkBehaviour
{
    public int reloadingSoundIndex;
    public int shootingSoundIndex;
    public bool isAuto;
    public float rateOfFire;
    public float damage;
    public float range;
    public float bulletPenetration; // 0 - 100
    public float reloadTime;

    [SyncVar]
    public int clipMaxAmmo;
    [SyncVar]
    public int reserveMaxAmmo;

    [HideInInspector]
    [SyncVar]
    public int clipAmmo;

    [HideInInspector]
    [SyncVar]
    public int reserveAmmo;

    [HideInInspector]
    [SyncVar]
    public bool isReloading = false;

    private float nextTimeToFire = 0;

    [HideInInspector]
    public GameObject cam;

    [HideInInspector]
    public Player_Network gunOwner;


    [ServerCallback]
    void Awake()
    {
        clipAmmo = clipMaxAmmo;
        reserveAmmo = reserveMaxAmmo;
    }

    void OnEnable()
    {
        isReloading = false;
    }

    [Client]
    public void Shoot()
    {
        if (gunOwner == null || isReloading)
        {
            return;
        }

        int connectionId = gunOwner.GetComponent<Player_Network>().GetComponent<NetworkIdentity>().connectionToClient.connectionId;

        // Limit rate of fire
        if (Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1.0f / rateOfFire;
        }
        else
        {
            return;
        }

        if (CheckForReload())
        {
            return;
        }

        // Decrease clip ammo by 1 bullet
        clipAmmo -= 1;

        // Play shooting sound
        gunOwner.soundManager.PlaySound(connectionId, shootingSoundIndex, transform.position, 0.15f);

        // Show muzzle flash
        gunOwner.weaponManager.MuzzleFlash(connectionId);
        RaycastHit[] hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, range);

        // Sort hits
        hits = hits.OrderBy(x => Vector2.Distance(cam.transform.position, x.transform.position)).ToArray();

        for (int i = 0; i < hits.Length; i++)
        {
            gunOwner.weaponManager.HitEffect(connectionId, hits[i].point, hits[i].normal);
            if (hits[i].transform.gameObject.tag == "Enemy")
            {
                float calculatedDamage = damage * (Mathf.Pow(bulletPenetration / 100, i));
                gunOwner.weaponManager.DealDamage(connectionId, hits[i].transform.gameObject, calculatedDamage);
                Debug.Log(hits[i].transform.name + "hit for " + calculatedDamage);
                Debug.Log(hits[i].transform.gameObject.GetComponent<Health>().currentHealth);
            }
        }

        CheckForReload();
    }

    public IEnumerator Reload()
    {
        // If no ammo
        if (reserveAmmo <= 0)
        {
            Debug.Log("Out of ammo");
            yield break;
        }

        // If full ammo
        if (clipAmmo == clipMaxAmmo)
        {
            Debug.Log("Already full ammo");
            yield break;
        }

        isReloading = true;
        // Play reloading sound
        gunOwner.soundManager.PlaySound(gunOwner.GetComponent<Player_Network>().GetComponent<NetworkIdentity>().connectionToClient.connectionId, reloadingSoundIndex, transform.position, 0.15f);

        Debug.Log("Reloading...");

        yield return new WaitForSeconds(reloadTime);

        int oldAmmoCount = clipAmmo;

        clipAmmo = Mathf.Min(clipMaxAmmo, reserveAmmo + oldAmmoCount);
        reserveAmmo = Mathf.Max(reserveAmmo + oldAmmoCount - clipMaxAmmo, 0);
        isReloading = false;
    }

    bool CheckForReload()
    {
        // Out of clip ammo
        if (clipAmmo <= 0)
        {
            // Attempt reload here
            gunOwner.CmdReloadGun(gameObject);
            return true;
        }
        return false;
    }
}
