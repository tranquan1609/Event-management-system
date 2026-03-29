namespace DACSWEBSK.Models
{
    public class GiftRedemption
    {
        public int Id { get; set; }
        public string UserEmail { get; set; } // hoặc UserId nếu có đăng nhập
        public int GiftId { get; set; }
        public Gift Gift { get; set; }
        public DateTime RedeemedAt { get; set; }
        public int? EventId { get; set; } // Nếu muốn gắn với sự kiện
    }

}
