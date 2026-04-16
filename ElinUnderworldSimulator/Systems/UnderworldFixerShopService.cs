using System;
using System.Collections.Generic;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldFixerShopService
    {
        private sealed class RepeatableStock
        {
            public string ItemId;
            public int Quantity;
        }

        private sealed class OneTimeStock
        {
            public string Key;
            public Func<Thing, bool> Match;
            public Func<Thing> Create;
        }

        private static readonly RepeatableStock[] RepeatableHomeSupportStock =
        {
            new RepeatableStock { ItemId = "board_home", Quantity = 1 },
            new RepeatableStock { ItemId = "board_resident", Quantity = 1 },
            new RepeatableStock { ItemId = "1", Quantity = 1 },
            new RepeatableStock { ItemId = "2", Quantity = 1 },
            new RepeatableStock { ItemId = "board_map", Quantity = 1 },
            new RepeatableStock { ItemId = "board_build", Quantity = 1 },
            new RepeatableStock { ItemId = "book_resident", Quantity = 1 },
            new RepeatableStock { ItemId = "board_party", Quantity = 1 },
            new RepeatableStock { ItemId = "board_party2", Quantity = 1 },
            new RepeatableStock { ItemId = "book_roster", Quantity = 1 },
            new RepeatableStock { ItemId = "3", Quantity = 1 },
            new RepeatableStock { ItemId = "4", Quantity = 1 },
            new RepeatableStock { ItemId = "5", Quantity = 1 },
            new RepeatableStock { ItemId = "drawing_paper", Quantity = 10 },
            new RepeatableStock { ItemId = "drawing_paper2", Quantity = 10 },
            new RepeatableStock { ItemId = "stethoscope", Quantity = 1 },
            new RepeatableStock { ItemId = "whip_love", Quantity = 1 },
            new RepeatableStock { ItemId = "whip_interest", Quantity = 1 },
            new RepeatableStock { ItemId = "syringe_blood", Quantity = 20 },
        };

        private static readonly OneTimeStock[] OneTimeHomeUnlockStock =
        {
            new OneTimeStock
            {
                Key = "uwfixer:chest6",
                Match = thing => thing.id == "chest6",
                Create = () => ThingGen.Create("chest6"),
            },
            new OneTimeStock
            {
                Key = "uwfixer:housePlate",
                Match = thing => thing.id == "housePlate",
                Create = () => ThingGen.Create("housePlate"),
            },
            new OneTimeStock
            {
                Key = "uwfixer:mailpost",
                Match = thing => thing.id == "mailpost",
                Create = () => ThingGen.Create("mailpost"),
            },
            new OneTimeStock
            {
                Key = "uwfixer:rp_food",
                Match = thing => thing.id == "rp_food",
                Create = () => ThingGen.Create("rp_food").SetNum(5).SetLv(5).Thing,
            },
            new OneTimeStock
            {
                Key = "uwfixer:rp_block",
                Match = thing => thing.id == "rp_block",
                Create = () => ThingGen.Create("rp_block").SetNum(10).SetLv(1).Thing,
            },
            new OneTimeStock
            {
                Key = "uwfixer:plan:2119",
                Match = thing => thing.id == "book_plan" && thing.refVal == 2119,
                Create = () => ThingGen.CreatePlan(2119),
            },
            new OneTimeStock
            {
                Key = "uwfixer:plan:2512",
                Match = thing => thing.id == "book_plan" && thing.refVal == 2512,
                Create = () => ThingGen.CreatePlan(2512),
            },
            new OneTimeStock
            {
                Key = "uwfixer:plan:2810",
                Match = thing => thing.id == "book_plan" && thing.refVal == 2810,
                Create = () => ThingGen.CreatePlan(2810),
            },
            new OneTimeStock
            {
                Key = "uwfixer:recipe:workbench2",
                Match = thing => thing.id == "rp_random" && string.Equals(thing.GetStr(53), "workbench2", StringComparison.Ordinal),
                Create = () => ThingGen.CreateRecipe("workbench2"),
            },
            new OneTimeStock
            {
                Key = "uwfixer:recipe:factory_stone",
                Match = thing => thing.id == "rp_random" && string.Equals(thing.GetStr(53), "factory_stone", StringComparison.Ordinal),
                Create = () => ThingGen.CreateRecipe("factory_stone"),
            },
            new OneTimeStock
            {
                Key = "uwfixer:recipe:stonecutter",
                Match = thing => thing.id == "rp_random" && string.Equals(thing.GetStr(53), "stonecutter", StringComparison.Ordinal),
                Create = () => ThingGen.CreateRecipe("stonecutter"),
            },
            new OneTimeStock
            {
                Key = "uwfixer:recipe:torch_wall",
                Match = thing => thing.id == "rp_random" && string.Equals(thing.GetStr(53), "torch_wall", StringComparison.Ordinal),
                Create = () => ThingGen.CreateRecipe("torch_wall"),
            },
            new OneTimeStock
            {
                Key = "uwfixer:recipe:factory_sign",
                Match = thing => thing.id == "rp_random" && string.Equals(thing.GetStr(53), "factory_sign", StringComparison.Ordinal),
                Create = () => ThingGen.CreateRecipe("factory_sign"),
            },
        };

        private static readonly string[] RecipeShopKeys =
        {
            ShopType.Starter.ToString(),
            ShopType.Loytel.ToString(),
            ShopType.Farris.ToString(),
        };

        internal static void TryPopulate(Trait trait)
        {
            if (!UnderworldPlugin.IsUnderworldMode() || !(trait?.owner is Chara fixer) || fixer.id != ModInfo.FixerId)
            {
                return;
            }

            Thing chest = EnsureMerchantChest(fixer);
            KeepStockPersistent(fixer);
            EnsureRepeatableStock(chest, RepeatableHomeSupportStock);
            EnsureOneTimeStock(chest, OneTimeHomeUnlockStock);
            EnsureRecipeUnlockStock(chest);
        }

        private static Thing EnsureMerchantChest(Chara fixer)
        {
            Thing chest = fixer.things.Find("chest_merchant");
            if (chest != null)
            {
                return chest;
            }

            chest = ThingGen.Create("chest_merchant");
            fixer.AddThing(chest);
            return chest;
        }

        private static void KeepStockPersistent(Chara fixer)
        {
            if (EClass.world?.date == null)
            {
                return;
            }

            fixer.c_dateStockExpire = EClass.world.date.GetRaw(24 * 365 * 50);
            fixer.isRestocking = true;
        }

        private static void EnsureRepeatableStock(Thing chest, IEnumerable<RepeatableStock> stocks)
        {
            foreach (RepeatableStock stock in stocks)
            {
                if (chest.things.Find(stock.ItemId) != null)
                {
                    continue;
                }

                AddShopThing(chest, ThingGen.Create(stock.ItemId).SetNum(stock.Quantity));
            }
        }

        private static void EnsureOneTimeStock(Thing chest, IEnumerable<OneTimeStock> stocks)
        {
            foreach (OneTimeStock stock in stocks)
            {
                EnsureOneTimeStock(chest, stock.Key, stock.Match, stock.Create);
            }
        }

        private static void EnsureRecipeUnlockStock(Thing chest)
        {
            HashSet<string> recipeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecipeSource recipe in RecipeManager.list)
            {
                if (recipe?.row?.recipeKey == null || recipe.row.recipeKey.Length == 0)
                {
                    continue;
                }

                if (string.Equals(recipe.id, "explosive", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string recipeKey in recipe.row.recipeKey)
                {
                    if (Array.IndexOf(RecipeShopKeys, recipeKey) < 0)
                    {
                        continue;
                    }

                    recipeIds.Add(recipe.id);
                    break;
                }
            }

            foreach (string recipeId in recipeIds)
            {
                string key = "uwfixer:recipe:" + recipeId;
                EnsureOneTimeStock(
                    chest,
                    key,
                    thing => thing.id == "rp_random" && string.Equals(thing.GetStr(53), recipeId, StringComparison.Ordinal),
                    () => ThingGen.CreateRecipe(recipeId));
            }
        }

        private static void EnsureOneTimeStock(Thing chest, string key, Func<Thing, bool> match, Func<Thing> create)
        {
            if (HasNoRestockKey(key) || ContainsThing(chest, match))
            {
                return;
            }

            Thing thing = create();
            MarkNoRestockKey(key);
            thing.SetInt(101, 1);
            AddShopThing(chest, thing);
        }

        private static bool ContainsThing(Thing chest, Func<Thing, bool> match)
        {
            foreach (Thing thing in chest.things)
            {
                if (thing != null && !thing.isDestroyed && match(thing))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddShopThing(Thing chest, Thing thing)
        {
            thing.c_idBacker = 0;
            thing.c_IDTState = 0;
            thing.isStolen = false;
            chest.AddThing(thing);
        }

        private static bool HasNoRestockKey(string key)
        {
            if (!EClass.player.noRestocks.TryGetValue(ModInfo.FixerId, out HashSet<string> noRestocks))
            {
                return false;
            }

            return noRestocks.Contains(key);
        }

        private static void MarkNoRestockKey(string key)
        {
            if (!EClass.player.noRestocks.TryGetValue(ModInfo.FixerId, out HashSet<string> noRestocks))
            {
                noRestocks = new HashSet<string>(StringComparer.Ordinal);
                EClass.player.noRestocks[ModInfo.FixerId] = noRestocks;
            }

            noRestocks.Add(key);
        }
    }
}
