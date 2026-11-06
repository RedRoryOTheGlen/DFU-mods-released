using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Weather;
using DaggerfallWorkshop.Utility.AssetInjection;
using UnityEngine;
using UnityStandardAssets.Water;

namespace WorldwalkingMod
{
    public class WorldwalkingMobile : MonoBehaviour
    {
        GameObject markerObject;

        BillboardMobile billboard;
        bool isEnemy = false;
        bool isBoat = false;
        bool isFlying = false;
        int levelCeiling = -1;

        Vector3 anchorPosition;
        Vector3 offsetPosition;

        bool initialized = false;

        float offsetTime = 60;
        float offsetTimer = 0;

        float sightRadius = 9;
        float maximumAnchorDistance = 18;

        bool spotted = false;
        float spottedTime = 2;
        float spottedTimer = 0;

        public void Initialize(Vector3 anchor, bool enemy = false, bool flying = false, bool boat = false, int ceiling = -1)
        {
            anchorPosition = anchor;
            offsetPosition = anchorPosition;

            isEnemy = enemy;
            isFlying = flying;
            isBoat = boat;

            levelCeiling = ceiling;

            markerObject = transform.GetChild(0).gameObject;

            //get billboard
            billboard = markerObject.GetComponent<BillboardMobile>();
            if (billboard != null)
            {
                billboard.isFlying = isFlying;
            }

            markerObject.SetActive(false);

            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
                return;

            if (isEnemy && IsPlayerVisible())
            {
                if (levelCeiling == -1 || (levelCeiling != -1 && GameManager.Instance.PlayerEntity.Level < levelCeiling))
                {
                    //chase behavior
                    //if enemy and current position is within distance of anchor, check for player object position
                    //if player is within sight radius, move towards player
                    //if player is in location, counts as out of sight
                    Vector3 targetPosition = Vector3.MoveTowards(transform.position, Worldwalking.Instance.PlayerObject.transform.position, GetTravelSpeed() * 1.2f * Time.unscaledDeltaTime * Worldwalking.Instance.TimeScale);
                    bool water = Worldwalking.Instance.IsOpenWater(Mathf.RoundToInt(targetPosition.x), Mathf.RoundToInt(-targetPosition.z));
                    if ((water && isBoat) || (!water && !isBoat && !isFlying) || isFlying)
                    {
                        transform.position = targetPosition;
                        transform.forward = (Worldwalking.Instance.PlayerObject.transform.position - transform.position).normalized;
                        UpdateHeight();
                        if (billboard != null)
                        {
                            billboard.isMoving = true;
                            billboard.isSwimming = water;
                        }
                    }
                    else
                    {
                        if (billboard != null)
                        {
                            billboard.isMoving = false;
                            billboard.isSwimming = water;
                        }
                    }

                    //distance check to player
                    if (Vector3.Distance(Worldwalking.Instance.PlayerObject.transform.position, transform.position) < 1f)
                    {
                        Worldwalking.Instance.TriggerEncounterObject(this.gameObject);
                    }
                }
                else
                {
                    //flee behavior
                    //if enemy and current position is within distance of anchor, check for player object position
                    //if player is within sight radius, move away from player
                    //if player is in location, counts as out of sight
                    Vector3 targetDirection = (transform.position - Worldwalking.Instance.PlayerObject.transform.position);
                    Vector3 targetPosition = transform.position + targetDirection;
                    Vector3 stepPosition = Vector3.MoveTowards(transform.position, targetPosition, GetTravelSpeed() * 1.2f * Time.unscaledDeltaTime * Worldwalking.Instance.TimeScale);
                    bool water = Worldwalking.Instance.IsOpenWater(Mathf.RoundToInt(stepPosition.x), Mathf.RoundToInt(-stepPosition.z));
                    if ((water && isBoat) || (!water && !isBoat && !isFlying) || isFlying)
                    {
                        transform.position = stepPosition;
                        transform.forward = targetDirection.normalized;
                        UpdateHeight();
                        if (billboard != null)
                        {
                            billboard.isMoving = true;
                            billboard.isSwimming = water;
                        }
                    }
                    else
                    {
                        if (billboard != null)
                        {
                            billboard.isMoving = false;
                            billboard.isSwimming = water;
                        }
                    }

                    /*//distance check to player
                    if (Vector3.Distance(Worldwalking.Instance.PlayerObject.transform.position, transform.position) < 1f)
                    {
                        Worldwalking.Instance.TriggerEncounterObject(this.gameObject);
                    }*/
                }
            }
            else
            {
                //wander behavior
                //move to random point around anchor
                Vector2 aPos = new Vector3(transform.position.x, 0, transform.position.z);
                Vector2 bPos = new Vector3(offsetPosition.x, 0, offsetPosition.z);
                if (Vector3.Distance(aPos, bPos) > 0.1f)
                {
                    Vector3 targetPosition = Vector3.MoveTowards(transform.position, offsetPosition, GetTravelSpeed() * Time.unscaledDeltaTime * Worldwalking.Instance.TimeScale);
                    bool water = Worldwalking.Instance.IsOpenWater(Mathf.RoundToInt(targetPosition.x), Mathf.RoundToInt(-targetPosition.z));
                    if ((water && isBoat) || (!water && !isBoat && !isFlying) || isFlying)
                    {
                        transform.position = targetPosition;
                        transform.forward = (new Vector3(offsetPosition.x, transform.position.y, offsetPosition.z) - transform.position).normalized;
                        UpdateHeight();
                        if (billboard != null)
                        {
                            billboard.isMoving = true;
                            billboard.isSwimming = water;
                        }
                    }
                    else
                    {
                        //cancel the movement
                        offsetPosition = transform.position;

                        if (billboard != null)
                        {
                            billboard.isMoving = false;
                            billboard.isSwimming = water;
                        }
                    }
                    return;
                }
                else
                {
                    if (billboard != null)
                    {
                        billboard.isMoving = false;
                    }
                }

                //wait
                if (offsetTimer > offsetTime)
                {
                    //get new offset
                    UpdateOffset();
                    offsetTimer = 0;
                }
                else
                {
                    offsetTimer += Time.unscaledDeltaTime * Worldwalking.Instance.TimeScale;
                }
                //repeat
            }

            //check if is inside player sight range and show marker if so
            //else, hide marker
            if (Vector3.Distance(Worldwalking.Instance.PlayerObject.transform.position, transform.position) > Worldwalking.Instance.sightRadius)
            {
                if (markerObject.activeSelf)
                {
                    markerObject.SetActive(false);
                }

                if (spotted)
                {
                    if (spottedTimer > spottedTime)
                        spotted = false;
                    else if (Worldwalking.Instance.TimeScale > 0)
                        spottedTimer += Time.unscaledDeltaTime;
                }
            }
            else
            {
                if (!markerObject.activeSelf)
                {
                    markerObject.SetActive(true);

                    if (isEnemy && !spotted)
                    {
                        spotted = true;
                        Worldwalking.Instance.EnemySighted();
                    }

                    spottedTimer = 0;
                }
            }
        }

        float GetTravelSpeed()
        {
            float value = 0.01f;

            if (billboard != null)
            {
                if (billboard.isRiding || billboard.isFlying)
                    value = 0.02f;
            }

            if (DaggerfallUnity.Instance.WorldTime.Now.IsNight)
                value *= 0.5f;

            //climate
            if (Worldwalking.Instance.currentClimate == (int)MapsFile.Climates.Swamp ||
            Worldwalking.Instance.currentClimate == (int)MapsFile.Climates.Rainforest ||
            Worldwalking.Instance.currentClimate == (int)MapsFile.Climates.MountainWoods)
                value *= 0.9f;
            else if (Worldwalking.Instance.currentClimate == (int)MapsFile.Climates.Mountain ||
            Worldwalking.Instance.currentClimate == (int)MapsFile.Climates.HauntedWoodlands)
                value *= 0.8f;

            //weather
            WeatherType weather = GameManager.Instance.WeatherManager.PlayerWeather.WeatherType;
            if (!isBoat)
            {
                if (weather == WeatherType.Thunder || weather == WeatherType.Snow) //20% speed penalty in snow and thunder
                {
                    value *= 0.8f;
                }
                else if (weather == WeatherType.Rain)   //10% speed penalty in rain
                    value *= 0.9f;
            }
            else
            {
                if (weather == WeatherType.Thunder)
                    value *= 1.2f;
                else if (weather == WeatherType.Fog)
                    value *= 0.2f;
            }

            return value * Worldwalking.Instance.mobileSpeedMod;
        }

        bool IsPlayerVisible()
        {
            //if player is inside a location, return false
            if (Worldwalking.Instance.IsPlayerInLocation())
                return false;

            //if player is further than max distance from anchor, return false
            if (Vector3.Distance(Worldwalking.Instance.PlayerObject.transform.position, anchorPosition) > maximumAnchorDistance)
                return false;

            //if player is further than sight radius from self, return false
            if (Vector3.Distance(Worldwalking.Instance.PlayerObject.transform.position, transform.position) > (sightRadius * Worldwalking.Instance.mobileSightMod) - Worldwalking.Instance.playerStealth)
                return false;

            //if not a boat and not flying and player is in water, return false
            if (!isBoat && !isFlying && Worldwalking.Instance.onWater)
                return false;

            //if a boat and player is on land, return false
            if (isBoat && !Worldwalking.Instance.onWater)
                return false;

            return true;
        }

        void UpdateOffset()
        {
            Vector2 random = Vector3.zero;
            Vector3 targetPosition = Vector3.zero;
            bool water = false;
            bool available = false;
            int breakout = 0;
            while (!available && breakout < 10)
            {
                random = Random.insideUnitCircle.normalized;
                targetPosition = anchorPosition + (new Vector3(random.x, 0, random.y) * Random.Range(0.1f, 5f));
                water = Worldwalking.Instance.IsOpenWater(Mathf.RoundToInt(targetPosition.x), Mathf.RoundToInt(-targetPosition.z));
                if ((water && isBoat) || (!water && !isBoat && !isFlying) || isFlying)
                {
                    available = true;
                }
                breakout++;
            }
            offsetPosition = targetPosition;
        }

        void UpdateHeight()
        {
            //raycast from above the object position and check height of terrain
            Ray ray = new Ray(new Vector3(transform.position.x, transform.position.y + 100, transform.position.z), Vector3.down);
            RaycastHit hit = new RaycastHit();

            float baseHeight = 0;

            if (Physics.Raycast(ray, out hit, 200, Physics.DefaultRaycastLayers))
            {
                baseHeight = hit.point.y;
                transform.position = new Vector3(transform.position.x, baseHeight, transform.position.z);
            }
        }
    }
}
