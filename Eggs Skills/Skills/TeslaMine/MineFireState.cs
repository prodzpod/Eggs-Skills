﻿using RoR2;
using EntityStates;
using UnityEngine;
using RoR2.Projectile;
namespace EggsSkills.EntityStates
{
    class TeslaMineFireState : BaseState
    {
        public float baseDelay = 0.4f;
        public float delay;
        public override void OnEnter()
        {
            base.OnEnter();
            delay = baseDelay/base.attackSpeedStat;
            var aimRay = GetAimRay();
            StartAimMode(aimRay);
            if(base.isAuthority)
            {
                ProjectileManager.instance.FireProjectile(EggsSkills.SkillsLoader.teslaMinePrefab,aimRay.origin,RoR2.Util.QuaternionSafeLookRotation(aimRay.direction),gameObject,damageStat * 2f,0,RollCrit());
            };
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.fixedAge >= this.delay && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
}
