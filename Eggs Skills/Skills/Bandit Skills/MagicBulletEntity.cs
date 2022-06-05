﻿using EggsSkills.Config;
using EntityStates.Bandit2.Weapon;
using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace EggsSkills.EntityStates
{
    class MagicBulletEntity : Bandit2FirePrimaryBase
    {
        //Skills++
        public static int spp_richochetMod = 0;
        public static float spp_bounceMod = 0f;

        //We use this for referencing assets
        private Bandit2FireRifle assetRef = new Bandit2FireRifle();

        //Whether or not it crit
        private bool isCrit;

        //Damage coefficient of the skill
        private readonly float baseDamageCoef = 2f;
        //End damage of the ability
        private float damage;
        //Proc coefficient of the skill
        private readonly float procCoef = 1f;
        //What is damage multiplied by per richochet
        private readonly float richochetMod = 0.6f + spp_bounceMod;

        //How many richochets
        private readonly int maxRecursion = Configuration.GetConfigValue(Configuration.BanditMagicbulletRicochets) + spp_richochetMod;
        //Helps us track how many more times it can bounce
        private int recursion;

        //Keeps track of enemies we already hit
        private List<HurtBox> hitHurtBoxes = new List<HurtBox>();

        //Called on entry of the skillstate
        public override void OnEnter()
        {
            //Base duration of the skill + animations
            baseDuration = 1.2f;
            //Lowest possible duration of the skill
            minimumBaseDuration = 0.1f;
            //Find out if it is critting
            isCrit = base.RollCrit();
            //It hasn't recursed yet, set to 0
            recursion = 0;
            //Make player face the aimdirection
            base.StartAimMode();
            //Do standard onenter stuff
            base.OnEnter();
        }

        //Called by base onenter, we replace it with our own special bullet
        public override void FireBullet(Ray aimRay)
        {
            //Play the sound
            Util.PlaySound(assetRef.fireSoundString, gameObject);
            //Network check
            if (base.isAuthority)
            {
                //Set the damage accordingly
                damage = baseDamageCoef * base.characterBody.damage;
                //Create the bulletattack
                BulletAttack attack = new BulletAttack
                {
                    origin = aimRay.origin,
                    aimVector = aimRay.direction,
                    tracerEffectPrefab = assetRef.tracerEffectPrefab,
                    muzzleName = assetRef.muzzleName,
                    hitEffectPrefab = assetRef.hitEffectPrefab,
                    damage = damage,
                    owner = gameObject,
                    isCrit = isCrit,
                    maxDistance = 1000f,
                    smartCollision = true,
                    procCoefficient = procCoef,
                    falloffModel = BulletAttack.FalloffModel.None,
                    weapon = gameObject,
                    hitCallback = CallBack
                };
                //Fire
                attack.Fire();
            }
            //Apply the muzzleflash
            EffectManager.SimpleMuzzleFlash(assetRef.muzzleFlashPrefab, gameObject, assetRef.muzzleName, false);
        }
        
        //Event called when bullet hits anything
        private bool CallBack(BulletAttack bulletattack, ref BulletAttack.BulletHit hitInfo)
        {
            //Run the hit as normal first
            bool hit = BulletAttack.DefaultHitCallbackImplementation(bulletattack, ref hitInfo);
            //Pos is where the bullet hit
            Vector3 pos = hitInfo.point;
            //If it actually hit something, add the main hurtbox of them to our list
            if (hitInfo.hitHurtBox) hitHurtBoxes.Add(hitInfo.hitHurtBox.hurtBoxGroup.mainHurtBox);
            //Execute our richochet code
            HandleRichochet(pos);
            //Return the previously found hit / nohit
            return hit;
        }

        //What to do when attempting to richochet the bullet
        private void HandleRichochet(Vector3 pos)
        {
            //Hold variable for whether target is found or not
            bool targetFound = false;
            //If the bullet can still recurse...
            if (recursion < maxRecursion)
            {
                //Reduce the damage
                damage *= richochetMod;
                //Loop via spheresearch
                foreach (HurtBox hurtBox in new SphereSearch
                {
                    origin = pos,
                    radius = 25f,
                    mask = LayerIndex.entityPrecise.mask,
                }.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex)).OrderCandidatesByDistance().FilterCandidatesByDistinctHurtBoxEntities().GetHurtBoxes())
                {
                    //Get the mainhurtbox
                    HurtBox mainBox = hurtBox.hurtBoxGroup.mainHurtBox;
                    //If the mainhurtbox is not yet in our list...
                    if (!hitHurtBoxes.Contains(mainBox))
                    {
                        //Add the found target to the list
                        hitHurtBoxes.Add(mainBox);
                        //Trip the bool flag
                        targetFound = true;
                        //Mark that we recursed 
                        recursion += 1;
                        //Emulate the bullet, because doing another ACTUAL bullet attack is hell and a half, also feels bad to have all your shots miss cause some geometry
                        SimulateBullet(pos, mainBox);
                        //End the loop cause we found our target
                        break;
                    }
                }
                //If we go through having never found a target, max out the recursion so the loop ends completely
                if(!targetFound) recursion = maxRecursion;
            }
        }

        //Just like the simulations
        private void SimulateBullet(Vector3 pos, HurtBox box)
        {
            //Get the pos of where the bullet hit
            Vector3 origin = pos;
            //Setup effectdata
            EffectData data = new EffectData()
            {
                start = origin,
                origin = box.transform.position
            };
            //Play the effect
            EffectManager.SpawnEffect(assetRef.tracerEffectPrefab, data, true);
            //This used to be takedamage but it was unreliable lol, just explode them point blank no radius
            new BlastAttack
            {
                radius = 0.1f,
                baseDamage = damage,
                procCoefficient = procCoef,
                position = box.transform.position,
                attacker = gameObject,
                teamIndex = base.teamComponent.teamIndex,
                baseForce = 0F,
                bonusForce = Vector3.zero,
                crit = base.RollCrit(),
                falloffModel = BlastAttack.FalloffModel.None,
                losType = BlastAttack.LoSType.None,
                inflictor = base.gameObject
            }.Fire();
            //Continue to attempt richocet
            HandleRichochet(box.transform.position);
        }
    }
}
