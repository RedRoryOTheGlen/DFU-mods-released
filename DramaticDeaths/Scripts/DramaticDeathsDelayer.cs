
using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DramaticDeaths
{
    public class DramaticDeathsDelayer : MonoBehaviour
    {

        #region Fields

        MobileUnit mobile;
        EnemyMotor motor;
        DaggerfallEntityBehaviour entityBehaviour;
        EnemyEntity entity;
        EnemySounds sounds;
        EnemyDeath death;
        CharacterController controller;

        IEnumerator dying;

        bool died;

        QuestResourceBehaviour questResourceBehaviour;

        #endregion

        #region Unity

        void Awake()
        {
            entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            entityBehaviour.OnSetEntity += EntityBehaviour_OnSetEntity;
            entity = entityBehaviour.Entity as EnemyEntity;
            entityBehaviour.Entity.OnDeath += EnemyEntity_OnDeath;

            mobile = GetComponent<DaggerfallEnemy>().MobileUnit;
            motor = GetComponent<EnemyMotor>();
            sounds = GetComponent<EnemySounds>();
            controller = GetComponent<CharacterController>();

            death = GetComponent<EnemyDeath>();
            death.enabled = false;
        }

        IEnumerator PerformDeath()
        {

            Debug.Log("DRAMATIC DEATHS - Performing Death!");

            float knockbackMin = 100 * DramaticDeaths.knockbackMinScale;
            if (motor.KnockbackSpeed < knockbackMin)
                motor.KnockbackSpeed = knockbackMin;

            mobile.ChangeEnemyState(MobileStates.Hurt);

            // If enemy associated with quest system, make sure quest system is done with it first
            questResourceBehaviour = GetComponent<QuestResourceBehaviour>();
            if (questResourceBehaviour)
            {
                int tries = 0;
                while (!questResourceBehaviour.IsFoeDead && tries < 100)
                {
                    if (motor.KnockbackSpeed < knockbackMin)
                        motor.KnockbackSpeed = knockbackMin;
                    mobile.ChangeEnemyState(MobileStates.Hurt);

                    tries++;
                    yield return new WaitForEndOfFrame();
                }

                if (!questResourceBehaviour.IsFoeDead)
                {
                    dying = null;
                    yield break;
                }
            }

            // Unequip items on starting death
            // This is still required so enemy equipment is not marked as equipped
            // This item collection is transferred to loot container below
            for (int i = (int)EquipSlots.Head; i <= (int)EquipSlots.Feet; i++)
            {
                DaggerfallUnityItem item = entity.ItemEquipTable.GetItem((EquipSlots)i);
                if (item != null)
                {
                    entity.ItemEquipTable.UnequipItem((EquipSlots)i);
                }
            }

            //play a random hit sound at the point of impact
            SoundClips hit = (SoundClips)((int)SoundClips.Hit1 + Random.Range(0, 5));
            DaggerfallUI.Instance.DaggerfallAudioSource.PlayClipAtPoint(hit, transform.position, 1f);

            //play death sound on killing blow
            DaggerfallAudioSource dfAudioSource = GetComponent<DaggerfallAudioSource>();
            if (dfAudioSource)
            {
                //stop pain sound from playing
                if (dfAudioSource.IsPlaying())
                    dfAudioSource.AudioSource.Stop();

                SoundClips pain = sounds.AttackSound;

                if (entityBehaviour.EntityType == EntityTypes.EnemyClass)
                {
                    if (mobile.Enemy.ID == (int)MobileTypes.Knight_CityWatch)
                        pain = GetHumanoidDeathSound(sounds.RaceForSounds, MobileGender.Male);
                    else
                        pain = GetHumanoidDeathSound(sounds.RaceForSounds, mobile.Summary.Enemy.Gender);
                }

                dfAudioSource.PlayOneShot(pain, 1, 1);
            }

            while (motor.KnockbackSpeed > 0 || (!controller.isGrounded && mobile.Enemy.Behaviour != MobileBehaviour.Aquatic))
            {
                mobile.ChangeEnemyState(MobileStates.Hurt);
                if (entity.IsImmuneToParalysis)
                    entity.IsImmuneToParalysis = false;
                entity.IsParalyzed = true;
                yield return new WaitForEndOfFrame();
            }

            /*// If enemy associated with quest system, make sure quest system is done with it first
            questResourceBehaviour = GetComponent<QuestResourceBehaviour>();
            if (questResourceBehaviour)
            {
                while (!questResourceBehaviour.IsFoeDead)
                {
                    yield return new WaitForEndOfFrame();
                }
            }*/

            CompleteDeath();

            dying = null;
        }

        SoundClips GetHumanoidDeathSound(Races race, MobileGender gender)
        {
            switch (race)
            {
                case Races.Breton:
                    return (gender == MobileGender.Male) ? SoundClips.BretonMalePain3 : SoundClips.BretonFemalePain3;
                case Races.Redguard:
                    return (gender == MobileGender.Male) ? SoundClips.RedguardMalePain3 : SoundClips.RedguardFemalePain3;
                case Races.Nord:
                    return (gender == MobileGender.Male) ? SoundClips.NordMalePain3 : SoundClips.NordFemalePain3;
                case Races.DarkElf:
                    return (gender == MobileGender.Male) ? SoundClips.DarkElfMalePain3 : SoundClips.DarkElfFemalePain3;
                case Races.HighElf:
                    return (gender == MobileGender.Male) ? SoundClips.HighElfMalePain3 : SoundClips.HighElfFemalePain3;
                case Races.WoodElf:
                    return (gender == MobileGender.Male) ? SoundClips.WoodElfMalePain3 : SoundClips.WoodElfFemalePain3;
                case Races.Khajiit:
                    return (gender == MobileGender.Male) ? SoundClips.KhajiitMalePain3 : SoundClips.KhajiitFemalePain3;
                case Races.Argonian:
                    return (gender == MobileGender.Male) ? SoundClips.ArgonianMalePain3 : SoundClips.ArgonianFemalePain3;
                default:
                    return SoundClips.None;
            }
        }

        #endregion

        #region Private Methods

        void CompleteDeath(bool corpse = true)
        {
            if (!entityBehaviour)
                return;

            died = true;

            Debug.Log("DRAMATIC DEATHS - Completing Death!");

            // Disable enemy gameobject
            // Do not destroy as we must still save enemy state when dead
            gameObject.SetActive(false);

            // Lower enemy alert state on player now that enemy is dead
            // If this is final enemy targeting player then alert state will remain clear
            // Other enemies still targeting player will continue to raise alert state every update
            EnemySenses senses = entityBehaviour.GetComponent<EnemySenses>();
            if (senses && senses.Target == GameManager.Instance.PlayerEntityBehaviour)
                GameManager.Instance.PlayerEntity.SetEnemyAlert(false);

            if (!corpse)
                return;

            // Show death message
            string deathMessage = TextManager.Instance.GetLocalizedText("thingJustDied");
            deathMessage = deathMessage.Replace("%s", TextManager.Instance.GetLocalizedEnemyName(mobile.Enemy.ID));
            if (!DaggerfallUnity.Settings.DisableEnemyDeathAlert)
                DaggerfallUI.Instance.PopupMessage(deathMessage);

            int corpseTexture;
            if (mobile.Enemy.Gender == MobileGender.Female && mobile.Enemy.FemaleCorpseTexture != 0)
            {
                corpseTexture = mobile.Enemy.FemaleCorpseTexture;
            }
            else
            {
                corpseTexture = mobile.Enemy.CorpseTexture;
            }

            // Generate lootable corpse marker
            DaggerfallLoot loot = GameObjectHelper.CreateLootableCorpseMarker(
                GameManager.Instance.PlayerObject,
                entityBehaviour.gameObject,
                entity,
                corpseTexture,
                DaggerfallUnity.NextUID);

            /*if (mobile.Enemy.Behaviour == MobileBehaviour.Aquatic)
                loot.transform.position = transform.position;*/

            // Tag corpse loot marker with quest UID
            if (questResourceBehaviour)
                loot.corpseQuestUID = questResourceBehaviour.QuestUID;

            entityBehaviour.CorpseLootContainer = loot;

            // Transfer any items owned by entity to loot container
            // Many quests will stash a reward in enemy inventory for player to find
            // This will be in addition to normal random loot table generation
            loot.Items.TransferAll(entityBehaviour.Entity.Items);

            // Play body collapse sound
            if (DaggerfallUI.Instance.DaggerfallAudioSource)
                DaggerfallUI.Instance.DaggerfallAudioSource.PlayClipAtPoint(SoundClips.BodyFall, loot.transform.position, 1f);

            //mirror the corpse texture
            //if (Random.value > 0.5f) {
            if (DramaticDeaths.deaths % 2 == 0) {
                Vector3 mirror = new Vector3(-loot.transform.localScale.x, loot.transform.localScale.y, loot.transform.localScale.z);
                loot.transform.localScale = mirror;
            }

            if (DramaticDeaths.deaths < 11)
                DramaticDeaths.deaths++;
            else
                DramaticDeaths.deaths = 0;

            // Raise static event
            if (EnemyDeath.OnEnemyDeath != null)
                EnemyDeath.OnEnemyDeath(death, null);

            //loot.gameObject.AddComponent<DramaticDeathsCorpseGrounder>();
        }

        #endregion

        #region Event Handlers

        private void EntityBehaviour_OnSetEntity(DaggerfallEntity oldEntity, DaggerfallEntity newEntity)
        {
            if (oldEntity != null)
            {
                oldEntity.OnDeath -= EnemyEntity_OnDeath;
            }

            if (newEntity != null)
            {
                entity = newEntity as EnemyEntity;
                entity.OnDeath += EnemyEntity_OnDeath;
            }
        }

        private void EnemyEntity_OnDeath(DaggerfallEntity entity)
        {
            // Set flag to perform OnDeath tasks
            // It make take a few ticks for enemy to actually die if owned by quest system
            // because some other processing might need to be done in quest (like placing an item)
            // before this enemy can be deactivated and loot container dropped

            if (dying != null)
                return;

            dying = PerformDeath();

            StartCoroutine(dying);
        }

        private void OnDisable()
        {
            if (!died && dying != null)
            {
                StopCoroutine(dying);

                CompleteDeath(false);
            }
        }

        #endregion
    }
}
