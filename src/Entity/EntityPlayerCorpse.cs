using System.IO;
using SharedUtils;
using SharedUtils.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    public class EntityPlayerCorpse : EntityAgent
    {
        public Core Core { get; private set; }
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            Core = Api.ModLoader.GetModSystem<Core>();
        }

        /// <summary> The corpse inventory </summary>
        public InventoryGeneric inventory;

        /// <summary> How many milliseconds have passed since the last interaction check </summary>
        private long lastInteractMs;
        public long LastInteractPassedMs
        {
            get { return World.ElapsedMilliseconds - lastInteractMs; }
            set { lastInteractMs = value; }
        }

        /// <summary> How many seconds have passed since the interaction began </summary>
        public float SecondsPassed { get; set; }
        public float requiredSeconds = 3;


        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            if (inventory != null)
            {
                foreach (var slot in inventory)
                {
                    slot.Itemstack?.ResolveBlockOrItem(World);
                }
            }
        }

        public override bool ShouldReceiveDamage(DamageSource damageSource, float damage)
        {
            if (!Config.Current.CanFired.Val && damageSource.Type == EnumDamageType.Fire) return false;
            if (!Config.Current.HasHealth.Val) return false;

            return base.ShouldReceiveDamage(damageSource, damage);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (LastInteractPassedMs > 300)
            {
                if (SecondsPassed != 0 && Api.Side == EnumAppSide.Client)
                {
                    Core.HudOverlayRenderer.CircleVisible = false;
                }
                SecondsPassed = 0;
            }
            else
            {
                SecondsPassed += dt;
                if (Api.Side == EnumAppSide.Client)
                {
                    Core.HudOverlayRenderer.CircleProgress = SecondsPassed / requiredSeconds;
                }
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            IPlayer byPlayer = World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (byPlayer == null) return;
            if (byPlayer.PlayerUID != WatchedAttributes.GetString("ownerUID") &&
                byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                (byPlayer as IServerPlayer)?.SendIngameError("", Lang.Get("game:ingameerror-not-corpse-owner"));
                base.OnInteract(byEntity, itemslot, hitPosition, mode);
                return;
            }

            if (Api.Side == EnumAppSide.Server)
            {
                if (inventory == null || inventory.Count == 0) Die();
                else if (SecondsPassed > requiredSeconds)
                {
                    foreach (var slot in inventory)
                    {
                        if (slot.Empty) continue;

                        if (!byPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                        {
                            Api.World.SpawnItemEntity(slot.Itemstack, byEntity.ServerPos.XYZ.AddCopy(0, 1, 0));
                        }
                        slot.Itemstack = null;
                        slot.MarkDirty();
                    }

                    string log = string.Format("{0} at {1} can be collected by {2}, id {3}", GetName(), SidedPos.XYZ.RelativePos(Api), (byEntity as EntityPlayer).Player.PlayerName, EntityId);
                    Api.Logger.ModNotification(log);
                    if (Config.Current.DebugMode.Val) Api.SendMessageAll(log);

                    Die();
                }
            }

            LastInteractPassedMs = World.ElapsedMilliseconds;
        }

        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        {
            if (reason == EnumDespawnReason.Death && inventory != null)
            {
                inventory.Api = Api; // fix strange null
                inventory.DropAll(SidedPos.XYZ.AddCopy(0, 1, 0));
            }

            string log = string.Format("{0} at {1} was destroyed, id {2}", GetName(), SidedPos.XYZ.RelativePos(Api), EntityId);
            Api.Logger.ModNotification(log);
            if (Config.Current.DebugMode.Val) Api.SendMessageAll(log);

            base.Die(reason, damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);

            if (Api.Side == EnumAppSide.Client)
            {
                Core.HudOverlayRenderer.CircleVisible = false;
            }
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            if (inventory != null && WatchedAttributes != null)
            {
                WatchedAttributes.SetString("invid", inventory.InventoryID);
                inventory.ToTreeAttributes(WatchedAttributes);
            }
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (WatchedAttributes != null)
            {
                string inventoryID = WatchedAttributes.GetString("invid");

                inventory = new InventoryGeneric(0, inventoryID, Api);
                inventory.FromTreeAttributes(WatchedAttributes);
            }
        }

        /// <summary> Get the corpse name </summary>
        public override string GetName()
        {
            return Lang.Get("{0}'s corpse", WatchedAttributes.GetString("ownerName"));
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return new WorldInteraction[] {
                new WorldInteraction{
                    ActionLangCode = ConstantsCore.ModId + ":blockhelp-collect",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }
    }
}