﻿using System.ComponentModel.DataAnnotations;

namespace AHNet.Web.Areas.Admin.ViewModels
{
    public class LoginViewModel
    {
        public string Username { get; set; }

        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }
}
