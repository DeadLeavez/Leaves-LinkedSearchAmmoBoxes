using System.Text.Json.Serialization;

namespace Leaves_LinkedSearchAmmoBoxes;

// Mirrors config/config.jsonc. The mod now has two independent augmentations, each with its own
// toggle, both riding under the master switch.
public record ModConfig
{
    // Master on/off. When false, OnLoad returns before registering ANY patch, so the flea is left
    // exactly as SPT builds it - neither augmentation runs.
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    // Augmentation 1 (original): the flea "Linked Search" (chain icon). When you linked-search a
    // magazine/weapon/round, the compatible round's ammo box(es) get listed alongside it.
    // Patches RagfairLinkedItemService.BuildLinkedItemTable.
    [JsonPropertyName("augmentLinkedSearch")]
    public bool AugmentLinkedSearch { get; set; } = true;

    // Augmentation 2 (new): plain "search for this item" (click an item -> it filters the flea to
    // that item, sent as handbookId to /client/ragfair/find). When the searched item is a loose
    // round, its ammo box(es) are added to the results too. Patches RagfairHelper.FilterCategories.
    [JsonPropertyName("augmentItemSearch")]
    public bool AugmentItemSearch { get; set; } = true;
}
