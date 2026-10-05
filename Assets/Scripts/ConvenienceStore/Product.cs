namespace ConvenienceStore
{
    /// <summary>편의점에서 파는 상품 한 종류. 매입가(Cost)에 사서 판매가(Price)에 팝니다.</summary>
    public class Product
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int Cost;
        public readonly int Price;
        /// <summary>신상품으로 들여오는 데 드는 비용. 처음부터 파는 상품은 0.</summary>
        public readonly int UnlockCost;

        public Product(int id, string name, int cost, int price, int unlockCost)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Price = price;
            UnlockCost = unlockCost;
        }
    }

    public static class ProductCatalog
    {
        /// <summary>진열대 번호(맵의 '1'~'6') 순서와 같습니다.</summary>
        public static readonly Product[] All =
        {
            new Product(0, "삼각김밥", 600, 1200, 0),
            new Product(1, "컵라면", 800, 1500, 0),
            new Product(2, "바나나우유", 900, 1700, 0),
            new Product(3, "과자", 1000, 2000, 20000),
            new Product(4, "아이스크림", 1200, 2500, 45000),
            new Product(5, "도시락", 2500, 5000, 90000),
        };
    }
}
