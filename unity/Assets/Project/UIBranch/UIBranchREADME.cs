/*
 -- Playercard Prefab:
--structure
The PlayerCard Prefab: - root of the prefab
    TownHallCard - The town hall card in the prefab
    PlayerCardsHolder - holds the rest of the cards in the prefab
        StatusCards - holds all of the cards played against the player
        TryalCards - holds all of the tryal cards of the prefab

--usages
StatusCards and TryalCards will automatically space the cards they have (their children objects),
just add the cards that need to be there, and the script will handle the rest (top down = left to right) MAX 5 each

Town Hall card can be replaced by going to the spriteRenderer component on the child of TownHallCard, and replacing the sprite with whatever new one is wanted

--technologies
StatusCards and TryalCards both have horizontal layout groups that will space the child objects along the size of the object

PlayerCardsHolder has a Vertical Layout group of StatusCards and TryalCards that holds them together, aligns them and spaces them

Prefab root has horizontal layout group of PlayerCardsHolder and TownHallCard to kep them aligned and spaced

--UIalignment
--structure
    UI: root of the structure
        TopScreen: handler for the top sector of the screen
        MidScreen: handler for the mid sector of the screen
            MidLeftScreen: handler for the respective sector
            MidCenterScreen: a spacer to keep the center clear
            MidRightScreen: handler for the repesctive sector
        LowerScreen: handler for the lower sector

--usages 
TopScreen and LowerScreen will automatically space their child objects (playerCards) MAX 4 (top down -> left right)
MidLeft and MidRight will automatically space child objects (playerCards) MAX 2 each
MidScreen will automatically handle the middle screen on any setup without needing to modify it

--technologies:
TopScreen and LowerScreen has both Horizontal layout group and MainScreenSpacerHandler to keep child objects spaced correctly
MidLeft and MidRight have Vertical Layout groups to keep child objects spaced correctly
MidScreen has a horizontal layout group to keep MidLeftScreen, MidCenterScreen, and MidRightScreen spaced correctly
 */