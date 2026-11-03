using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace BulkyBook.Models
{
    public class Category
    {
        public int Id { get; set; }
        [Required]
        [StringLength(100)]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;
        [Range(0, 100, ErrorMessage = "Display Order must be between 0 and 100!!")]
        //[ValidateNever]   
        [Display(Name = "Display Order")]
        public int? DisplayOrder { get; set; }
    }
}
