﻿using RoR2;
using EntityStates;
using UnityEngine;
using System.Linq;
using EntityStates.Treebot.Weapon;
using EggsBuffs;
using EggsSkills.Config;

namespace EggsSkills.EntityStates
{
    class DirectiveRoot : BaseSkillState
    {
        private bool cappedAttackspeed = Configuration.GetConfigValue<bool>(Configuration.TreebotPullSpeedcap);
        private bool isCrit;
        private bool isFirstPress;

        private float barrierCoefficient = 0.03f;
        private float basePullTimer = 1f;
        private float baseRadius = Configuration.GetConfigValue<float>(Configuration.TreebotPullRange);
        private float damageCoefficient = 2.5f;
        private float maxAttackSpeedMod = 4f;
        private float pullTimerModifier;
        private float pullTimer;
        private float speedFraction = 0.7f;

        private GameObject bodyPrefab = UnityEngine.Resources.Load<GameObject>("prefabs/effects/impacteffects/TreebotPounderExplosion");
        public override void OnEnter()
        {
            if (base.isAuthority)
            {
                base.OnEnter();
                float[] getMin = new float[] {this.maxAttackSpeedMod, base.attackSpeedStat};
                this.pullTimerModifier = this.cappedAttackspeed ? getMin.Min() : base.attackSpeedStat;
                base.characterMotor.walkSpeedPenaltyCoefficient = this.speedFraction;
                base.characterBody.AddBuff(BuffsLoading.buffDefAdaptive);
                this.isFirstPress = true;
                this.pullTimer = this.basePullTimer / this.pullTimerModifier;
            }
        }
        public override void OnExit()
        {
            base.characterBody.RemoveBuff(BuffsLoading.buffDefAdaptive);
            base.characterMotor.walkSpeedPenaltyCoefficient = 1f;
            base.OnExit();
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.isAuthority && !base.IsKeyDownAuthority() && this.isFirstPress)
            {
                this.isFirstPress = false;
            }
            if (base.isAuthority && base.IsKeyDownAuthority() && !this.isFirstPress)
            {
                this.outer.SetNextStateToMain();
                return;
            }
            else if(base.isAuthority && !base.characterMotor.isGrounded)
            {
                this.outer.SetNextStateToMain();
                return;
            }
            else if(base.isAuthority && base.fixedAge >= 8)
            {
                this.outer.SetNextStateToMain();
                return;
            }
            if (this.pullTimer > 0)
            {
                this.pullTimer -= Time.fixedDeltaTime;
            }
            else
            {
                this.pullTimer = this.basePullTimer / this.pullTimerModifier;
                this.Pull();
            }
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        { 
            return InterruptPriority.Skill;
        }
        public void Pull()
        {
            this.isCrit = base.RollCrit();
            foreach (HurtBox hurtBox in new SphereSearch
            {
                origin = base.characterBody.corePosition,
                radius = this.baseRadius,
                mask = LayerIndex.entityPrecise.mask
            }.RefreshCandidates().FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(base.teamComponent.teamIndex)).OrderCandidatesByDistance().FilterCandidatesByDistinctHurtBoxEntities().GetHurtBoxes())
            {
                //force calc
                CharacterBody body = hurtBox.healthComponent.body;
                Vector3 a = hurtBox.transform.position - base.characterBody.corePosition;
                float magnitude = a.magnitude;
                Vector3 direction = a.normalized;
                float mass = body.GetComponent<Rigidbody>().mass;
                float massEval;
                if (!body.isFlying)
                {
                    massEval = mass * -20f - 400f;
                }
                else
                {
                    massEval = (mass * -20f - 400f) / 2;
                }
                float[] maxMass = new float[] { massEval, -6000 };
                Vector3 appliedForce = maxMass.Max() * direction * ((magnitude + 15) / (this.baseRadius * 2));
                //damage
                if (base.isAuthority)
                {
                    DamageInfo damageInfo = new DamageInfo
                    {
                        attacker = base.gameObject,
                        inflictor = base.gameObject,
                        crit = this.isCrit,
                        damage = base.damageStat * this.damageCoefficient,
                        damageColorIndex = DamageColorIndex.Default,
                        damageType = DamageType.Stun1s,
                        force = appliedForce,
                        procCoefficient = 0.4f,
                        procChainMask = default,
                        position = hurtBox.transform.position
                    };
                    hurtBox.healthComponent.TakeDamage(damageInfo);
                    GlobalEventManager.instance.OnHitEnemy(damageInfo, body.gameObject);
                    base.healthComponent.AddBarrier(base.healthComponent.fullCombinedHealth * this.barrierCoefficient);
                }
            }
            base.PlayAnimation("Gesture", "LightImpact");
            EffectData bodyEffectData = new EffectData
            {
                origin = base.characterBody.footPosition,
                color = Color.green,
                scale = 30
            };
            EffectManager.SpawnEffect(bodyPrefab, bodyEffectData, true);
            Util.PlaySound(FireMortar.fireSoundString, base.gameObject);
        }
    }
}
