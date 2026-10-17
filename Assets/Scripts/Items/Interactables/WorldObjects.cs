using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Items
{
    /// <summary>An item lying on the floor. Interact to pick it up (gear equips, weapons and skills may ask which slot to replace).</summary>
    public sealed class WorldItem : Interactable
    {
        ItemInstance _item;
        Transform _icon;
        float _t;
        bool _taken;
        float _hold;

        public ItemInstance Item => _item;
        public override string Prompt => _item == null ? "" : $"Take {_item.DisplayName} ({_item.Rarity})";
        public override Color PromptColour => _item == null ? Color.white : _item.RarityColour;
        /// <summary>Makes the item ungrabbable for a moment (a swapped-out item must not be re-taken by the same button press).</summary>
        public void HoldFor(float seconds) => _hold = seconds;

        public override bool CanInteract => !_taken && _item != null && _hold <= 0f;
        public override ItemInstance PreviewItem => _item;

        public void Init(ItemInstance item, Transform icon)
        {
            _item = item;
            _icon = icon;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_hold > 0f) _hold -= Time.deltaTime;
            if (_icon != null) _icon.localPosition = new Vector3(0f, 0.9f + 0.12f * Mathf.Sin(_t * 3f), 0f);
        }

        public override void Interact(PlayerInventory inventory, PlayerController player)
        {
            if (_taken) return;
            inventory.PickUp(_item, transform.position, accepted =>
            {
                if (!accepted) return;
                _taken = true;
                Destroy(gameObject);
            });
        }
    }

    public enum CollectibleKind { Gold, Cell, Scroll }

    /// <summary>
    /// Coins, Cells and scrolls. Gold and Cells hop out, settle and fly to the player; scrolls wait to be touched.
    /// </summary>
    public sealed class Collectible : MonoBehaviour
    {
        CollectibleKind _kind;
        int _value;
        StatColor _colour;
        bool _magnet;
        Vector2 _vel;
        float _age;
        bool _collected;
        PlayerInventory _target;
        Transform _targetTransform;

        public bool autoTick = true;

        public CollectibleKind Kind => _kind;
        public int Value => _value;
        public StatColor Colour => _colour;
        public bool Collected => _collected;

        public void Init(CollectibleKind kind, int value, StatColor colour, bool magnet)
        {
            _kind = kind;
            _value = value;
            _colour = colour;
            _magnet = magnet;
            if (magnet) _vel = new Vector2(Random.Range(-3f, 3f), Random.Range(5f, 8f));
        }

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (_collected) return;
            _age += dt;

            if (_target == null)
            {
                Health p = Health.Player;
                if (p == null) return;
                _target = p.GetComponent<PlayerInventory>();
                _targetTransform = p.transform;
            }
            if (_target == null) return;

            Vector2 centre = (Vector2)_targetTransform.position + Vector2.up * 0.8f;
            Vector2 pos = transform.position;
            float dist = Vector2.Distance(pos, centre);

            if (_magnet)
            {
                bool pulling = _age > 0.45f && dist < 6.5f;
                if (pulling)
                {
                    float speed = 9f + (6.5f - dist) * 3f;
                    transform.position = Vector2.MoveTowards(pos, centre, speed * dt);
                }
                else
                {
                    _vel.y -= 24f * dt;
                    Vector2 next = pos + _vel * dt;
                    var hit = Physics2D.Raycast(new Vector2(next.x, pos.y + 0.2f), Vector2.down, 0.2f + Mathf.Max(0f, -_vel.y * dt) + 0.1f, GameLayers.WalkableMask);
                    if (hit.collider != null && _vel.y <= 0f)
                    {
                        next.y = hit.point.y + 0.16f;
                        _vel = new Vector2(_vel.x * 0.5f, Mathf.Abs(_vel.y) > 3f ? -_vel.y * 0.35f : 0f);
                    }
                    transform.position = next;
                }
                if (dist < 0.7f && _age > 0.2f) Collect();
            }
            else
            {
                transform.position += new Vector3(0f, Mathf.Sin(_age * 3f) * 0.2f * dt, 0f);
                if (dist < 1.0f) Collect();
            }
        }

        void Collect()
        {
            _collected = true;
            switch (_kind)
            {
                case CollectibleKind.Gold: _target.AddGold(_value); break;
                case CollectibleKind.Cell: _target.AddCells(_value); break;
                case CollectibleKind.Scroll: _target.AddScroll(_colour); break;
            }
            Destroy(gameObject);
        }
    }

    /// <summary>A chest holding a pre-rolled item (so a seed fixes its contents). Opening drops the item at its feet.</summary>
    public sealed class LootChest : Interactable
    {
        ItemInstance _contents;
        Transform _lid;
        SpriteRenderer _paintedBody;
        Sprite _paintedOpen;
        bool _opened;

        /// <summary>Index of the room this chest sits in (used to restore saves).</summary>
        public int CellIndex { get; set; } = -1;
        public bool Opened => _opened;
        public ItemInstance Contents => _contents;
        public override string Prompt => _opened ? "" : "Open chest";
        public override bool CanInteract => !_opened;

        public void Init(ItemInstance contents, Transform lid)
        {
            _contents = contents;
            _lid = lid;
        }

        /// <summary>Painted chests swap to an open sprite instead of moving a lid.</summary>
        public void UsePaintedSprites(SpriteRenderer body, Sprite open)
        {
            _paintedBody = body;
            _paintedOpen = open;
        }

        void ShowOpen()
        {
            if (_lid != null) { _lid.localPosition += new Vector3(0.35f, 0.25f, 0f); _lid.localRotation = Quaternion.Euler(0f, 0f, -25f); }
            if (_paintedBody != null && _paintedOpen != null) _paintedBody.sprite = _paintedOpen;
        }

        /// <summary>Marks the chest open without dropping its contents (loading a save).</summary>
        public void RestoreOpened()
        {
            _opened = true;
            ShowOpen();
        }

        public override void Interact(PlayerInventory inventory, PlayerController player)
        {
            if (_opened) return;
            _opened = true;
            ShowOpen();
            if (_contents != null) LootFactory.DropItem(_contents, (Vector2)transform.position + new Vector2(0f, 0.2f));
        }
    }

    /// <summary>The merchant. Interact opens the shop screen (the UI listens for <see cref="OpenShopEvent"/>).</summary>
    public sealed class ShopStand : Interactable
    {
        public ShopStock Stock { get; private set; }
        public int CellIndex { get; set; } = -1;
        public override string Prompt => "Browse wares";
        public override Color PromptColour => new Color(1f, 0.9f, 0.6f);

        public void Init(ShopStock stock)
        {
            Stock = stock;
            Radius = 2.4f;
        }

        public override void Interact(PlayerInventory inventory, PlayerController player)
            => EventBus<OpenShopEvent>.Raise(new OpenShopEvent { Stock = Stock, Inventory = inventory });
    }

    /// <summary>Offers a choice of mutations (passive perks). Single use.</summary>
    public sealed class MutationAltar : Interactable
    {
        List<MutationData> _choices;
        SpriteRenderer _glow;
        bool _used;
        float _t;

        public int CellIndex { get; set; } = -1;
        public bool Used => _used;
        public void RestoreUsed() => _used = true;
        public IList<MutationData> Choices => _choices;
        public override string Prompt => _used ? "" : "Mutation altar";
        public override Color PromptColour => new Color(0.8f, 0.5f, 1f);
        public override bool CanInteract => !_used;

        public void Init(List<MutationData> choices, SpriteRenderer glow)
        {
            _choices = choices;
            _glow = glow;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_glow != null)
            {
                Color c = _glow.color;
                c.a = _used ? 0.1f : 0.35f + 0.25f * Mathf.Sin(_t * 3f);
                _glow.color = c;
            }
        }

        public override void Interact(PlayerInventory inventory, PlayerController player)
        {
            if (_used) return;
            if (inventory.Mutations.Count >= PlayerInventory.MaxMutations)
            {
                EventBus<ToastEvent>.Raise(new ToastEvent { Text = "Mutation slots are full", Colour = new Color(1f, 0.6f, 0.5f) });
                return;
            }

            var options = new List<MutationData>();
            foreach (var m in _choices) if (!inventory.Mutations.Contains(m)) options.Add(m);
            if (options.Count == 0) return;

            EventBus<OpenMutationEvent>.Raise(new OpenMutationEvent
            {
                Choices = options,
                Inventory = inventory,
                Resolve = idx =>
                {
                    if (idx < 0 || idx >= options.Count) return;
                    if (inventory.AddMutation(options[idx])) _used = true;
                }
            });
        }

        /// <summary>For tests and tools: choose without UI.</summary>
        public bool Choose(PlayerInventory inventory, int index)
        {
            if (_used || index < 0 || index >= _choices.Count) return false;
            if (!inventory.AddMutation(_choices[index])) return false;
            _used = true;
            return true;
        }
    }
}

