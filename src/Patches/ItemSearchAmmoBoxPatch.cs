using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Ragfair;

namespace Leaves_LinkedSearchAmmoBoxes.Patches;

// Patches RagfairHelper.FilterCategories - the method that turns a flea search request into the set
// of item tpls whose offers should be shown. When you click an item and pick "search for this item",
// the client hits /client/ragfair/find with the item's own tpl in HandbookId; FilterCategories ->
// GetCategoryList hits its leaf branch and returns just [that tpl], so you see offers of exactly
// that one item.
//
// We POSTFIX it: if the searched HandbookId is a loose round we know boxes for, we append those box
// tpl(s) to the result list, so the search also surfaces the factory packs of that round.
//
// Why key off HandbookId (the searched id) rather than scanning the whole result list: a single-item
// "search for this item" sends the round's tpl as HandbookId and the result is [round]. A category
// browse instead sends a CATEGORY id and the result already contains every round in that category -
// scanning the result would then dump every box into a plain "browse the ammo category" view, which
// isn't what this feature is for. Matching on HandbookId scopes us to the direct-item-search case.
//
// Unlike BuildLinkedItemTable this runs per search request, not once - but the work is a single
// dictionary lookup plus at most a couple of adds, so it's cheap, and it never mutates shared state
// (the returned list is freshly built by FilterCategories on every call).
public class ItemSearchAmmoBoxPatch : AbstractPatch
{
    // FilterCategories is public, so PublicLookup. If a future SPT renames it the patch simply won't
    // apply (AbstractPatch handles a null target) and this augmentation no-ops instead of crashing.
    protected override MethodBase? GetTargetMethod()
    {
        return typeof(RagfairHelper).GetMethod(
            "FilterCategories", BindingFlags.Public | BindingFlags.Instance);
    }

    // __result is the List<MongoId> FilterCategories returns; request is the original arg by name
    // (plain Harmony under the hood, so name-matched injection works). We mutate the list in place.
    [PatchPostfix]
    private static void Postfix(List<MongoId> __result, SearchRequestData request)
    {
        // Only a direct item search carries a HandbookId; linked-only searches and build searches
        // leave it empty, and category browses set it to a category id (which won't be in the map).
        if (request.HandbookId == null || request.HandbookId.Value.IsEmpty)
        {
            return;
        }

        var cartridgeToBoxes = LinkedSearchAmmoBoxContext.CartridgeToBoxes();
        if (!cartridgeToBoxes.TryGetValue(request.HandbookId.Value, out var boxes))
        {
            // Searched item isn't a round we have boxes for - leave the result untouched.
            return;
        }

        foreach (var box in boxes)
        {
            if (!__result.Contains(box))
            {
                __result.Add(box);
            }
        }
    }
}
