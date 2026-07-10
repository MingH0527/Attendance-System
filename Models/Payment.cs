
using System.ComponentModel.DataAnnotations;
 
namespace WebAndMobile.Models
{
    public class Payment
    {
        // 这两个不再由表单填写，而是在 Controller 里从登录 session/claims 里取出后赋值
        // (仍然保留在 model 里是因为要存进数据库/订单记录)
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";

        public int FeeId { get; set; }
        public string FeeName { get; set; } = "";

        [Required(ErrorMessage = "请输入金额")]
        [Range(0.01, 100000, ErrorMessage = "金额必须大于 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "请选择付款方式")]
        public string PaymentMethod { get; set; } = "";
    }
}

