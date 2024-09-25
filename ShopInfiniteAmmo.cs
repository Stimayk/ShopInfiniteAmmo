using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopInfiniteAmmo
{
    public class ShopInfiniteAmmo : BasePlugin
    {
        public override string ModuleName => "[SHOP] Infinite Ammo";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.1";

        private IShopApi? SHOP_API;
        private const string CategoryName = "InfiniteAmmo";
        public static JObject? JsonInfiniteAmmo { get; private set; }
        private readonly PlayerInfiniteAmmo[] playerInfiniteAmmos = new PlayerInfiniteAmmo[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/InfiniteAmmo.json");
            if (File.Exists(configPath))
            {
                JsonInfiniteAmmo = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonInfiniteAmmo == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Бесконечные патроны");

            foreach (var item in JsonInfiniteAmmo.Properties().Where(p => p.Value is JObject))
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(
                        item.Name,
                        (string)item.Value["name"]!,
                        CategoryName,
                        (int)item.Value["price"]!,
                        (int)item.Value["sellprice"]!,
                        (int)item.Value["duration"]!
                    );
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerInfiniteAmmos[playerSlot] = null!);

            RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
            RegisterEventHandler<EventWeaponReload>(OnWeaponReload);
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            if (TryGetInfiniteAmmoType(uniqueName, out int ammoType))
            {
                playerInfiniteAmmos[player.Slot] = new PlayerInfiniteAmmo(ammoType, itemId);
                ApplyInfiniteAmmo(player, ammoType);
            }
            else
            {
                Logger.LogError($"{uniqueName} has invalid or missing 'ammotype' in config!");
            }
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1 && TryGetInfiniteAmmoType(uniqueName, out int ammoType))
            {
                playerInfiniteAmmos[player.Slot] = new PlayerInfiniteAmmo(ammoType, itemId);
                ApplyInfiniteAmmo(player, ammoType);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerInfiniteAmmos[player.Slot] = null!;
            return HookResult.Continue;
        }

        private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player != null && playerInfiniteAmmos[player.Slot] != null)
            {
                ApplyInfiniteAmmo(player, playerInfiniteAmmos[player.Slot].AmmoType);
            }

            return HookResult.Continue;
        }

        private HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player != null && playerInfiniteAmmos[player.Slot] != null)
            {
                ApplyInfiniteAmmo(player, playerInfiniteAmmos[player.Slot].AmmoType);
            }

            return HookResult.Continue;
        }

        private static void ApplyInfiniteAmmo(CCSPlayerController player, int ammoType)
        {
            switch (ammoType)
            {
                case 1:
                    ApplyInfiniteClip(player);
                    break;
                case 2:
                    ApplyInfiniteReserve(player);
                    break;
            }
        }

        private static void ApplyInfiniteClip(CCSPlayerController player)
        {
            var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;
            if (activeWeaponHandle?.Value != null && activeWeaponHandle.Value.VData != null)
            {
                activeWeaponHandle.Value.Clip1 = activeWeaponHandle.Value.VData.MaxClip1;
            }
        }

        private static void ApplyInfiniteReserve(CCSPlayerController player)
        {
            var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;
            if (activeWeaponHandle?.Value != null)
            {
                activeWeaponHandle.Value.ReserveAmmo[0] = 999;
            }
        }

        private static bool TryGetInfiniteAmmoType(string uniqueName, out int ammoType)
        {
            ammoType = 0;
            if (JsonInfiniteAmmo != null && JsonInfiniteAmmo.TryGetValue(uniqueName, out var obj) && obj is JObject jsonItem && jsonItem["ammotype"] != null)
            {
                ammoType = (int)jsonItem["ammotype"]!;
                return true;
            }
            return false;
        }

        public record class PlayerInfiniteAmmo(int AmmoType, int ItemID);
    }
}