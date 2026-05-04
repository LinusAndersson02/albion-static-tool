namespace StatisticsAnalysisTool.Network.Legacy;

public enum LegacyAlbionEventCode
{
    HealthUpdate = 6,
    HealthUpdates = 7,
    NewCharacter = 29,
    NewEquipmentItem = 30,
    NewSimpleItem = 32,
    NewFurnitureItem = 33,
    NewKillTrophyItem = 34,
    NewJournalItem = 35,
    NewLaborerItem = 36,
    NewEquipmentItemLegendarySoul = 37,
    TakeSilver = 62,
    UpdateFame = 82,
    UpdateReSpecPoints = 84,
    UpdateCurrency = 85,
    UpdateFactionStanding = 86,
    CharacterEquipmentChanged = 90,
    NewLoot = 98,
    AttachItemContainer = 99,
    NewMob = 123,
    PartyJoined = 231,
    PartyDisbanded = 232,
    PartyPlayerJoined = 233,
    PartyPlayerLeft = 235,
    PartyPlayerUpdated = 239,
    PartyInviteOrJoinPlayerEquipmentInfo = 245,
    InCombatStateUpdate = 276,
    OtherGrabbedLoot = 277,
    PartyFinderEquipmentSnapshot = 372,
    MightAndFavorReceivedEvent = 494
}

public enum LegacyAlbionOperationCode
{
    Join = 2,
    InventoryMoveItem = 30,
    GetCharacterEquipment = 143,
    PartyFinderGetEquipmentSnapshot = 364,
    PartyFinderRequestEquipmentSnapshot = 368
}
