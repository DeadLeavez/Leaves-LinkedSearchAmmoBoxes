using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Services;

namespace Leaves_LinkedSearchAmmoBoxes.Patches;

// Patches RagfairLinkedItemService.BuildLinkedItemTable - the method that builds SPT's cache of
// "what items are linked to what" (a Dictionary<MongoId, HashSet<MongoId>>: item -> linked items).
// The flea "Linked Search" reads that cache via GetLinkedItems -> RagfairHelper.FilterCategories.
//
// It's a lazy cache: built once, the first time anyone opens a linked search, then reused. We run a
// POSTFIX right after it's built and add ammo boxes into it - once. Doing it here rather than in
// GetLinkedItems matters two ways:
//   * no per-request cost - GetLinkedItems is hit constantly while browsing the flea;
//   * no mutating a shared HashSet on concurrent linked searches, which would be a race under Fika.
public class BuildLinkedItemTablePatch : AbstractPatch
{
    // BuildLinkedItemTable is private, hence NonPublic. If a future SPT renames it the patch simply
    // won't apply (AbstractPatch handles a null target), and the feature no-ops instead of crashing.
    protected override MethodBase? GetTargetMethod()
    {
        return typeof(RagfairLinkedItemService).GetMethod(
            "BuildLinkedItemTable", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    [PatchPostfix]
    private static void Postfix(RagfairLinkedItemService __instance)
    {
        // The cache lives in a private field on the service. Reflection is the price of reaching an
        // SPT internal we don't own; if the field ever gets renamed we bail quietly (feature off)
        // rather than throw.
        var cacheField = typeof(RagfairLinkedItemService).GetField(
            "linkedItemsCache", BindingFlags.NonPublic | BindingFlags.Instance);
        if (cacheField?.GetValue(__instance) is not Dictionary<MongoId, HashSet<MongoId>> cache)
        {
            Leaves.Utils.Instance.printColor(
                "[LinkedAmmoBoxes] Could not read RagfairLinkedItemService.linkedItemsCache - SPT may have changed it. Feature inactive.",
                LogTextColor.Red);
            return;
        }

        var cartridgeToBoxes = LinkedSearchAmmoBoxContext.CartridgeToBoxes();
        int linksAdded = 0;

        foreach (var (searchedItem, linkedSet) in cache)
        {
            // Collect first, add after - never mutate linkedSet while enumerating it.
            var boxesToAdd = new List<MongoId>();

            // 1) Searched item IS a round -> surface the box(es) that hold it.
            if (cartridgeToBoxes.TryGetValue(searchedItem, out var ownBoxes))
            {
                boxesToAdd.AddRange(ownBoxes);
            }

            // 2) Any round linked to the searched item (e.g. rounds that fit a searched magazine or
            //    weapon) -> surface that round's box(es) too.
            foreach (var linked in linkedSet)
            {
                if (cartridgeToBoxes.TryGetValue(linked, out var linkedBoxes))
                {
                    boxesToAdd.AddRange(linkedBoxes);
                }
            }

            foreach (var box in boxesToAdd)
            {
                if (linkedSet.Add(box))
                {
                    linksAdded++;
                }
            }
        }

        Leaves.Utils.Instance.printColor(
            $"[LinkedAmmoBoxes] Added {linksAdded} ammo-box entries to the flea linked-search table.",
            LogTextColor.Cyan);
    }
}
