using System.Reflection;
using Leaves_LinkedSearchAmmoBoxes.Patches;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Services;

namespace Leaves_LinkedSearchAmmoBoxes;

// One tiny job: make the flea market's "Linked Search" also list ammo boxes wherever the matching
// loose round shows up. All the work is a single Harmony postfix on RagfairLinkedItemService's
// table builder - see BuildLinkedItemTablePatch.
//
// PostDBModLoader + 1 is plenty early: we don't touch any item data at load, we only register the
// patch. The actual augmentation runs at RUNTIME, the first time a player opens a linked search
// (that's when SPT lazily builds its linked-items table), so item load order doesn't matter here.
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class LinkedSearchAmmoBoxes(
    DatabaseService databaseService,
    ModHelper modHelper,
    PatchManager patchManager)
    : IOnLoad
{
    public Task OnLoad()
    {
        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<ModConfig>(modPath, "config/config.jsonc");

        if (!config.Enabled)
        {
            Leaves.Utils.Instance.printColor("[LinkedAmmoBoxes] Disabled in config - leaving the flea untouched.", LogTextColor.Yellow);
            return Task.CompletedTask;
        }

        // Both augmentations off individually is the same as disabled - don't register anything.
        if (!config.AugmentLinkedSearch && !config.AugmentItemSearch)
        {
            Leaves.Utils.Instance.printColor("[LinkedAmmoBoxes] Both augmentLinkedSearch and augmentItemSearch are off - nothing to do.", LogTextColor.Yellow);
            return Task.CompletedTask;
        }

        // Hand the patches the DB (they're static and can't be constructor-injected). They reach
        // Leaves.Utils directly via its self-registered Leaves.Utils.Instance singleton.
        LinkedSearchAmmoBoxContext.DatabaseService = databaseService;

        patchManager.PatcherName = "Leaves.LinkedSearchAmmoBoxes";

        if (config.AugmentLinkedSearch)
        {
            patchManager.AddPatch(new BuildLinkedItemTablePatch());
            Leaves.Utils.Instance.printColor("[LinkedAmmoBoxes] Armed - ammo boxes will be added to flea LINKED-search results.", LogTextColor.Cyan);
        }

        if (config.AugmentItemSearch)
        {
            patchManager.AddPatch(new ItemSearchAmmoBoxPatch());
            Leaves.Utils.Instance.printColor("[LinkedAmmoBoxes] Armed - ammo boxes will be added to flea ITEM-search results.", LogTextColor.Cyan);
        }

        patchManager.EnablePatches();
        return Task.CompletedTask;
    }
}
