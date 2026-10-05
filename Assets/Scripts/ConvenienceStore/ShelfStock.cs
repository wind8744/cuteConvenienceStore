using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>진열대 하나의 재고. 아직 들여오지 않은 상품의 진열대는 Open 이 false 입니다.</summary>
    public class ShelfStock
    {
        public Product Product { get; }
        public bool Open { get; private set; }
        public int Capacity { get; private set; }
        public int Stock { get; private set; }
        public int Missing => Open ? Capacity - Stock : 0;

        public ShelfStock(Product product, int capacity, bool open)
        {
            Product = product;
            Capacity = capacity;
            Open = open;
            Stock = open ? capacity : 0;
        }

        /// <summary>최대 want 개를 꺼내고 실제로 꺼낸 수를 돌려줍니다.</summary>
        public int Take(int want)
        {
            int taken = Mathf.Clamp(want, 0, Stock);
            Stock -= taken;
            return taken;
        }

        public void Add(int count)
        {
            if (!Open) return;
            Stock = Mathf.Clamp(Stock + count, 0, Capacity);
        }

        /// <summary>저장해 둔 재고로 되돌립니다.</summary>
        public void Restore(int stock) => Stock = Open ? Mathf.Clamp(stock, 0, Capacity) : 0;

        public void SetCapacity(int capacity)
        {
            Capacity = capacity;
            Stock = Mathf.Min(Stock, capacity);
        }

        /// <summary>신상품 입고. 첫 물량은 가득 채워서 들어옵니다.</summary>
        public void OpenAndFill()
        {
            Open = true;
            Stock = Capacity;
        }
    }
}
