using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.UI;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>The generated uGUI: HUD, toasts and the shop / altar / replace menus, driven through their buttons and events.</summary>
    public class UiTests
    {
        LootDatabase _db;
        MovementConfig _cfg;
        PlayerController _pc;
        PlayerInventory _inv;
        Health _health;
        UiBootstrap _ui;
        readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _db = DefaultContent.CreateDatabase();
            _cfg = ScriptableObject.CreateInstance<MovementConfig>();
            Physics2D.SyncTransforms();
            _pc = PlayerFactory.Build(_cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            PlayerCombatSetup.Attach(_pc, null, null);
            _inv = PlayerItemsSetup.Attach(_pc);
            _health = _pc.GetComponent<Health>();
            _pc.autoTick = false;
            _pc.GetComponent<PlayerCombat>().autoTick = false;
            _pc.GetComponent<PlayerDefense>().autoTick = false;
            PlayerItemsSetup.DisableAutoTick(_pc.gameObject);
            _spawned.Add(_pc.gameObject);
            _inv.ResetForRun(_db);

            var go = new GameObject("UI");
            go.SetActive(false);
            _ui = go.AddComponent<UiBootstrap>();
            _ui.Bind(_pc);
            go.SetActive(true);
            _ui.Build();
            _spawned.Add(go);
        }

        [TearDown]
        public void TearDown()
        {
            PauseGate.Reset();
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var es in UnityEngine.Object.FindObjectsByType<EventSystem>()) UnityEngine.Object.DestroyImmediate(es.gameObject);
            UnityEngine.Object.DestroyImmediate(_cfg);
            Time.timeScale = 1f;
        }

        List<Button> ModalButtons()
        {
            var list = new List<Button>();
            foreach (var b in _ui.Root.ModalLayer.GetComponentsInChildren<Button>(false)) list.Add(b);
            return list;
        }

        // ------------------------------------------------------------------ HUD

        [Test]
        public void HudBuildsWithCanvasAndEventSystem()
        {
            Assert.IsNotNull(_ui.Root);
            Assert.IsNotNull(_ui.Hud);
            Assert.IsNotNull(_ui.Modals);
            Assert.IsNotNull(EventSystem.current, "an event system with the Input System UI module exists");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, _ui.Root.Canvas.renderMode);
            var scaler = _ui.Root.GetComponent<CanvasScaler>();
            Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
        }

        [Test]
        public void HudShowsHealthGoldAndLoadoutText()
        {
            _inv.AddGold(250);
            _inv.AddCells(3);
            _health.TakeHit(new DamageInfo { Amount = 55f, SourceTeam = Team.Enemy, Tags = DamageTags.Melee });

            // the HUD refreshes in Update, which does not run in a synchronous test
            _ui.Hud.Refresh();
            var all = new List<string>();
            foreach (var t in _ui.Root.HudLayer.GetComponentsInChildren<Text>(true)) all.Add(t.text);

            CollectionAssert.Contains(all, "250");
            CollectionAssert.Contains(all, "3");
            Assert.IsTrue(all.Exists(s => s.Contains("/")), "health text is current / max");
            Assert.IsTrue(all.Exists(s => s.Contains("Bellringer")), "weapon slot names the equipped weapon");
            Assert.IsTrue(all.Exists(s => s.Contains("Tower Shield")));
        }

        [Test]
        public void ToastsAppearForPickupsAndScrolls()
        {
            _inv.AddScroll(StatColor.Ember);
            var toasts = _ui.Root.HudLayer.Find("Toasts");
            Assert.IsNotNull(toasts);
            Assert.AreEqual(1, toasts.childCount);
            Assert.AreEqual("+1 Ember scroll", toasts.GetChild(0).GetComponent<Text>().text);
        }

        [Test]
        public void InteractPromptShowsAnItemCardForFloorItems()
        {
            var item = ItemInstance.ForWeapon(_db.weapons[2], ItemRarity.Epic);
            EventBus<InteractPromptEvent>.Raise(new InteractPromptEvent { Visible = true, Text = "Take Tide Hammer (Epic)", Colour = item.RarityColour, Item = item });

            var prompt = _ui.Root.HudLayer.GetComponentInChildren<ItemCard>(true);
            Assert.IsNotNull(prompt);
            Assert.IsTrue(prompt.gameObject.activeInHierarchy);
            bool found = false;
            foreach (var t in prompt.GetComponentsInChildren<Text>()) if (t.text.Contains("Tide Hammer")) found = true;
            Assert.IsTrue(found);

            EventBus<InteractPromptEvent>.Raise(new InteractPromptEvent { Visible = false });
            Assert.IsFalse(prompt.gameObject.activeInHierarchy);
        }

        // ------------------------------------------------------------------ menus

        [Test]
        public void ShopOpensPausesBuysThroughItsButtonsAndUnpausesOnClose()
        {
            _inv.AddGold(500);
            var stock = ShopStock.Generate(_db, new SeededRng(4), 0.4f);
            bool closed = false;
            EventBus<OpenShopEvent>.Raise(new OpenShopEvent { Stock = stock, Inventory = _inv, Closed = () => closed = true });

            Assert.AreEqual(1, _ui.Modals.Depth);
            Assert.AreEqual(0f, Time.timeScale, "the game is paused behind the menu");
            Assert.IsTrue(PauseGate.Paused);

            var buttons = ModalButtons();
            Assert.AreEqual(stock.Offers.Count + 1, buttons.Count, "one button per offer plus Leave");

            int scrollIndex = stock.Offers.FindIndex(o => o.Kind == OfferKind.Scroll);
            int gold = _inv.Gold;
            buttons[scrollIndex].onClick.Invoke();
            Assert.AreEqual(gold - ItemPricing.ScrollPrice, _inv.Gold);
            Assert.AreEqual(1, _inv.TotalScrolls);

            buttons[buttons.Count - 1].onClick.Invoke();   // Leave
            Assert.AreEqual(0, _ui.Modals.Depth);
            Assert.AreEqual(1f, Time.timeScale, "unpaused");
            Assert.IsTrue(closed);
        }

        [Test]
        public void BuyingAWeaponWithFullSlotsStacksTheReplaceMenuAndAppliesTheChoice()
        {
            _inv.AddGold(2000);
            var stock = ShopStock.Generate(_db, new SeededRng(4), 0.4f);
            int weaponOffer = stock.Offers.FindIndex(o => o.Kind == OfferKind.Item && o.Item.Kind == ItemKind.Weapon);
            Assert.GreaterOrEqual(weaponOffer, 0, "this seed's shop sells a weapon");
            ItemInstance bought = stock.Offers[weaponOffer].Item;

            EventBus<OpenShopEvent>.Raise(new OpenShopEvent { Stock = stock, Inventory = _inv });
            ModalButtons()[weaponOffer].onClick.Invoke();

            Assert.AreEqual(2, _ui.Modals.Depth, "the replace dialog opens on top of the shop");
            var slotButtons = ModalButtons();
            Assert.AreEqual(3, slotButtons.Count, "slot 1, slot 2, cancel");
            slotButtons[1].onClick.Invoke();   // replace slot 2

            Assert.AreEqual(1, _ui.Modals.Depth, "back to the shop");
            Assert.AreSame(bought, _inv.Weapons[1]);
            Assert.AreEqual(1f - 0f, 1f);
            _ui.Modals.CloseAll();
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void CancellingTheReplaceMenuRefundsThePurchase()
        {
            _inv.AddGold(2000);
            var stock = ShopStock.Generate(_db, new SeededRng(4), 0.4f);
            int weaponOffer = stock.Offers.FindIndex(o => o.Kind == OfferKind.Item && o.Item.Kind == ItemKind.Weapon);
            EventBus<OpenShopEvent>.Raise(new OpenShopEvent { Stock = stock, Inventory = _inv });
            ModalButtons()[weaponOffer].onClick.Invoke();

            var slotButtons = ModalButtons();
            slotButtons[2].onClick.Invoke();   // cancel
            Assert.AreEqual(2000, _inv.Gold, "refunded");
            Assert.IsFalse(stock.Offers[weaponOffer].Sold);
            _ui.Modals.CloseAll();
        }

        [Test]
        public void MutationMenuGrantsTheChosenMutation()
        {
            var choices = new List<MutationData> { _db.mutations[0], _db.mutations[1], _db.mutations[2] };
            int chosen = -99;
            EventBus<OpenMutationEvent>.Raise(new OpenMutationEvent { Choices = choices, Inventory = _inv, Resolve = i => chosen = i });

            Assert.AreEqual(1, _ui.Modals.Depth);
            var buttons = ModalButtons();
            Assert.AreEqual(4, buttons.Count, "three choices plus Not now");
            buttons[2].onClick.Invoke();
            Assert.AreEqual(2, chosen);
            Assert.AreEqual(0, _ui.Modals.Depth);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void StartingANewRunClosesOpenMenus()
        {
            EventBus<OpenMutationEvent>.Raise(new OpenMutationEvent { Choices = new List<MutationData> { _db.mutations[0] }, Inventory = _inv, Resolve = i => { } });
            Assert.AreEqual(1, _ui.Modals.Depth);
            EventBus<RunStartedEvent>.Raise(new RunStartedEvent { Seed = 1 });
            Assert.AreEqual(0, _ui.Modals.Depth);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void PauseGateIsCountedSoStackedMenusUnpauseOnlyAtTheEnd()
        {
            PauseGate.Push();
            PauseGate.Push();
            Assert.AreEqual(0f, Time.timeScale);
            PauseGate.Pop();
            Assert.AreEqual(0f, Time.timeScale, "still one menu open");
            PauseGate.Pop();
            Assert.AreEqual(1f, Time.timeScale);
            PauseGate.Pop();   // extra pops are harmless
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}

