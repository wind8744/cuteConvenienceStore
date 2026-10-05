using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>진열대 하나를 그립니다. 재고만큼 상품을 올려 두고, 비면 느낌표 말풍선을 깜빡입니다.</summary>
    public class ShelfView : MonoBehaviour
    {
        private readonly SpriteRenderer[] _items = new SpriteRenderer[StoreArt.ShelfSlots];
        private SpriteRenderer _frame, _alert, _alertIcon;

        public ShelfStock Stock { get; private set; }
        /// <summary>진열대 가운데의 월드 좌표.</summary>
        public Vector2 Center => (Vector2)transform.position + new Vector2(1f, 0.5f);

        public void Init(StoreArt art, Vector2Int tile)
        {
            transform.position = new Vector3(tile.x, tile.y, 0f);
            int order = StoreArt.SortOrder(tile.y);
            _frame = gameObject.AddComponent<SpriteRenderer>();
            _frame.sprite = art.Shelf;
            _frame.sortingOrder = order;

            for (int i = 0; i < _items.Length; i++)
            {
                var go = new GameObject("Item");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = StoreArt.ShelfSlotOffset(i);
                _items[i] = go.AddComponent<SpriteRenderer>();
                _items[i].sortingOrder = order + 1;
            }

            var alertGo = new GameObject("Alert");
            alertGo.transform.SetParent(transform, false);
            alertGo.transform.localPosition = new Vector3(1f, 1.8f, 0f);
            _alert = alertGo.AddComponent<SpriteRenderer>();
            _alert.sprite = art.Bubble;
            _alert.sortingOrder = 9000;
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(alertGo.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, 8f / StoreArt.Tile, 0f);
            _alertIcon = iconGo.AddComponent<SpriteRenderer>();
            _alertIcon.sprite = art.Exclaim;
            _alertIcon.sortingOrder = 9001;
        }

        public void Bind(ShelfStock stock, Sprite icon)
        {
            Stock = stock;
            foreach (SpriteRenderer item in _items) item.sprite = icon;
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < _items.Length; i++) _items[i].enabled = Stock.Open && i < Stock.Stock;
            // 아직 들여오지 않은 상품의 진열대는 어둡게 둔다.
            _frame.color = Stock.Open ? Color.white : new Color(0.72f, 0.7f, 0.74f);
        }

        private void Update()
        {
            bool empty = Stock != null && Stock.Open && Stock.Stock == 0;
            _alert.enabled = _alertIcon.enabled = empty && Mathf.FloorToInt(Time.time * 3f) % 2 == 0;
        }
    }
}
