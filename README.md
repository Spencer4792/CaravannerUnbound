# CaravannerUnbound

Unchains the Soroborean Caravanner's fast-travel service in **Outward DE**.

This mod makes every standing city, Cierzo, Berg, Levant, Monsoon, Harmattan, and New Sirocco, available at once, from anywhere the caravanner will talk to you.

## Features

- **Travel from anywhere** the vanilla "only in cities" restriction is lifted (and can be restored in config if you only want the expanded destination list).
- **Story-aware** destroyed or blockaded cities stay off the list, and New Sirocco only appears once its caravan trader is established. Turn `RespectStoryEvents` off if you'd rather ignore that.
- **Configurable destinations** individual on/off toggle for each city in the config (works with the in-game Configuration Manager, default F5).
- **Update-resilient** the mod validates everything it touches at startup and logs a precise warning if a game patch changes something, instead of failing silently. Story-event IDs are read from the game's own constants rather than hardcoded.

## How it works

The vanilla destination logic runs completely untouched, including its random 72-hour destination roll, and the mod then *appends* the cities the roll didn't pick, using the game's own route data. Vanilla multiplayer syncing is preserved: in co-op, only the host expands the list and clients receive it normally.

## Requirements

- Outward Definitive Edition on the **Mono** branch (Steam: Properties → Betas → `default-mono`)
- BepInEx pack for Outward

## Config

`BepInEx/config/com.spencer4792.caravannerunbound.cfg` (created on first launch):

| Setting | Default | Effect |
|---|---|---|
| Enabled | true | Master switch |
| RespectStoryEvents | true | Destroyed/unbuilt cities stay unavailable |
| OnlyOfferTravelInCities | false | Restore the vanilla in-city-only rule |
| Destinations / \<City\> | true | Offer that city |

## Background

I noticed a lot of mods that altered the Soroborean Caravanners were no longer functioning properly on Definitive Edition. Rather than patch up old code, CaravannerUnbound was written from scratch for DE with a different approach: additive, config-driven, and resilient to game updates.

## Source

Source code is included and MIT licensed — fork it, learn from it, build on it.
