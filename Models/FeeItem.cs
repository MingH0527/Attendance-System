
namespace WebAndMobile.Models
{
    public class FeeItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public string Category { get; set; } = ""; // "Tuition" | "Library" | "Hostel" | "Books" | "Other"
        public bool AmountIsFixed { get; set; } = true; // false = 学生可自行输入数量/金额, e.g. 买书
    }
}

