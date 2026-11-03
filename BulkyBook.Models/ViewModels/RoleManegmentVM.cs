using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Models.ViewModels
{
    public class RoleManegmentVM
    {
        public ApplicationUser User { get; set; }

        public IEnumerable<SelectListItem> RoleList { get; set; }
    }
}
