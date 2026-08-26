using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services;

namespace Leaves_LinkedSearchAmmoBoxes;

// Static bridge between the mod (which has DI-injected services) and the Harmony patch (which must
// be static - a hard Harmony requirement, same reason Questrandomizer has QuestrandomizerContext).
// The mod fills these in during OnLoad; the patch reads them when SPT builds its linked-items table.
public static class LinkedSearchAmmoBoxContext
{
    // Parent id of the "AmmoBox" item class - the factory bullet packs. Same constant BarterEconomy
    // uses to find boxes.
    private static readonly MongoId AmmoBoxClass = "543be5cb4bdc2deb348b4568";

    public static DatabaseService DatabaseService = null!;

    // Built once, lazily, the first time the patch runs. Doing it lazily (rather than in OnLoad)
    // means the whole item database - including ammo boxes added by other mods - is guaranteed
    // present, because the linked-items table isn't built until the first flea "linked search",
    // long after every mod's load stage has finished.
    private static Dictionary<MongoId, List<MongoId>>? cartridgeToBoxes;

    // cartridge tpl -> the ammo box tpl(s) that contain it. An ammo box declares the round it holds
    // in its StackSlots filter (the same place BuildAmmoBoxMap reads in BarterEconomy). Every
    // vanilla box lists exactly one round, but we handle multiple defensively.
    public static Dictionary<MongoId, List<MongoId>> CartridgeToBoxes()
    {
        if (cartridgeToBoxes != null)
        {
            return cartridgeToBoxes;
        }

        var map = new Dictionary<MongoId, List<MongoId>>();
        foreach (var item in DatabaseService.GetItems().Values)
        {
            if (item.Parent != AmmoBoxClass)
            {
                continue;
            }

            foreach (var stackSlot in item.Properties?.StackSlots ?? Enumerable.Empty<StackSlot>())
            {
                foreach (var filter in stackSlot.Properties?.Filters ?? Enumerable.Empty<SlotFilter>())
                {
                    foreach (var cartridge in filter.Filter ?? new HashSet<MongoId>())
                    {
                        if (!map.TryGetValue(cartridge, out var boxes))
                        {
                            boxes = new List<MongoId>();
                            map[cartridge] = boxes;
                        }
                        if (!boxes.Contains(item.Id))
                        {
                            boxes.Add(item.Id);
                        }
                    }
                }
            }
        }

        cartridgeToBoxes = map;
        return map;
    }
}
