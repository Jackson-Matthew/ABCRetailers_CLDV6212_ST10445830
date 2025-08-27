using System.ComponentModel.DataAnnotations;

namespace ABC_Retailers_ST10445830.Models.ViewModels
{
    public class OrderCreateViewModel
    {

        [Required]
        [Display(Name = "Customer")]
        public String CustomerId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Product")]
        public String ProductId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Quantity")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Required]
        [Display(Name = "Order Date")]
        [DataType(DataType.Date)]
        public DateTime OrderDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Submitted";

        public List<Customer> Customers { get; set; } = new();
        public List<Product> Products { get; set; } = new();
    }
}
