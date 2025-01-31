using CommonLib.Utils;
using PlayerCorpse.Entities;
using PlayerCorpse.Items;
using Vintagestory.API.Common;

namespace PlayerCorpse
{
    public class Core : ModSystem
    {
        public static string ModId { get; private set; }
        public static ILogger ModLogger { get; private set; }

        public override void StartPre(ICoreAPI api)
        {
            ModId = Mod.Info.ModID;
            ModLogger = Mod.Logger;
        }

        public override void Start(ICoreAPI api)
        {
            Config.Current = api.LoadOrCreateConfig<Config>(Mod.Info.ModID + ".json", ModLogger);
            api.World.Config.SetBool(Mod.Info.ModID + ":CorpseCompassEnabled", Config.Current.CorpseCompassEnabled.Value);

            api.RegisterEntity("EntityPlayerCorpse", typeof(EntityPlayerCorpse));
            api.RegisterItemClass("ItemCorpseCompass", typeof(ItemCorpseCompass));
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            if (Config.Current.CreateWaypoint.Value == "auto")
            {
                Config.Current.CreateWaypoint.Value = "always";

                string[] hasDeathWaypointsMods = api.Assets.Get<string[]>(
                    new AssetLocation(Mod.Info.ModID, "config/hasdeathwaypointsmods.json"));

                foreach (string modid in hasDeathWaypointsMods)
                {
                    if (api.ModLoader.IsModEnabled(modid))
                    {
                        Config.Current.CreateWaypoint.Value = "none";
                    }
                }
            }
        }
    }
}
